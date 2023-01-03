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

# Python built-in
import argparse
import logging
import math
import sys

# USD imports
from pxr import Gf, Sdf, Usd, UsdLux, UsdGeom, UsdShade, UsdPhysics

# Omni imports
import omni.client
import omni.usd_resolver

# Internal imports
import log, xform_utils, get_char_util

g_connection_status_subscription = None
g_stage = None

LOGGER = log.get_logger("PyHelloWorld", level=logging.INFO)


def logCallback(threadName, component, level, message):
    if logging_enabled:
        LOGGER.setLevel(logging.DEBUG)
        xform_utils.LOGGER.setLevel(logging.DEBUG)
        LOGGER.debug(message)


def connectionStatusCallback(url, connectionStatus):
    if connectionStatus is omni.client.ConnectionStatus.CONNECT_ERROR:
        sys.exit("[ERROR] Failed connection, exiting.")


def startOmniverse(doLiveEdit):
    omni.client.set_log_callback(logCallback)
    if logging_enabled:
        omni.client.set_log_level(omni.client.LogLevel.DEBUG)

    if not omni.client.initialize():
        sys.exit("[ERROR] Unable to initialize Omniverse client, exiting.")

    g_connection_status_subscription = omni.client.register_connection_status_callback(connectionStatusCallback)


def shutdownOmniverse():
    omni.client.live_wait_for_pending_updates()

    g_connection_status_subscription = None

    omni.client.shutdown()


def isValidOmniUrl(url):
    omniURL = omni.client.break_url(url)
    if omniURL.scheme == "omniverse" or omniURL.scheme == "omni":
        return True
    return False


def createOmniverseModel(path, live_edit):
    LOGGER.info("Creating Omniverse stage")
    global g_stage

    # Use a "".live" extension for live updating, otherwise use a ".usd" extension
    stageUrl = path + "/helloworld_py" + (".live" if live_edit else ".usd")
    omni.client.delete(stageUrl)

    LOGGER.info("Creating stage: %s", stageUrl)

    g_stage = Usd.Stage.CreateNew(stageUrl)
    UsdGeom.SetStageUpAxis(g_stage, UsdGeom.Tokens.y)
    UsdGeom.SetStageMetersPerUnit(g_stage, 0.01)

    LOGGER.info("Created stage: %s", stageUrl)

    return stageUrl


def logConnectedUsername(stageUrl, output_log = True):
    _, serverInfo = omni.client.get_server_info(stageUrl)

    if serverInfo:
        LOGGER.info("Connected username: %s", serverInfo.username)
    return serverInfo.username


def createPhysicsScene(rootUrl):
    global g_stage

    sceneName = "/physicsScene"
    scenePrimPath = rootUrl + sceneName

	# Create physics scene, note that we dont have to specify gravity
    # the default value is derived based on the scene up Axis and meters per unit.
	# Hence in this case the gravity would be (0.0, -981.0, 0.0) since we have
	# defined the Y up-axis and we are having a scene in centimeters.
    UsdPhysics.Scene.Define(g_stage, scenePrimPath)

def enablePhysics(prim, dynamic):
	if dynamic:
		# Make the cube a physics rigid body dynamic
		UsdPhysics.RigidBodyAPI.Apply(prim)

	# Add collision
	UsdPhysics.CollisionAPI.Apply(prim)

	if prim.IsA(UsdGeom.Mesh):
		meshCollisionAPI = UsdPhysics.MeshCollisionAPI.Apply(prim)
		if dynamic:
			# set mesh approximation to convexHull for dynamic meshes
			meshCollisionAPI.GetApproximationAttr().Set(UsdPhysics.Tokens.convexHull)
		else:
			# set mesh approximation to none - triangle mesh as is will be used
			meshCollisionAPI.GetApproximationAttr().Set(UsdPhysics.Tokens.none)

# create dynamic cube
def createDynamicCube(stageUrl, rootUrl, size):
    global g_stage
	# Create the geometry inside of "Root"
    cubeName = "/cube"
    cubePrimPath = rootUrl + cubeName
    cube = UsdGeom.Cube.Define(g_stage, cubePrimPath)

    if not cube:
        sys.exit("[ERROR] Failure to create cube")

	# Move it up
    cube.AddTranslateOp().Set(Gf.Vec3f(65.0, 300.0, 65.0))

    cube.GetSizeAttr().Set(size)

    enablePhysics(cube.GetPrim(), True)

	# Commit the changes to the USD
    save_stage(stageUrl, comment='Created a dynamic cube.')

