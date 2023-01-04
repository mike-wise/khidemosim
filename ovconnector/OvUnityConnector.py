#!/usr/bin/env python3

###############################################################################
#
# Copyright 2020 NVIDIA Corporation
#
# Permission is hereby granted, free of charge, to any person obtaining a copy of
# this software and associated documentation files (the "Software"), to deal in
# the Software without restriction, including without limitation the rights to
# use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
# the Software, and to permit persons to whom the Software is furnished to do so,
# subject to the following conditions:
#
# The above copyright notice and this permission notice shall be included in all
# copies or substantial portions of the Software.
#
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
# FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
# COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
# IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
# CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
#
###############################################################################

###############################################################################
#
# This liveSession sample demonstrates how to connect to a live session using
# the non-destructive live workflow.  A .live layer is used in the stage's session
# layer to contain the changes.  An Omniverse channel is used to broadcast users
# and merge notifications to all clients, and a session config (TOML) file is used
# to determine the "owner" of the session.
#
# * Initialize the Omniverse Resolver Plugin
# * Display existing live sessions for a stage
# * Connect to a live session
# * Set the edit target to the .live layer so changes replicate to other clients
# * Make xform changes to a mesh prim in the .live layer
# * Display the owner of the live session
# * Display the current connected users/peers in the session
# * Emit a GetUsers message to the session channel
# * Display the contents of the session config
# * Merge the changes from the .live session back to the root stage
# * Respond (by exiting) when another user merges session changes back to the root stage
#
###############################################################################

# Python built-in
import argparse
import asyncio
import inspect
import logging
import math
import os
import sys
import time
import json
import copy
import keyboard

from os import listdir
from os.path import isfile, join


# USD imports
from pxr import Gf, Sdf, Usd, UsdGeom, UsdUtils, Ar

# Omni imports
import omni.client
import omni.usd_resolver

# Internal imports
import log, xform_utils, get_char_util, tick_update, session_toml_util
import omni.kit.collaboration.channel_manager as cm
import omni.kit.layers.live_session_channel_manager as lscm
from concurrent.futures import ThreadPoolExecutor

connection_status_subscription = None
g_stage = None
g_stage_merged = False
g_logging_enabled = False
g_channel_manager = None
g_live_session_channel_manager = None
g_end_program = False
g_send_merge_start_message = False
g_send_merge_done_message = False
g_send_get_users_message = False
g_live_session_info = None

LOGGER = log.get_logger("PyLiveSession", level=logging.INFO)


def logCallback(threadName, component, level, message):
    global g_logging_enabled
    if g_logging_enabled:
        LOGGER.setLevel(logging.DEBUG)
        xform_utils.LOGGER.setLevel(logging.DEBUG)
        LOGGER.debug(message)


def connectionStatusCallback(url, connectionStatus):
    if connectionStatus is omni.client.ConnectionStatus.CONNECT_ERROR:
        shutdownOmniverse()
        sys.exit("[ERROR] Failed connection, exiting.")


def startOmniverse():
    global g_logging_enabled
    omni.client.set_log_callback(logCallback)
    if g_logging_enabled:
        omni.client.set_log_level(omni.client.LogLevel.DEBUG)

    if not omni.client.initialize():
        sys.exit("[ERROR] Unable to initialize Omniverse client, exiting.")

    connection_status_subscription = omni.client.register_connection_status_callback(connectionStatusCallback)


def shutdownOmniverse():
    omni.client.live_wait_for_pending_updates()

    connection_status_subscription = None

    omni.client.shutdown()


def isValidOmniUrl(url):
    omniURL = omni.client.break_url(url)
    if omniURL.scheme == "omniverse" or omniURL.scheme == "omni":
        return True
    return False


def save_stage(comment=""):
    global g_stage

    # Set checkpoint message for saving Stage.
    omni.usd_resolver.set_checkpoint_message(comment)

    # Save the proper edit target (in the case that we're live editing)
    edit_target_layer = g_stage.GetEditTarget().GetLayer()
    edit_target_layer.Save()

    # Clear checkpoint message to ensure comment is not used in future file operations.
    omni.usd_resolver.set_checkpoint_message("")
    omni.client.live_process()


def findGeomMesh(existing_stage, search_mesh_str=""):
    global g_stage
    LOGGER.debug(existing_stage)

    g_stage = Usd.Stage.Open(existing_stage)

    if not g_stage:
        shutdownOmniverse()
        sys.exit("[ERROR] Unable to open stage" + existing_stage)

    #meshPrim = stage.GetPrimAtPath('/Root/box_%d' % boxNumber)
    for node in g_stage.Traverse():
        if search_mesh_str:
            if node.IsA(UsdGeom.Xformable):
                node_path = str(node.GetPath())
                if search_mesh_str in node_path:
                    return UsdGeom.Xformable(node)
        else:
            if node.IsA(UsdGeom.Mesh):
                    return UsdGeom.Xformable(node)

    shutdownOmniverse()
    sys.exit("[ERROR] No UsdGeomMesh found in stage:", existing_stage)
    return None


# Channel message callbacks
def merge_finished_cb(user_name, app_name):
    global g_stage_merged
    LOGGER.warning(f"Exiting since the merge was finished by {user_name} - {app_name}.")
    g_stage_merged = True

def merge_started_cb(user_name, app_name):
    LOGGER.warning(f"The merge was started by {user_name} - {app_name}. Do not make any changes")

def hello_cb(user_name, app_name):
    LOGGER.warning(f"{user_name} - {app_name} is in the live session")

def join_cb(user_name, app_name):
    LOGGER.warning(f"{user_name} - {app_name} has joined the live session")

def left_cb(user_name, app_name):
    LOGGER.warning(f"{user_name} - {app_name} has left the live session")



class LiveSessionInfo:
    """ Live Session Info class
    This class attempts to collect all of the logic around the live session file paths and URLs.
    It should be first instantiated with the stage URL (omniverse://server/folder/stage.usd), then
    get_session_folder_path_for_stage() can be used to list the available sessions.

    Once a session is selected, set_session_name() will finish the initialization of all of the paths
    and the other methods can be used.
    
    In the folder that contains the USD to be live-edited, there exists this folder structure:
    <.live> / < my_usd_file.live> / <session_name> / root.live

    get_session_folder_path_for_stage:  <stage_folder> / <.live> / < my_usd_file.live>
    get_live_session_folder_path:       <stage_folder> / <.live> / < my_usd_file.live> / <session_name>
    get_live_session_url:               <stage_folder> / <.live> / < my_usd_file.live> / <session_name> / root.live
    get_live_session_toml_url:          <stage_folder> / <.live> / < my_usd_file.live> / <session_name> / __session__.toml
    get_message_channel_url:            <stage_folder> / <.live> / < my_usd_file.live> / <session_name> / __session__.channel
    
    
    """
    def __init__(self, stage_url: str):
        self.OMNIVERSE_CHANNEL_FILE_NAME = "__session__.channel"
        self.LIVE_SUBFOLDER = "/.live"
        self.LIVE_SUBFOLDER_SUFFIX = ".live"
        self.DEFAULT_LIVE_FILE_NAME = "root.live"
        self.SESSION_TOML_FILE_NAME = "__session__.toml"


        self.stage_url = stage_url
        self.live_file_url = None
        self.channel_url = None
        self.toml_url = None
        self.session_name = None
        self.omni_url = omni.client.break_url(self.stage_url)
        # construct the folder that would contain sessions - <.live> / < my_usd_file.live> / <session_name> / root.live
        self.omni_session_folder_path = os.path.dirname(self.omni_url.path) + self.LIVE_SUBFOLDER + "/" + self.get_stage_file_name() + self.LIVE_SUBFOLDER_SUFFIX
        self.session_folder_string = omni.client.make_url(self.omni_url.scheme, self.omni_url.user, self.omni_url.host, self.omni_url.port, self.omni_session_folder_path)

    def get_session_folder_path_for_stage(self):
        return self.session_folder_string

    def set_session_name(self, session_name):
        self.session_name = session_name

    def get_live_session_folder_path(self):
        return self.omni_session_folder_path + "/" + self.session_name + self.LIVE_SUBFOLDER_SUFFIX

    def get_stage_file_name(self):
        # find the stage file's root name
        usd_file_root = os.path.splitext(os.path.basename(self.omni_url.path))[0]
        return usd_file_root

    def get_live_session_url(self):
        live_session_path = self.get_live_session_folder_path() + "/" + self.DEFAULT_LIVE_FILE_NAME
        live_session_url = omni.client.make_url(self.omni_url.scheme, self.omni_url.user, self.omni_url.host, self.omni_url.port, live_session_path)
        return live_session_url

    def get_live_session_toml_url(self):
        live_session_toml_path = self.get_live_session_folder_path() + "/" + self.SESSION_TOML_FILE_NAME
        live_session_url = omni.client.make_url(self.omni_url.scheme, self.omni_url.user, self.omni_url.host, self.omni_url.port, live_session_toml_path)
        return live_session_url

    def get_message_channel_url(self):
        live_session_channel_path = self.get_live_session_folder_path() + "/" + self.OMNIVERSE_CHANNEL_FILE_NAME
        live_session_url = omni.client.make_url(self.omni_url.scheme, self.omni_url.user, self.omni_url.host, self.omni_url.port, live_session_channel_path)
        return live_session_url