# Create a simple quad in USD with normals and add a collider
def createQuad(stageUrl, rootUrl, size):
    global g_stage
	# Create the geometry inside of "Root"
    quadName = "/quad"
    quadPrimPath = rootUrl + quadName
    mesh = UsdGeom.Mesh.Define(g_stage, quadPrimPath)

    if not mesh:
        sys.exit("[ERROR] Failure to create cube")

	# Add all of the vertices
    points = [
		Gf.Vec3f(-size, 0.0, -size),
		Gf.Vec3f(-size, 0.0, size),
		Gf.Vec3f(size, 0.0, size),
		Gf.Vec3f(size, 0.0, -size)]
    mesh.CreatePointsAttr(points)

	# Calculate indices for each triangle
    vecIndices = [ 0, 1, 2, 3 ]
    mesh.CreateFaceVertexIndicesAttr(vecIndices)

	# Add vertex normals
    meshNormals = [
		Gf.Vec3f(0.0, 0.0, 1.0),
		Gf.Vec3f(0.0, 0.0, 1.0),
		Gf.Vec3f(0.0, 0.0, 1.0),
		Gf.Vec3f(0.0, 0.0, 1.0) ]
    mesh.CreateNormalsAttr(meshNormals)

	# Add face vertex count
    faceVertexCounts = [ 4 ]
    mesh.CreateFaceVertexCountsAttr(faceVertexCounts)

	# set is as a static triangle mesh
    enablePhysics(mesh.GetPrim(), False)

	# Commit the changes to the USD
    save_stage(stageUrl, comment='Created a Quad.')

h = 50.0
boxVertexIndices = [ 0,  1,  2,  1,  3,  2,
                     4,  5,  6,  4,  6,  7,
                     8,  9, 10,  8, 10, 11,
                    12, 13, 14, 12, 14, 15,
                    16, 17, 18, 16, 18, 19,
                    20, 21, 22, 20, 22, 23 ]
boxVertexCounts = [ 3 ] * 12
boxNormals = [ ( 0,  0, -1), ( 0,  0, -1), ( 0,  0, -1), ( 0,  0, -1),
               ( 0,  0,  1), ( 0,  0,  1), ( 0,  0,  1), ( 0,  0,  1),
               ( 0, -1,  0), ( 0, -1,  0), ( 0, -1,  0), ( 0, -1,  0),
               ( 1,  0,  0), ( 1,  0,  0), ( 1,  0,  0), ( 1,  0,  0),
               ( 0,  1,  0), ( 0,  1,  0), ( 0,  1,  0), ( 0,  1,  0),
               (-1,  0,  0), (-1,  0,  0), (-1,  0,  0), (-1,  0,  0)]
boxPoints = [ ( h, -h, -h), (-h, -h, -h), ( h,  h, -h), (-h,  h, -h),
              ( h,  h,  h), (-h,  h,  h), (-h, -h,  h), ( h, -h,  h),
              ( h, -h,  h), (-h, -h,  h), (-h, -h, -h), ( h, -h, -h),
              ( h,  h,  h), ( h, -h,  h), ( h, -h, -h), ( h,  h, -h),
              (-h,  h,  h), ( h,  h,  h), ( h,  h, -h), (-h,  h, -h),
              (-h, -h,  h), (-h,  h,  h), (-h,  h, -h), (-h, -h, -h) ]
boxUVs = [ (0, 0), (0, 1), (1, 1), (1, 0),
           (0, 0), (0, 1), (1, 1), (1, 0),
           (0, 0), (0, 1), (1, 1), (1, 0),
           (0, 0), (0, 1), (1, 1), (1, 0),
           (0, 0), (0, 1), (1, 1), (1, 0),
           (0, 0), (0, 1), (1, 1), (1, 0) ]

def save_stage(stageUrl, comment=""):
    global g_stage

    # Set checkpoint message for saving Stage.
    omni.usd_resolver.set_checkpoint_message(comment)

    # Save the proper edit target (in the case that we're live editing)
    edit_target_layer = g_stage.GetEditTarget().GetLayer()
    edit_target_layer.Save()

    # Clear checkpoint message to ensure comment is not used in future file operations.
    omni.usd_resolver.set_checkpoint_message("")
    omni.client.live_process()

def createBox(stageUrl, rootUrl, boxNumber=0):
    global g_stage
    boxUrl = rootUrl + '/box_%d' % boxNumber

    boxPrim = UsdGeom.Mesh.Define(g_stage, boxUrl)

    boxPrim.CreateDisplayColorAttr([(0.463, 0.725, 0.0)])
    boxPrim.CreatePointsAttr(boxPoints)
    boxPrim.CreateNormalsAttr(boxNormals)
    boxPrim.CreateFaceVertexCountsAttr(boxVertexCounts)
    boxPrim.CreateFaceVertexIndicesAttr(boxVertexIndices)
    texCoords = boxPrim.CreatePrimvar("st", Sdf.ValueTypeNames.TexCoord2fArray, UsdGeom.Tokens.varying)
    texCoords.Set(boxUVs)
    texCoords.SetInterpolation("vertex")

    if not boxPrim:
        sys.exit("[ERROR] Failure to create box")

    # Set init transformation
    srt_action = xform_utils.TransformPrimSRT(
        g_stage,
        boxPrim.GetPath(),
        translation=Gf.Vec3d(0.0, 100.0, 0.0),
        rotation_euler=Gf.Vec3d(20.0, 0.0, 20.0),
    )
    srt_action.do()

    enablePhysics(boxPrim.GetPrim(), True)

    save_stage(stageUrl, comment='Created a box.')

    return boxPrim

def findGeomMesh(existing_stage, boxNumber=0):
    global g_stage
    LOGGER.debug(existing_stage)

    g_stage = Usd.Stage.Open(existing_stage)

    if not g_stage:
        sys.exit("[ERROR] Unable to open stage" + existing_stage)

    #meshPrim = stage.GetPrimAtPath('/Root/box_%d' % boxNumber)
    for node in g_stage.Traverse():
        if node.IsA(UsdGeom.Mesh):
            return UsdGeom.Mesh(node)

    sys.exit("[ERROR] No UsdGeomMesh found in stage:", existing_stage)
    return None

def uploadMaterial(destination_path):
    uriPath = destination_path + "/Materials"
    omni.client.delete(uriPath)
    omni.client.copy("resources/Materials", uriPath)


def createMaterial(mesh, stageUrl):
    # Create a material instance for this in USD
    materialName = "Fieldstone"
    newMat = UsdShade.Material.Define(g_stage, "/Root/Looks/Fieldstone")

    matPath = '/Root/Looks/Fieldstone'

    # MDL Shader
    # Create the MDL shader
    mdlShader = UsdShade.Shader.Define(g_stage, matPath+'/Fieldstone')
    mdlShader.CreateIdAttr("mdlMaterial")

    mdlShaderModule = "./Materials/Fieldstone.mdl"
    mdlShader.SetSourceAsset(mdlShaderModule, "mdl")
    mdlShader.GetPrim().CreateAttribute("info:mdl:sourceAsset:subIdentifier", Sdf.ValueTypeNames.Token, True).Set(materialName)

    mdlOutput = newMat.CreateSurfaceOutput("mdl")
    mdlOutput.ConnectToSource(mdlShader, "out")

    # USD Preview Surface Shaders

    # Create the "USD Primvar reader for float2" shader
    primStShader = UsdShade.Shader.Define(g_stage, matPath+'/PrimST')
    primStShader.CreateIdAttr("UsdPrimvarReader_float2")
    primStShader.CreateOutput("result", Sdf.ValueTypeNames.Float2)
    primStShader.CreateInput("varname", Sdf.ValueTypeNames.Token).Set("st")

    # Create the "Diffuse Color Tex" shader
    diffuseColorShader = UsdShade.Shader.Define(g_stage, matPath+'/DiffuseColorTex')
    diffuseColorShader.CreateIdAttr("UsdUVTexture")
    texInput = diffuseColorShader.CreateInput("file", Sdf.ValueTypeNames.Asset)
    texInput.Set("./Materials/Fieldstone/Fieldstone_BaseColor.png")
    texInput.GetAttr().SetColorSpace("RGB")
    diffuseColorShader.CreateInput("st", Sdf.ValueTypeNames.Float2).ConnectToSource(primStShader.CreateOutput("result", Sdf.ValueTypeNames.Float2))
    diffuseColorShaderOutput = diffuseColorShader.CreateOutput("rgb", Sdf.ValueTypeNames.Float3)

    # Create the "Normal Tex" shader
    normalShader = UsdShade.Shader.Define(g_stage, matPath+'/NormalTex')
    normalShader.CreateIdAttr("UsdUVTexture")
    normalTexInput = normalShader.CreateInput("file", Sdf.ValueTypeNames.Asset)
    normalTexInput.Set("./Materials/Fieldstone/Fieldstone_N.png")
    normalTexInput.GetAttr().SetColorSpace("RAW")
    normalShader.CreateInput("st", Sdf.ValueTypeNames.Float2).ConnectToSource(primStShader.CreateOutput("result", Sdf.ValueTypeNames.Float2))
    normalShaderOutput = normalShader.CreateOutput("rgb", Sdf.ValueTypeNames.Float3)

    # Create the USD Preview Surface shader
    usdPreviewSurfaceShader = UsdShade.Shader.Define(g_stage, matPath+'/PreviewSurface')
    usdPreviewSurfaceShader.CreateIdAttr("UsdPreviewSurface")
    diffuseColorInput = usdPreviewSurfaceShader.CreateInput("diffuseColor", Sdf.ValueTypeNames.Color3f)
    diffuseColorInput.ConnectToSource(diffuseColorShaderOutput)
    normalInput = usdPreviewSurfaceShader.CreateInput("normal", Sdf.ValueTypeNames.Normal3f)
    normalInput.ConnectToSource(normalShaderOutput)

    # Set the linkage between material and USD Preview surface shader
    usdPreviewSurfaceOutput = newMat.CreateSurfaceOutput()
    usdPreviewSurfaceOutput.ConnectToSource(usdPreviewSurfaceShader, "surface")

    UsdShade.MaterialBindingAPI(mesh).Bind(newMat)

    save_stage(stageUrl, comment='Added material to box.')