def list_session_users():
    global g_live_session_channel_manager
    # users are cm.PeerUser types
    users: set(cm.PeerUser)
    users = g_live_session_channel_manager.get_users()
    LOGGER.info("Listing session users: ")
    if len(users) == 0:
        LOGGER.info(" - No other users in session")
    for user in users:
        LOGGER.info(f" - {user.user_name}[{user.from_app}]")


async def join_session_channel():
    """ Join the live session channel
    OmniClientUrl
    """
    global g_channel_manager, g_live_session_channel_manager, g_live_session_info

    channel_file_url = g_live_session_info.get_message_channel_url()
    g_live_session_channel_manager = lscm.LiveSessionChannelManager(channel_file_url, None)
    await g_live_session_channel_manager.start_async(g_channel_manager)

    g_live_session_channel_manager.register_hello_callback(hello_cb)
    g_live_session_channel_manager.register_join_callback(join_cb)
    g_live_session_channel_manager.register_left_callback(left_cb)
    g_live_session_channel_manager.register_merge_start_callback(merge_started_cb)
    g_live_session_channel_manager.register_merge_finished_callback(merge_finished_cb)


async def find_or_create_session(stageUrl):
    """Find or Create Session
    This function displays the existing session and allows the user to create a new session

    It will look for this folder structure (expecting stageUrl to contain 'myfile'):
        <.live> / < my_usd_file.live> / <session_name> / root.live
    If it doesn't find any sessions for the current USD file then it will present the option
    to create a new session.
    """
    global g_live_session_info

    g_live_session_info = LiveSessionInfo(stageUrl)
    
    # get the folder contains the sessions
    session_folder_path_for_stage = g_live_session_info.get_session_folder_path_for_stage()

    # list the available sessions, allow the user to pick one
    result, list_entries = await omni.client.list_async(session_folder_path_for_stage)
    print("Select a live session to join: ")
    session_idx = 0
    for entry in list_entries:
        session_name = os.path.splitext(entry.relative_path)[0]
        print(f" [{session_idx}] {session_name}")
        session_idx += 1
    print(f" [n] Create a new session")
    print(f" [q] Quit")

    session_idx_selected = input("Select a live session to join: ")
    live_stage = None
    live_layer = None
    session_name = None

    # the user picked a session, find the root.live file
    if session_idx_selected.isnumeric() and int(session_idx_selected) < session_idx:
        session_name = os.path.splitext(list_entries[int(session_idx_selected)].relative_path)[0] 
        g_live_session_info.set_session_name(session_name)

        # Check the session config file to verify the version matches the current supported version
        toml_url_str = g_live_session_info.get_live_session_toml_url()
        if not session_toml_util.is_version_compatible(toml_url_str):
            actual_version = session_toml_util.get_session_version(toml_url_str)
            print(f"The session config TOML file version is not compatible, exiting.")
            print(f"Expected: {session_toml_util.SUPPORTED_VERSION} Actual: {actual_version}")
            session_toml_util.log_session_toml(toml_url_str)
            g_end_program = True
            return False

        live_session_url = g_live_session_info.get_live_session_url()
        live_stage = Usd.Stage.Open(live_session_url)

    # the user wants a new session, get the new session name
    elif session_idx_selected == "n":
        session_name = input("Enter the new session name: ")
        if not session_name:
            LOGGER.error(f"Invalid session name - exiting")
            shutdownOmniverse()
            exit(1)

        g_live_session_info.set_session_name(session_name)
        
        # get current user name
        _, serverInfo = omni.client.get_server_info(g_live_session_info.stage_url)
        
        # create a new config session file
        OWNER_KEY = "user_name"
        STAGE_URL_KEY = "stage_url"
        MODE_KEY = "mode"
        SESSION_NAME_KEY = "name"
        
        session_config_dict = {
            OWNER_KEY : serverInfo.username,
            STAGE_URL_KEY : g_live_session_info.stage_url,
            MODE_KEY : "default",
            SESSION_NAME_KEY : session_name,
        }
        toml_url_str = g_live_session_info.get_live_session_toml_url()
        write_success = session_toml_util.write_session_toml(toml_url_str, session_config_dict)
        if not write_success:
            LOGGER.error(f"Unable to create session config file <{toml_url_str} - exiting")
            shutdownOmniverse()
            exit(1)

        # create a new root.live session file
        live_session_url = g_live_session_info.get_live_session_url()
        live_stage = Usd.Stage.CreateNew(live_session_url)
    else:
        print(f"Invalid selection, exiting")
        g_end_program = True
        return False


    # Join the message channel for the session
    await join_session_channel()

    # Get the live layer from the live stage
    live_layer = live_stage.GetRootLayer()
    LOGGER.info(f"Selected session URL: {live_session_url}")

    # construct the layers so that we can join the session
    g_stage.GetSessionLayer().subLayerPaths.append(live_layer.identifier)
    g_stage.SetEditTarget(Usd.EditTarget(live_layer))
    return True
    

def swapcol(m,i,j):
    mm = copy.deepcopy(m)
    for k in range(4):
        ix1 = k*4 + i
        ix2 = k*4 + j
        mm[ix1] = m[ix2]
        mm[ix2] = m[ix1]
        # print(f"k:{k} ix1:{ix1} ix2:{ix2}")
    return mm

def swaprow(m,i,j):
    mm = copy.deepcopy(m)
    for k in range(4):
        ix1 = k + i*4
        ix2 = k + j*4
        mm[ix1] = m[ix2]
        mm[ix2] = m[ix1]
        # print(f"k:{k} ix1:{ix1} ix2:{ix2}")
    return mm    


def to_ov_matrix(dict,key):
    try:
        mdict = dict[key]
        m = []
        
        transpose = True
        pattern = 1
        objname = dict["pathname"]
        
        if "ovtransctrl" in dict:
            ovtransctl = dict["ovtransctrl"]
            if ovtransctl=="NoTranspose":
                pattern = 1
                transpose = False
            elif ovtransctl=="Correct":
                pattern = 1
                correct = True
            elif ovtransctl=="NoT":
                pattern = 2
                transpose = True
            elif ovtransctl=="OnlyLeftT":
                pattern = 3
                transpose = True
            elif ovtransctl=="OnlyRightT":
                pattern = 4
                transpose = True
            elif ovtransctl=="NoTandNoTranspose":
                pattern = 2
                transpose = False
            # print(f"{ovtransctl} transpose:{transpose}  {objname}")
        
        if transpose:
            m.append(mdict["e00"]); m.append(mdict["e10"]); m.append(mdict["e20"]); m.append(mdict["e30"])       
            m.append(mdict["e01"]); m.append(mdict["e11"]); m.append(mdict["e21"]); m.append(mdict["e31"])
            m.append(mdict["e02"]); m.append(mdict["e12"]); m.append(mdict["e22"]); m.append(mdict["e32"])
            m.append(mdict["e03"]); m.append(mdict["e13"]); m.append(mdict["e23"]); m.append(mdict["e33"])
        else:
            m.append(mdict["e00"]); m.append(mdict["e01"]); m.append(mdict["e02"]); m.append(mdict["e03"])       
            m.append(mdict["e10"]); m.append(mdict["e11"]); m.append(mdict["e12"]); m.append(mdict["e13"])
            m.append(mdict["e20"]); m.append(mdict["e21"]); m.append(mdict["e22"]); m.append(mdict["e23"])
            m.append(mdict["e30"]); m.append(mdict["e31"]); m.append(mdict["e32"]); m.append(mdict["e33"])        


        if pattern==1:   # The correct pattern, z row and col both negated
            mat = Gf.Matrix4d(  m[0],  m[1], -m[2],  m[3],  
                                m[4],  m[5], -m[6],  m[7],  
                               -m[8], -m[9],  m[10],-m[11],   
                                m[12], m[13],-m[14], m[15] )
        elif pattern==2: # do no transform at all
            mat = Gf.Matrix4d(  m[0],  m[1], m[2],  m[3],  
                                m[4],  m[5], m[6],  m[7],  
                                m[8],  m[9], m[10], m[11],   
                                m[12], m[13],m[14], m[15] )
        elif pattern==3:  # only transform from the left which negates the z row
            mat = Gf.Matrix4d(  m[0],  m[1],  m[2],  m[3],  
                                m[4],  m[5],  m[6],  m[7],  
                               -m[8], -m[9], -m[10],-m[11],   
                                m[12], m[13], m[14], m[15] )
        elif pattern==4: # only transform from the right which negates the z column
            mat = Gf.Matrix4d(  m[0],  m[1], -m[2],  m[3],  
                                m[4],  m[5], -m[6],  m[7],  
                                m[8],  m[9], -m[10], m[11],   
                                m[12], m[13],-m[14], m[15] )
        else: # if in doubt do the correct pattern
            mat = Gf.Matrix4d(  m[0],  m[1], -m[2],  m[3],  
                                m[4],  m[5], -m[6],  m[7],  
                               -m[8], -m[9],  m[10],-m[11],   
                                m[12], m[13],-m[14], m[15] )
    

    except KeyError as e:
        print(f"Exception in to_ov_matrix - KeyError - reason {str(e)}")
        mat = Gf.Matrix4d(1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1 )  
    except NameError as e:
        print(f"Exception in to_ov_matrix - NameError - reason {str(e)}")        
        mat = Gf.Matrix4d(1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1 )  
    except Exception as ex:
        print(f"Exception {ex.__class__} caught in to_ov_matrix")
        mat = Gf.Matrix4d(1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1 )  
        
    # cormat = Gf.Matrix4d(  m[0],  m[1], -m[2],  m[3],  
    #                        m[4],  m[5], -m[6],  m[7],  
    #                       -m[8], -m[9],  m[10],-m[11],   
    #                        m[12], m[13],-m[14], m[15] )
    # dmat = cormat - mat
    # print(f"to_ov_matrix - {key} :{dmat}")
    return mat
    