# Create a distant light in the scene.
def createDistantLight(stageUrl):
    newLight = UsdLux.DistantLight.Define(g_stage, "/Root/DistantLight")
    newLight.CreateAngleAttr(0.53)
    newLight.CreateColorAttr(Gf.Vec3f(1.0, 1.0, 0.745))
    newLight.CreateIntensityAttr(5000.0)

    save_stage(stageUrl, comment='Created a DistantLight.')


# Create a dome light in the scene.
def createDomeLight(stageUrl, texturePath):
    newLight = UsdLux.DomeLight.Define(g_stage, "/Root/DomeLight")
    newLight.CreateIntensityAttr(1000.0)
    newLight.CreateTextureFileAttr(texturePath)
    newLight.CreateTextureFormatAttr("latlong")

    # Set rotation on domelight
    xForm = newLight
    rotateOp = xForm.AddXformOp(UsdGeom.XformOp.TypeRotateXYZ, UsdGeom.XformOp.PrecisionDouble)
    rotateOp.Set(Gf.Vec3d(270, 0, 0))

    save_stage(stageUrl, comment='Created a DomeLight.')


def createEmptyFolder(emptyFolderPath):
    LOGGER.info("Creating new folder: %s", emptyFolderPath)
    result = omni.client.create_folder(emptyFolderPath)

    LOGGER.info("Finished (this may be an error if the folder already exists) [ %s ]", result.name)