def tovek(dict,key):
    try:
        vdict = dict[key]
        x = float(vdict["x"])
        y = float(vdict["y"])
        z = float(vdict["z"])
        vek = [x,y,z]
    except Exception as ex:
        print(f"Exception {ex.__class__} caught in tovek")
        vek = [0,0,0]
    # print(f"tovek - {key}  vek:{vek}")
    return vek
    
def process_json_lines(lines):
    global g_stage
    
    try:
      i = 0
      singlestep = False
      
      LOGGER.info(f"processing {len(lines)} json lines")
      for li in lines:
        try:
          if len(li)>0 and li[0]!="#":
              objdict = json.loads(li)
              if "ovcls" not in objdict:
                  # not an ovcls, silently continue
                  # print(f"Warning - ovcls not in dict")
                  continue
              cls = objdict["ovcls"]
              if cls != "MmOvObj":
                  print(f"Warning - unknown ovcls:{cls}")
                  continue
              # for key,val in objdict.items():
              #      print(f"  key:{str(key)} val:{str(val)}")
              simtime = float(objdict["simtime"])
              primpath = objdict["pathname"]
              translate = tovek(objdict,"position")
              loctranslate = tovek(objdict,"locposition")
              rot_xyz = tovek(objdict,"eulerangles")
              loc_rot_xyz = tovek(objdict,"loceulerangles")
              scale= tovek(objdict,"localscale")
              if "flatten" in objdict["extras"]:
                  # print("Choosing loctowctrans because Flatten")
                  locmat = to_ov_matrix(objdict,"loctowctrans")
              else:
                  # print("Choosing loctrans because not Flatten")
                  locmat = to_ov_matrix(objdict,"loctrans")
              
              if i==0:
                  start = time.time()
                  simstart = simtime
                  elap = 0
              else:
                  elap = time.time()-start
                  # print(f"simtime:{simtime} simstart:{simstart}")
                  simelap = simtime-simstart
                  if 2*elap<simelap:
                      time.sleep(simelap-(2*elap))
                  
              srt_action = xform_utils.TransformPrimSRT(
                  g_stage,
                  primpath,
                  translation=loctranslate,
                  rotation_euler=loc_rot_xyz,
                  rotation_order=Gf.Vec3i(0, 1, 2),
                  scale=scale,
                  loctrans=locmat
              )           
              srt_action.do()
              # Send live updates to the server and process live updates received from the server
              omni.client.live_process()           
              # print(f"xformed {primpath} {i} simtime:{simtime} elap:{elap}")     
              i += 1
              # print(f"{i} step finished")
              if keyboard.is_pressed('q'):  # if key 'q' is pressed 
                  print('q pressed - Quitting')
                  break
              if keyboard.is_pressed('s'):  # if key 's' is pressed 
                  print('s pressed - Single-Step triggered')
                  singlestep = True
              if singlestep:
                  option = "x"
                  print(f"{i} Press s to Single-Step - j to continue at full speed - q to quit")
                  while option!=b's' and option!=b'j':
                      time.sleep(1)
                      option = get_char_util.getChar()
                      if option==b'j':
                          singlestep = False
                      if option==b'q':
                          break
        except KeyError as e:
            print(f"Exception  in process_json_lines - KeyError - reason {str(e)}")
        except Exception as ex:
            print(f"Exception {ex.__class__} caught in process_json_lines")
                  
    except Exception as ex:
        print(f"Exception {ex.__class__} caught in process_json_lines")
            
            

def end_and_merge_session():
    """
    End and Merge Session - This function will check that it has ownership (from the TOML file), then merge live deltas to the root layer

    """
    global g_live_session_info, g_send_merge_start_message, g_send_merge_done_message, g_stage_merged

    # Do we have authority (check TOML)?
    # Gather the latest changes from the live stage
    # Send a MERGE_STARTED channel message
    # Create a checkpoint on the live layer (don't force if no changes)
    # Create a checkpoint on the root layer (don't force if no changes)
    # Merge the live deltas to the root layer
    # Save the root layer
    # Clear the live layer
    # Create a checkpoint on the root layer
    # Send a MERGE_FINISHED channel message

    # get current user name
    _, serverInfo = omni.client.get_server_info(g_live_session_info.stage_url)
    if not serverInfo:
        LOGGER.error("Invalid server info while retrieving username")
        return

    # get the session owner user name
    live_session_toml_url = g_live_session_info.get_live_session_toml_url()
    session_owner = session_toml_util.get_session_owner(live_session_toml_url)
    
    # stop the merge if they are not the same
    if session_owner != serverInfo.username:
        LOGGER.warning(f"The session owner is: {session_owner}, your user name is {serverInfo.username}, stopping the merge")
        return

    # gather the latest changes from the live stage
    omni.client.live_process()

    # send a merge started message
    g_send_merge_start_message = True
    while g_send_merge_start_message:
        time.sleep(0.1)
    
    # checkpoint the live layer
    omni.client.create_checkpoint(g_live_session_info.live_file_url, f"Pre-merge for {g_live_session_info.session_name} session", False)

    # checkpoint the root layer (don't force if there are no changes)
    omni.client.create_checkpoint(g_live_session_info.stage_url, f"Pre-merge for {g_live_session_info.session_name} session", False)

    # UsdUtilsFlattenLayerStack
    def resolve_asset_path(layer, asset_path):
        absolute_path = layer.ComputeAbsolutePath(asset_path)
        result, list_entry = omni.client.stat(absolute_path)
        if result != omni.client.Result.OK:
            LOGGER.info(f"[resolve_asset_path]:\n asset_path: {asset_path}")
            return asset_path

        # Make this path relative to current layer
        real_path = g_stage.GetRootLayer().realPath
        relative_path = omni.client.make_relative_url(real_path, absolute_path)

        #relative_path = relative_path.replace("\\", "/")
        LOGGER.info(f"[resolve_asset_path]:\n asset_path: {asset_path}\n absolute_path: {absolute_path}\n realPath: {real_path}\n relative path: {relative_path}")

        return relative_path

    flatten_stage = Usd.Stage.CreateInMemory()
    flatten_stage.GetRootLayer().subLayerPaths.append(g_stage.GetEditTarget().GetLayer().identifier)
    flatten_stage.GetRootLayer().subLayerPaths.append(g_stage.GetRootLayer().identifier)

    flattened_layer = UsdUtils.FlattenLayerStack(flatten_stage, resolve_asset_path)

    # merge the live layer changes to the root layer
    Sdf.CopySpec(flattened_layer, Sdf.Path.absoluteRootPath, g_stage.GetRootLayer(), Sdf.Path.absoluteRootPath)
    
    # change the edit target to the root layer
    live_edit_target = g_stage.GetEditTarget()
    g_stage.SetEditTarget(Usd.EditTarget(g_stage.GetRootLayer()))
    # save and checkpoint the root layer (but not this way)
    save_stage(f"After merging the live session: {g_live_session_info.session_name}")

    # clear the changes and flush the live layer to Nucleus
    live_edit_target.GetLayer().Clear()
    omni.client.live_process()

    # send a merge finished message
    g_send_merge_done_message = True
    while g_send_merge_done_message:
        time.sleep(0.1)

    g_stage_merged = True
    
    
def delete_file(filename: str):
    if os.path.exists(filename):
        os.remove(filename)
        # print(f"deleted {filename}")
    else:
        print(f"file {filename} does not exist so can not delete")
        
def delete_files(dirname: str, filenamelist):
    for fname in filenamelist:
        fullfname = dirname+fname
        delete_file(fullfname)
    