def run_live_edit(prim, stageUrl):
    global g_stage
    angle = 0
    omni.client.live_process()
    prim_path = prim.GetPath()
    LOGGER.info(f"Begin Live Edit on {prim_path} - \nEnter 't' to transform, 'm' to send a channel message, 'l' to leave the channel, or 'q' to quit.\n")

    # Message channel callback responsds to channel events
    def message_channel_callback(result: omni.client.Result, channel_event: omni.client.ChannelEvent, user_id: str, content: omni.client.Content):
        LOGGER.info(f"Channel event: {channel_event}")
        if channel_event == omni.client.ChannelEvent.MESSAGE:
            # Assume that this is an ASCII message from another client
            text_message = memoryview(content).tobytes().decode('ascii')
            LOGGER.info(f"Channel message received: {text_message}")

    # We aren't doing anything in particular when the channel messages are finished sending
    def on_send_message_cb(result):
        pass

    # Join a message channel to communicate text messages between clients
    join_request = omni.client.join_channel_with_callback(stageUrl+".__omni_channel__", message_channel_callback)

    while True:
        option = get_char_util.getChar()

        omni.client.live_process()
        if option == b't':
            angle = (angle + 15) % 360
            radians = angle * 3.1415926 / 180.0
            x = math.sin(radians) * 100.0
            y = math.cos(radians) * 100.0

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
            save_stage(stageUrl)

        elif option == b'm':
            if join_request:
                LOGGER.info("Enter a channel message: ")
                message = input()
                omni.client.send_message_with_callback(join_request.id, message.encode('ascii'), on_send_message_cb)
            else:
                LOGGER.info("The message channel is disconnected.")

        elif option == b'l':
            LOGGER.info("Leaving message channel")
            if join_request:
                join_request.stop()
                join_request = None

        elif option == b'q' or option == chr(27).encode():
            LOGGER.info("Live edit complete")
            break
        else:
            LOGGER.info("Enter 't' to transform, 'm' to send a channel message, 'l' to leave the channel, or 'q' to quit.")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Python Omniverse Client Sample",
                                     formatter_class=argparse.ArgumentDefaultsHelpFormatter)

    parser.add_argument("-l", "--live", action='store_true', default=False)
    parser.add_argument("-p", "--path", action="store", default="")
    parser.add_argument("-v", "--verbose", action='store_true', default=False)
    parser.add_argument("-e", "--existing", action="store")

    args = parser.parse_args()

    existing_stage = args.existing
    live_edit = args.live or bool(existing_stage)
    destination_path = args.path
    logging_enabled = args.verbose

    # Set the hang detection time on synchronous client methods to 10 seconds
    # There's a movement to remove _any_ sync client methods, but we're using
    # them in the run_live_edit function because blocking keyboard methods don't 
    # work well in async functions
    omni.client.set_hang_detection_time_ms(10000)

    startOmniverse(live_edit)

    # if no path specified, determine the username
    if not destination_path:
        user_folder = "omniverse://localhost/Users"
        user_name = logConnectedUsername(user_folder, False)
        destination_path = user_folder + "/" + user_name

    if destination_path and not isValidOmniUrl(destination_path):
        msg = ("This is not an Omniverse Nucleus URL: %s"
                "Correct Omniverse URL format is: omniverse://server_name/Path/To/Example/Folder"
                "Allowing program to continue because file paths may be provided in the form: C:/Path/To/Stage")
        LOGGER.warning(msg, destination_path)

    if existing_stage and not isValidOmniUrl(existing_stage):
        msg = ("This is not an Omniverse Nucleus URL: %s"
                "Correct Omniverse URL format is: omniverse://server_name/Path/To/Example/Folder/helloWorld_py.usd"
                "Allowing program to continue because file paths may be provided in the form: C:/Path/To/Stage/helloWorld_py.usd")
        LOGGER.warning(msg, existing_stage)

    boxMesh = None

    if not existing_stage:
        # Create the USD model in Omniverse
        stageUrl = createOmniverseModel(destination_path, live_edit)

        # Log the username for the server
        logConnectedUsername(stageUrl)

        rootUrl = '/Root'
        UsdGeom.Xform.Define(g_stage, rootUrl)
        # Define the defaultPrim as the /Root prim
        rootPrim = g_stage.GetPrimAtPath(rootUrl)
        g_stage.SetDefaultPrim(rootPrim)

        # Create physics scene
        createPhysicsScene(rootUrl)

        # Create box geometry in the model
        boxMesh = createBox(stageUrl, rootUrl)

        # Create dynamic cube
        createDynamicCube(stageUrl, rootUrl, 100.0)

        # Create quad - static tri mesh collision so that the box collides with it
        createQuad(stageUrl, rootUrl, 500.0)

        # Create a distance and dome light in the scene
        createDistantLight(stageUrl)
        createDomeLight(stageUrl, "./Materials/kloofendal_48d_partly_cloudy.hdr")

        # Upload a material and textures to the Omniverse server
        uploadMaterial(destination_path)

        # Add a material to the box
        createMaterial(boxMesh, stageUrl)

        # Create an empty folder, just as an example
        createEmptyFolder(destination_path + "/EmptyFolder")
    else:
        stageUrl = existing_stage
        LOGGER.debug("Stage url: %s", stageUrl)
        boxMesh = findGeomMesh(existing_stage)

    if not boxMesh:
        sys.exit("[ERROR] Unable to create or find mesh")
    else:
        LOGGER.debug("Mesh created/found successfully")

    if live_edit and boxMesh is not None:
        run_live_edit(boxMesh.GetPrim(), stageUrl)

    shutdownOmniverse()