#function to return files in a directory
def file_in_directory(dirname: str):
    onlyfiles = [f for f in listdir(dirname) if isfile(join(dirname, f))]
    return(onlyfiles)

#function comparing two lists

def list_comparison(OriginalList: list, NewList: list):
    differencesList = [x for x in NewList if x not in OriginalList] #Note if files get deleted, this will not highlight them
    return(differencesList)


def file_watcher(watchDirectory: str, pollTime: float):
    while True:
        if 'watching' not in locals(): #Check if this is the first time the function has run
            previousFileList = file_in_directory(watchDirectory)
            watching = 1
            nfound = len(previousFileList)
            print(f'First Watch found {nfound}')
            npass = 1
            while nfound>0 and npass<5:
               delete_files(watchDirectory,previousFileList)
               previousFileList = file_in_directory(watchDirectory)
               nfound = len(previousFileList)
               print(f'First Watch now found {nfound}')
               npass += 1
        
        time.sleep(pollTime)
        
        newFileList = file_in_directory(watchDirectory)
        
        fileDiff = list_comparison(previousFileList, newFileList)
        
        previousFileList = newFileList
        if keyboard.is_pressed('q'):  # if key 'q' is pressed 
            print('q pressed - Quitting')
            break        
        if len(fileDiff) == 0: continue
        for jfname in fileDiff:
            fulljfname = f"{watchDirectory}{jfname}"
            # print(f"Opening {fulljfname}")
            with open(fulljfname) as f:
                lines = f.read().splitlines()                 
            process_json_lines(lines)
            delete_file(fulljfname)
    
    
    

def run_live_edit(prim, stageUrl, jsonFileName, watch_dir):
    global g_stage, g_end_program, g_stage_merged, g_send_get_users_message
    angle = 0
    prim_path = prim.GetPath()
    prompt_msg = inspect.cleandoc(
        """

        Enter an option:
         [t] transform the mesh
         [j] read json_file_name
         [w] watch watcher directory
         [o] list session owner/admin
         [u] list session users
         [g] emit a GetUsers message (note there will be no response unless another app is connected to the same session)
         [c] log contents of the session config file
         [m] merge changes and end the session
         [q] quit.
         """
    )
    LOGGER.info(f"Begin Live Edit on {prim_path}")
    LOGGER.info(f"{prompt_msg}")

    while True:
        option = get_char_util.getChar()

        if g_stage_merged:
            LOGGER.info(f"Exiting since a merge has completed")
            option = b'q'

        omni.client.live_process()
        if option == b't':
            angle = (angle + 15) % 360
            radians = angle * 3.1415926 / 180.0
            # x = math.sin(radians) * 10.0
            # y = math.cos(radians) * 10.0
            x = math.sin(radians) * 0.01
            y = math.cos(radians) * 0.01

            # Get srt transform from prim
            translate, rot_xyz, scale = xform_utils.get_srt_xform_from_prim(prim)

            # Translate and rotate
            translate += Gf.Vec3d(x, 0.0, y)
            rot_xyz = Gf.Vec3d(rot_xyz[0], angle, rot_xyz[2])

            LOGGER.info(f"Setting pos [{translate[0]:.2f}, {translate[1]:.2f}, {translate[2]:.2f}] and rot [{rot_xyz[0]:.2f}, {rot_xyz[1]:.2f}, {rot_xyz[2]:.2f}]")
            
            # Set srt transform
            srt_action = xform_utils.TransformPrimSRT(
                g_stage,
                prim.GetPath(),
                translation=translate,
                rotation_euler=rot_xyz,
                rotation_order=Gf.Vec3i(0, 1, 2),
                scale=scale,
            )
            srt_action.do()
            
            # Send live updates to the server and process live updates received from the server
            omni.client.live_process()

        ## \todo: Add renaming for parity with C++ sample

        elif option == b'o':
            session_owner = session_toml_util.get_session_owner(g_live_session_info.get_live_session_toml_url())
            LOGGER.info(f"Session Owner: {session_owner}")

        elif option == b'u':
            list_session_users()

        elif option == b'g':
            LOGGER.info("Blasting GET_USERS message to channel")
            # send a get_users message
            g_send_get_users_message = True
            while g_send_get_users_message:
                time.sleep(0.1)

        elif option == b'c':
            LOGGER.info("Retrieving session config file: ")
            live_session_toml_url = g_live_session_info.get_live_session_toml_url()
            session_toml_util.log_session_toml(live_session_toml_url)

        elif option == b'm':
            LOGGER.info("Ending session and Merging live changes to root layer: ")
            end_and_merge_session()

        elif option == b'j':
            LOGGER.info(f"Reading Json file {jsonFileName}")
            with open(jsonFileName) as f:
                lines = f.read().splitlines()            
                LOGGER.info(f"Read {len(lines)} lines")
            process_json_lines(lines)

        elif option == b'w':
            LOGGER.info(f"Watching directory {watch_dir}")
            file_watcher(watch_dir, 0.01 )
            # process_json_lines(lines)
    
        elif option == b'q' or option == chr(27).encode():
            LOGGER.info("Live edit complete")
            g_end_program = True
            break
        else:
            LOGGER.info(prompt_msg)


async def main():
    global g_logging_enabled, g_end_program, g_channel_manager, g_send_merge_start_message, g_send_merge_done_message, g_send_get_users_message

    # Set the hang detection time on synchronous client methods to 10 seconds
    # There's a movement to remove _any_ sync client methods, but we're using
    # them in the run_live_edit function because blocking keyboard methods don't 
    # work well in async functions
    omni.client.set_hang_detection_time_ms(10000)

    parser = argparse.ArgumentParser(description="Python Omniverse Client Sample",
                                     formatter_class=argparse.ArgumentDefaultsHelpFormatter)

    parser.add_argument("-v", "--verbose", action='store_true', default=False)
    parser.add_argument("-e", "--existing", action="store", required=True, help ="Omniverse scene name (must be hosted in Nucleus")
    parser.add_argument("-m", "--mesh", action="store", required=False, default="", help="mesh to transform")
    parser.add_argument("-j", "--jsonfile", action="store", required=False, default="", help="Input file with json xforms")
    parser.add_argument("-w", "--watchdir", action="store", required=False, default="", help="Rundir to watch json xforms")

    args = parser.parse_args()

    stage_url = args.existing
    g_logging_enabled = args.verbose
    search_mesh_str = args.mesh
    json_file_name = args.jsonfile
    watch_dir = args.watchdir

    startOmniverse()

    if stage_url and not isValidOmniUrl(stage_url):
        msg = ("This is not an Omniverse Nucleus URL: %s \n"
                "Correct Omniverse URL format is: omniverse://server_name/Path/To/Example/Folder/helloWorld_py.usd")
        LOGGER.error(msg, stage_url)
        shutdownOmniverse()
        exit(-1)

    boxMesh = None

    LOGGER.debug("Stage url: %s", stage_url)
    boxMesh = findGeomMesh(stage_url, search_mesh_str)

    # Setup a tick update for the async channel messages
    app_update = tick_update.TickUpdate()
    app_update.setup_tick(0.01666)
    app_update.start()

    # This was in the Kit extension, but putting it here because we don't need a 
    # ChannelManager outside of this context
    g_channel_manager = cm.ChannelManager(app_name = "Python Connect Sample") 
    g_channel_manager.on_startup()

    if not boxMesh:
        shutdownOmniverse()
        sys.exit("[ERROR] Unable to find mesh in stage")
    else:
        success = await find_or_create_session(stage_url)
        if not success:
            await app_update.stop()
            shutdownOmniverse()
            exit(1)

    if boxMesh is not None:
        # Run a CPU bound operation (or I/O bound in this case) in the ThreadPoolExecutor
        # This will make the coroutine block, but won't block
        # the event loop; other coroutines can run in meantime.
        g_loop.run_in_executor(g_thread_pool_executor, run_live_edit, boxMesh.GetPrim(), stage_url, json_file_name, watch_dir)        
    
    while not g_end_program:
        await asyncio.sleep(0.1)
        if g_send_merge_start_message:
            await g_live_session_channel_manager.broadcast_merge_started_message_async()
            g_send_merge_start_message = False
        if g_send_merge_done_message:
            await g_live_session_channel_manager.broadcast_merge_done_message_async()
            g_send_merge_done_message = False
        if g_send_get_users_message:
            await g_live_session_channel_manager.broadcast_get_users_message_async()
            g_send_get_users_message = False

    g_live_session_channel_manager.stop()
    await asyncio.sleep(0.1)
    
    g_channel_manager.on_shutdown()
    g_channel_manager = None

    await asyncio.sleep(0.1)

    await app_update.stop()
    
    shutdownOmniverse()




if __name__ == "__main__":
    g_loop = asyncio.get_event_loop()
    g_thread_pool_executor = ThreadPoolExecutor() # Create a ThreadPool with !2 threads
    g_loop.run_until_complete(main())
