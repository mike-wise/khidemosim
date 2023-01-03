using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using MmSledMsg = RosMessageTypes.Rs007Control.MagneMotionSledMsg;
using Unity.Robotics.ROSTCPConnector;
using NetMQ;
using NetMQ.Sockets;

namespace KhiDemo
{

    public enum MmRobotMoveMode { Sudden, Planned }
    public enum MmBoxMode { RealPooled, FakePooled }

    public enum MmSegForm { None, Straight, Curved }

    public enum MmBoxSimMode { Hierarchy, Kinematics, Physics }

    public enum MmTableStyle {  MftDemo, Simple }

    public enum MmMode { None, Echo, Planning, SimuRailToRail, StartRailToTray, StartTrayToRail }
    public enum MmSubMode { None, RailToTray, TrayToRail }

    public enum InfoType {  Info, Warn, Error }

    public enum MmRigidMode {  None, Sleds,SledsBox }

    public enum MmSledMoveMethod { SetPosition, MovePosition };
    public enum MmStartingCoords {  Rot000, Rot180 }; // should get rid of this and only keep Rot000

    public enum MmboxColorMode { Native, Simmode }

    public enum MmTrackSmoothness { Smooth, LittleBumpy, VeryBumpy, SuperBumpy }


    public class MagneMotion : MonoBehaviour
    {
        [Header("Scene Elements")]
        public MmController mmctrl;
        public MmTable mmt;
        public MmRobot mmRobot = null;
        public MmTray mmtray = null;
        public GameObject mmego = null;
        public MmTrajPlan planner = null;
        public GameObject simroot = null;
        public GameObject floorobject = null;
        public GameObject trayobject = null;

        [Header("Scene Element Forms")]
        public MmSled.SledForm sledForm = MmSled.SledForm.Prefab;
        public MmBox.BoxForm boxForm = MmBox.BoxForm.Prefab;
        public MmTableStyle mmTableStyle = MmTableStyle.MftDemo;
        public bool addPathMarkers = false;
        public bool addPathSledsOnStartup = true;
        public bool positionOnFloor = false;
        public bool enclosureOn = true;
        public bool enclosureLoaded = false;
        public bool effectorPoseMarkers = false;

        [Header("Components")]
        public GameObject mmtgo;
        public GameObject mmtctrlgo;

        [Header("Behaviour")]
        public MmBoxSimMode mmBoxSimMode = MmBoxSimMode.Hierarchy;
        MmBoxSimMode __mmBoxSimMode = MmBoxSimMode.Hierarchy;
        public MmRobotMoveMode mmRobotMoveMode = MmRobotMoveMode.Sudden;
        public MmMode mmMode = MmMode.None;
        public MmRigidMode mmRigidMode = MmRigidMode.None;
        public MmSledMoveMethod mmSledMoveMethod = MmSledMoveMethod.SetPosition;
        public bool stopSimulation = false;
        public float initialSleedSpeed = 1.0f;
        public MmStartingCoords mmStartingCoord = MmStartingCoords.Rot000;


        [Header("Physics")]
        public float staticFriction = 0.9f;
        public float dynamicFriction = 0.9f;
        public float bounciness = 0f;
        public MmTrackSmoothness trackSmoothness = MmTrackSmoothness.Smooth;

        [Header("Appearance")]
        public MmboxColorMode boxColorMode;


        [Header("Network ROS")]
        public bool enablePlanning = false;
        public bool echoMovementsRos = true;
        public bool publishMovementsRos = false;
        public float publishInterval = 0.1f;

        public ROSConnection rosconnection = null;
        public bool rosactivated = false;
        public string roshost = "localhost";
        public int rosport = 10005;


        [Header("Network ZMQ")]
        public bool publishMovementsZmq = false;
        public float publishIntervalZmq = 0.1f;
        public bool zmqactivated = false;
        public string zmqhost = "localhost";
        public int zmqport = 10006;

        [Header("Extras")]
        public bool calculatePoses = false;
        public bool publishJsonStatesToFile;
        public string jsonStatesFile = "";

        public PhysicMaterial physMat;

        List<(InfoType intyp, DateTime time, string msg)> messages;

        GameObject planningCanvas;
        GameObject target;
        GameObject targetPlacement;



        public MmBoxSimMode GetBoxSimMode()
        {
            return __mmBoxSimMode;
        }
        public void SetBoxSimMode()
        {
            __mmBoxSimMode = mmBoxSimMode;
        }

        public void PositionObjects()
        {

            trayobject.transform.rotation = Quaternion.Euler(0, 0, 0);
            floorobject.transform.localPosition = new Vector3(0, 0.761f, 0);
            var ptfloor = new Vector3(-0.206f, 0.761f, -0.21f);
            mmRobot.transform.localPosition = ptfloor; // Note that this position must be set manually on the link marked as "immovable" (currently "base_link")
            switch (mmStartingCoord)
            {
                case MmStartingCoords.Rot000:
                    simroot.transform.rotation = Quaternion.Euler(0, 0, 0);
                    trayobject.transform.localPosition = ptfloor + new Vector3(0.374f, 0.222f, 0.03f);
                    break;
                case MmStartingCoords.Rot180:
                    simroot.transform.rotation = Quaternion.Euler(0, 179.999f, 0);
                    trayobject.transform.localPosition = ptfloor + new Vector3(-0.374f, 0.222f, 0.03f);
                    break;
            }

            switch (mmStartingCoord)
            {
                case MmStartingCoords.Rot000:
                    target.transform.localPosition = new Vector3(-0.16f, 0.16f, -0.351f);
                    break;
                case MmStartingCoords.Rot180:
                    target.transform.localPosition = new Vector3(0.16f, 0.16f, 0.351f);
                    break;
            }
            switch (mmStartingCoord)
            {
                case MmStartingCoords.Rot000:
                    targetPlacement.transform.localPosition = new Vector3(0.321f, 0, -0.294f);
                    break;
                case MmStartingCoords.Rot180:
                    targetPlacement.transform.localPosition = new Vector3(-0.321f, 0, 0.294f);
                    break;
            }

        }

        public GameObject FindAndCheck(string foname)
        {
            var go = GameObject.Find(foname);
            if (go == null)
            {
                Debug.LogError($"Could Not Find {foname}");
            }
            return go;
        }

        private void Awake()
        {
            // Messages need to be allocated
            messages = new List<(InfoType intyp, DateTime time, string msg)>();// has to be first

            // Now find objects
            planner = FindObjectOfType<MmTrajPlan>();
            if (planner == null)
            {
                ErrMsg("no planner in scene");
            }

            mmRobot = FindObjectOfType<MmRobot>();
            if (mmRobot == null)
            {
                ErrMsg("no MnRobot in scene");
            }


            simroot = FindAndCheck("Simroot");
            simroot.transform.position = Vector3.zero;
            floorobject = FindAndCheck("FloorObject");
            trayobject = FindAndCheck("MmTray");
            planningCanvas = FindAndCheck("PlanningCanvas");
            target = FindAndCheck("Target");
            targetPlacement = FindAndCheck("TargetPlacement");

            PositionObjects();


            rosconnection = ROSConnection.GetOrCreateInstance();
            rosconnection.ShowHud = false;
            rosconnection.ConnectOnStart = false;

            // UnityUt.AddArgs(new string [] { "--roshost","localhost","BlueTina","--mode","echo" });


            GetNetworkParms();
            GetOtherParms();

            physMat = new PhysicMaterial();
            physMat.staticFriction = staticFriction;
            physMat.dynamicFriction = dynamicFriction;
            physMat.bounciness = bounciness;


            // ZmqSendString("Hello world");
        }


        string HostShortcuts(string oname)
        {
            var rname = oname;
            var loname = oname.ToLower();
            switch(loname)
            {
                case "nl":
                case "we": 
                    rname = "20.234.234.190";
                    break;
                case "us":
                case "usa":
                    rname = "20.225.161.122";
                    break;
                case "tx":
                    rname = "10.100.101.95";
                    break;
                case "sg":
                    rname = "52.187.116.1";
                    break;
                case "fr":
                    rname = "20.111.14.13";
                    break;
                case "de":
                    rname = "20.111.14.13";
                    break;
            }
            return rname;
        }

        public void GetNetworkParms()
        {
            var (ok1, hostros) = UnityUt.ParmString("--roshost");
            if (ok1)
            {
                InfoMsg($"Found roshost {hostros}");
                roshost = HostShortcuts(hostros);
                zmqhost = roshost;
            }
            var (ok2, hostzmq) = UnityUt.ParmString("--zmqhost");
            if (ok2)
            {
                InfoMsg($"Found zmqhost {hostzmq}");
                zmqhost = HostShortcuts(hostzmq);
            }
            var (ok3, portros) = UnityUt.ParmInt("--rosport");
            if (ok3)
            {
                InfoMsg($"Found rosport {portros}");
                rosport = portros;
            }
            var (ok4, portzmq) = UnityUt.ParmInt("--zmqport");
            if (ok4)
            {
                InfoMsg($"Found zmqport {portzmq}");
                zmqport = portzmq;
            }
        }

        public void GetOtherParms()
        {
            var (ok1, modestr) = UnityUt.ParmString("--mode");
            if (ok1)
            {
                InfoMsg($"Found mode {modestr}");

                var mode = modestr.ToLower();
                InfoMsg($"Initial Mode string selector {mode}");
                if (mode == "echo")
                {
                    mmMode = MmMode.Echo;
                }
                else if (mode=="rail2rail")
                {
                    mmMode = MmMode.SimuRailToRail;
                }
                else if (mode == "planning")
                {
                    mmMode = MmMode.Planning;
                }
                else if (mode=="tray2rail")
                {
                    mmMode = MmMode.StartTrayToRail;
                }
                else if (mode == "rail2tray")
                {
                    mmMode = MmMode.StartTrayToRail;
                }
                else
                {
                    mmMode = MmMode.StartTrayToRail;
                }
                InfoMsg($"Initial Mode selector now set to {mmMode}");
            }
        }

        public void CheckNetworkActivation()
        {
            var needros = echoMovementsRos || enablePlanning;
            if (needros && !rosactivated)
            {
                RosSetup();
            }
            if (publishMovementsZmq && !zmqactivated)
            {
                ZmqSetup();
            }
        }

        public void Check1Activation(GameObject cango)
        {
            if (cango == null)
            {
                return;
            }
            cango.SetActive(enablePlanning);
        }

        public void CheckPlanningActivation()
        {
            Check1Activation( planningCanvas );
            Check1Activation( target );
            Check1Activation( targetPlacement );
        }

        public void RosSetup()
        {
            //rosconnection = ROSConnection.GetOrCreateInstance();
            rosconnection.ShowHud = true;
            //rosconnection.InitializeHUD();
            InfoMsg($"Opening ROS connection {roshost}:{rosport}");
            rosconnection.Connect(roshost, rosport);
            rosconnection.ShowHud = true;
            mmRobot.SubcribeToRos();
            mmtray.SubscribeToRos();
            mmt.SubscribeToRos();
            rosactivated = true;
        }

        public void RosTeardown()
        {
            rosconnection.Disconnect();
            rosactivated = false;
        }

        RequestSocket socket;
        public void ZmqSetup()
        {
            InfoMsg($"Opening zmq connection {zmqhost}:{zmqport}");
            AsyncIO.ForceDotNet.Force();
            socket = new RequestSocket();
            socket.Connect($"tcp://{zmqhost}:{zmqport}");
            zmqactivated = true;
        }

        public void ZmqTeardown()
        {
            InfoMsg("Tearing down zmq");
            NetMQConfig.Cleanup(block:false);
            socket = null;
            zmqactivated = false;
        }

        public void ZmqSendString(string str)
        {
            if (!zmqactivated) return;

            var timeout1 = new System.TimeSpan(0, 0, 3);
            socket.TrySendFrame(timeout1,str);
            // Debug.Log($"Zmq sent {str}");
            var timeout2 = new System.TimeSpan(0, 0, 3);
            var ok = socket.TryReceiveFrameString(timeout2, out var response);
            if (!ok)
            {
                Debug.LogWarning($"Zmq received not okay after sending {str} - is a receiver running? - deactivating zmq");
                zmqactivated = false;
            }
        }

        public void ErrMsg(string msg)
        {
            messages.Add((InfoType.Error, DateTime.Now, msg));
            Debug.LogError(msg);
        }

        public void WarnMsg(string msg)
        {
            messages.Add((InfoType.Warn, DateTime.Now, msg));
            Debug.LogWarning(msg);
        }

        public void InfoMsg(string msg)
        {
            messages.Add((InfoType.Info, DateTime.Now, msg));
            Debug.Log(msg);
        }

        // Start is called before the first frame update
        void Start()
        {


            if (floorobject != null)
            {
                var boxcol = floorobject.GetComponent<BoxCollider>();
                boxcol.material = physMat;
                Debug.Log($"Assigned physMat to floorobject");
            }

            var ovctrl = gameObject.AddComponent<OvCtrl>();
            ovctrl.Init("Simroot");
            ovctrl.OV_ActiveObjects = "mmbox,mmsled,mmlink";
            ovctrl.OV_FlattenObjects = "mmbox,mmlink";
            ovctrl.OV_FlattenObjects = "";

            MmBox.AllocateBoxPools(this);

            mmtgo = new GameObject("MmTable");
            mmtgo.transform.SetParent(simroot.transform, worldPositionStays: true);
            mmt = mmtgo.AddComponent<MmTable>();
            mmt.Init(this);


            MmPathSeg.InitDicts();

            switch (mmTableStyle)
            {
                default:
                case MmTableStyle.MftDemo:
                    mmt.MakeMsftDemoMagmo();
                    break;
                case MmTableStyle.Simple:
                    mmt.MakeSimplePath();
                    break;
            }



            var mmgo = mmt.SetupGeometry(addPathMarkers: addPathMarkers, positionOnFloor: positionOnFloor);
            mmgo.transform.SetParent(transform, false);
            mmgo.transform.SetParent(simroot.transform, true);
            if (addPathSledsOnStartup)
            {
                mmt.AddSleds();
            }

            mmtray = FindObjectOfType<MmTray>();
            if (mmtray != null)
            {
                //Debug.Log($"Before Init MmTray.rotation:{mmtray.transform.rotation.eulerAngles:f1}");
                mmtray.Init(this);
                //Debug.Log($"After Init MmTray.rotation:{mmtray.transform.rotation.eulerAngles:f1}");
            }

            // needs ot go last
            mmtctrlgo = new GameObject("MmCtrl");
            mmctrl = mmtctrlgo.AddComponent<MmController>();
            mmctrl.Init(this);
            mmctrl.SetMode(mmMode,clear:false); // first call should not try and clear




            CheckEnclosure();
        }

        MmSled.SledForm oldsledForm;
        void ChangeSledFormIfRequested()
        {
            if (updatecount == 0)
            {
                oldsledForm = sledForm;
            }
            if (sledForm != oldsledForm)
            {
                var sleds = FindObjectsOfType<MmSled>();
                foreach (var sled in sleds)
                {
                    sled.ConstructSledForm(sledForm,addBox:false);
                }
                oldsledForm = sledForm;
            }
        }

        MmBox.BoxForm oldboxForm;
        void ChangeBoxFormIfRequested()
        {
            if (updatecount == 0)
            {
                oldboxForm = boxForm;
            }
            if (boxForm != oldboxForm)
            {
                var boxes = FindObjectsOfType<MmBox>();
                foreach (var box in boxes)
                {
                    box.ConstructForm(boxForm);
                }
                oldboxForm = boxForm;
            }
        }

        int rosReceivedCount = 0;
        int zmqPublishedCount = 0;

        public void IncRosReceivedCount()
        {
            rosReceivedCount++;
        }
        public void IncZmqPublishedCount()
        {
            zmqPublishedCount++;
        }

        float lastPub = 0;
        float lastPubZmq = 0;

        public void PhysicsStep()
        {
            //var fps = 1 / Time.deltaTime;
            //Debug.Log($"MagneMotion Simstep time:{Time.time:f3} deltatime:{Time.deltaTime:f3} fps:{fps:f2}");

            if (!stopSimulation)
            {
                mmctrl.PhysicsStep();
                mmt.PhysicsStep();
            }
            if (publishMovementsRos && Time.time-lastPub>this.publishInterval)
            {
                lastPub = Time.time;
                mmRobot.PublishJoints();
                mmtray.PublishTray();
                mmt.PublishSleds();
            }
            if (publishMovementsZmq && Time.time - lastPubZmq > this.publishIntervalZmq)
            {
                lastPubZmq = Time.time;
                mmRobot.PublishJointsZmq();
                mmtray.PublishTrayZmq();
                mmt.PublishSledsZmq();
            }
        }

        public void Quit()
        {
            Application.Quit();
#if UNITY_EDITOR
            EditorApplication.ExecuteMenuItem("Edit/Play");// this makes the editor quit playing
#endif
        }

        private void OnApplicationQuit()
        {
            RosTeardown();
            ZmqTeardown();
        }

        Dictionary<MmTrackSmoothness, MmTrackSmoothness> BumpyMapPlus = new Dictionary<MmTrackSmoothness, MmTrackSmoothness>()
        { {MmTrackSmoothness.Smooth, MmTrackSmoothness.LittleBumpy },
          {MmTrackSmoothness.LittleBumpy, MmTrackSmoothness.VeryBumpy },
          {MmTrackSmoothness.VeryBumpy, MmTrackSmoothness.SuperBumpy },
          {MmTrackSmoothness.SuperBumpy, MmTrackSmoothness.SuperBumpy },
        };
        Dictionary<MmTrackSmoothness, MmTrackSmoothness> BumpyMapNeg = new Dictionary<MmTrackSmoothness, MmTrackSmoothness>()
        { {MmTrackSmoothness.LittleBumpy, MmTrackSmoothness.Smooth },
          {MmTrackSmoothness.VeryBumpy, MmTrackSmoothness.LittleBumpy },
          {MmTrackSmoothness.SuperBumpy, MmTrackSmoothness.VeryBumpy },
          {MmTrackSmoothness.Smooth, MmTrackSmoothness.Smooth },
        };

        public void AdjustBumpyness(int direction)
        {
            if (direction>0)
            {
                trackSmoothness = BumpyMapPlus[trackSmoothness];
            }
            else
            {
                trackSmoothness = BumpyMapNeg[trackSmoothness];
            }
        }

        float ctrlQhitTime = 0;
        float ctrlVhitTime = 0;
        float ctrlBhitTime = 0;
        float F5hitTime = 0;
        float F6hitTime = 0;
        float F10hitTime = 0;
        //float plusHitTime = 0;
        //float minusHitTime = 0;
        public void KeyProcessing()
        {
            var plushit = Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Equals);
            var minushit = Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus);
            var ctrlhit = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (ctrlhit && Input.GetKeyDown(KeyCode.Q))
            {
                Debug.Log("Hit Ctrl-Q");
                if ((Time.time - ctrlQhitTime) < 1)
                {
                    Debug.Log("Hit it twice so quitting: Application.Quit()");
                    Quit();
                }
                // CTRL + Q - 
                ctrlQhitTime = Time.time;
            }
            if (((Time.time - F5hitTime) > 0.5) && Input.GetKeyDown(KeyCode.F5))
            {
                Debug.Log("F5 - Request Total Refresh");
            }
            if (((Time.time - F6hitTime) > 0.5) && Input.GetKeyDown(KeyCode.F6))
            {
                Debug.Log("F6 - Request Go Refresh");
            }
            if (((Time.time - F10hitTime) > 1) && Input.GetKeyDown(KeyCode.F10))
            {
                Debug.Log("F10 - Options");
                // uiman.optpan.TogglePanelState();
                //this.RequestRefresh("F5 hit", totalrefresh: true);
            }
            if (ctrlhit && Input.GetKeyDown(KeyCode.C))
            {
                Debug.Log("Hit Ctrl-C - interrupting");
                // CTRL + C
            }
            if (ctrlhit && Input.GetKeyDown(KeyCode.D))
            {
                Debug.Log("Hit LCtrl-D");
                stopSimulation = !stopSimulation;
            }
            if (ctrlhit && Input.GetKeyDown(KeyCode.H))
            {
                Debug.Log("Hit LCtrl-H");
                showHelpText  = !showHelpText;
                if (showHelpText)
                {
                    showLogText = false;
                }
            }
            if (ctrlhit && Input.GetKeyDown(KeyCode.X))
            {
                Debug.Log("Zoom text");
                fontsize += 2;
                if (fontsize > 30) fontsize = 30;
                //plusHitTime = Time.time;
            }
            if (ctrlhit && Input.GetKeyDown(KeyCode.I))
            {
                Debug.Log("Rotate Info");
                infoLineMode = nextInfoLineMode[(int) infoLineMode];
            }
            if (ctrlhit && Input.GetKeyDown(KeyCode.Z))
            {
                Debug.Log("Unzoom text");
                fontsize -= 2;
                if (fontsize < 12) fontsize = 12;
                //minusHitTime = Time.time;
            }
            if (ctrlhit && Input.GetKeyDown(KeyCode.G))
            {
                Debug.Log("Hit LCtrl-G");
                showLogText = !showLogText;
                if (showLogText)
                {
                    showHelpText = false;
                }
            }
            if (ctrlhit && Input.GetKeyDown(KeyCode.E))
            {
                Debug.Log("Hit LCtrl-E");
                mmctrl.SetModeWhenIdle(MmMode.Echo,clear:true);
            }
            if (ctrlhit && Input.GetKeyDown(KeyCode.T))
            {
                Debug.Log("Hit LCtrl-T");
                var traycount = mmtray.CountLoaded();
                var newmode = MmMode.StartRailToTray;
                if (traycount > 0)
                {
                    newmode = MmMode.StartTrayToRail;
                }
                mmctrl.SetModeWhenIdle(newmode);
            }
            if (ctrlhit && Input.GetKeyDown(KeyCode.B))
            {
                ctrlBhitTime = Time.time;
            }
            if (((Time.time - ctrlBhitTime) < 1) && Input.GetKeyDown(KeyCode.P))
            {
                // make more bumpy
                AdjustBumpyness(+1);
            }
            if (((Time.time - ctrlBhitTime) < 1) && Input.GetKeyDown(KeyCode.N))
            {
                // make less bumpy
                AdjustBumpyness(-1);
            }
            if (ctrlhit && Input.GetKeyDown(KeyCode.V))
            {
                ctrlVhitTime = Time.time;
            }
            if (((Time.time - ctrlVhitTime) < 1) && Input.GetKeyDown(KeyCode.S))
            {
                var pos = new Vector3(0, 3, 0);
                var rot = new Vector3(90, 0, 90);
                Camera.main.transform.position = pos;
                Camera.main.transform.rotation = Quaternion.Euler(rot);
            }
            if (((Time.time - ctrlVhitTime) < 1) && Input.GetKeyDown(KeyCode.T))
            {
                var pos = new Vector3(0, 2.4f, 0);
                var rot = new Vector3(90, -90, 90);
                Camera.main.transform.position = pos;
                Camera.main.transform.rotation = Quaternion.Euler(rot);
            }
            if (((Time.time - ctrlVhitTime) < 1) && Input.GetKeyDown(KeyCode.F))
            {
                var pos = new Vector3(0, 1.4f, -0.7f);
                var rot = new Vector3(45, 0, 0);
                Camera.main.transform.position = pos;
                Camera.main.transform.rotation = Quaternion.Euler(rot);
            }
            if (((Time.time - ctrlVhitTime) < 1) && Input.GetKeyDown(KeyCode.B))
            {
                var pos = new Vector3(0, 1.8f, 1.3f);
                var rot = new Vector3(45, 180, 0);
                Camera.main.transform.position = pos;
                Camera.main.transform.rotation = Quaternion.Euler(rot);
            }
            if (((Time.time - ctrlVhitTime) < 1) && Input.GetKeyDown(KeyCode.L))
            {
                var pos = new Vector3(-1.4f, 1.8f, 0f);
                var rot = new Vector3(45, 90, 0);
                Camera.main.transform.position = pos;
                Camera.main.transform.rotation = Quaternion.Euler(rot);
            }
            if (((Time.time - ctrlVhitTime) < 1) && Input.GetKeyDown(KeyCode.R))
            {
                var pos = new Vector3(1.4f, 1.8f, 0f);
                var rot = new Vector3(45, 270, 0);
                Camera.main.transform.position = pos;
                Camera.main.transform.rotation = Quaternion.Euler(rot);
            }
            if (((Time.time - ctrlVhitTime) < 1) && plushit)
            {
                Debug.Log("Move Back");
                var newpos = Camera.main.transform.position * 1.414f;
                Camera.main.transform.position = newpos;
            }
            if (((Time.time - ctrlVhitTime) < 1) && minushit)
            {
                Debug.Log("Move Up");
                var newpos = Camera.main.transform.position/1.414f;
                Camera.main.transform.position = newpos;
            }
            if (ctrlhit && Input.GetKeyDown(KeyCode.L))
            {
                Debug.Log("Hit LCtrl-L");
                mmctrl.SetModeWhenIdle(MmMode.SimuRailToRail);
                //mmctrl.SetMode(MmMode.SimuRailToRail, clear: true);
            }
            if (ctrlhit && Input.GetKeyDown(KeyCode.R))
            {
                Debug.Log("Hit LCtrl-R");
                mmctrl.DoReverseTrayRail();
            }
            if (ctrlhit && Input.GetKeyDown(KeyCode.N))
            {
                Debug.Log("Hit LCtrl-N");
                ToggleEnclosure();
            }
            if (ctrlhit && Input.GetKeyDown(KeyCode.P))
            {
                Debug.Log("Hit LCtrl-P");
                mmctrl.SetModeWhenIdle(MmMode.Planning, clear: true);
            }
            if (ctrlhit && Input.GetKeyDown(KeyCode.F))
            {
                Debug.Log("Hit LCtrl-F");
                initialSleedSpeed *= 2f;
                mmt.AdjustSledSpeedFactor(2);
                mmctrl.AdjustRobotSpeedFactor(2);
            }
            if (ctrlhit && Input.GetKeyDown(KeyCode.S))
            {
                Debug.Log("Hit LCtrl-S");
                initialSleedSpeed *= 0.5f;
                mmt.AdjustSledSpeedFactor(0.5f);
                mmctrl.AdjustRobotSpeedFactor(0.5f);
            }
        }

        public void ToggleEnclosure()
        {
            enclosureOn = !enclosureOn;
            CheckEnclosure();
        }

        public void CheckEnclosure()
        {

            if (!enclosureLoaded)
            {
                var prefab = Resources.Load<GameObject>("Enclosure/Models/RS007_Enclosure");
                mmego = Instantiate<GameObject>(prefab);
                mmego.name = $"Enclosure";
                mmego.transform.parent = this.mmtgo.transform;
                mmego.transform.position = new Vector3(0.0f, 0.0f, -0);
                mmego.transform.localRotation = Quaternion.Euler(0, 0, -0);
                // Add enclosure floor
                var encfloor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                encfloor.name = "EnclosureFloor";
                encfloor.transform.position = new Vector3(0, -0.04f, 0);
                encfloor.transform.localScale = new Vector3(1, 1, 1);
                encfloor.transform.SetParent(mmego.transform, worldPositionStays: false);
                enclosureLoaded = true;
                var material = encfloor.GetComponent<Renderer>().material;
                material.color = UnityUt.GetColorByName("steelblue");
            }
            if (mmego != null)
            {
                if (floorobject != null)
                {
                    if (enclosureOn)
                    {
                        floorobject.transform.localScale = new Vector3(0.02f, 1, 0.02f);
                    }
                    else
                    {
                        floorobject.transform.localScale = new Vector3(1, 1, 1);
                    }
                }
                mmt.pathgos.SetActive(!enclosureOn);// turn off the cigars-noses
                mmego.SetActive(enclosureOn);
            }
        }


        int fupdatecount = 0;
        // Update is called once per frame
        void FixedUpdate()
        {
            PhysicsStep();
            fupdatecount++;
        }

        void StartCalculatingPoses()
        {
            StartCoroutine(mmRobot.DefineEffectorPoses());
        }


        int updatecount = 0;
        // Update is called once per frame
        void Update()
        {
            KeyProcessing();
            ChangeSledFormIfRequested();
            // Can't do this anymore as it messues up our markerbox non-marker box logic
            // ChangeBoxFormIfRequested(); 
            if (calculatePoses)
            {
                StartCalculatingPoses();
                calculatePoses = false;
            }
            //ChangeModeIfRequested();
            updatecount++;
        }

        bool showHelpText=false;
        bool showLogText = false;

        string[] helptext =
        {
            "Ctrl-E Echo Mode",
            "Ctrl-P Planning Mode",
            "Ctrl-L RailToRail Mode",
            "Ctrl-T TrayToRail Mode",
            "Ctrl-R Reverse TrayRail",
            "",
            "Ctrl-F Speed up",
            "Ctrl-S Slow down",
            "Ctrl-B Ctrl-P More Bumpy",
            "Ctrl-B Ctrl-M Less Bumpy",
            "",
            "Ctrl-N Toggle Enclosure",
            "Ctrl-D Toggle Stop Simulation",
            "Ctrl-G Toggle Log Screen",
            "",
            "Ctrl-V Ctrl-F View from Front",
            "Ctrl-V Ctrl-B View from Back",
            "Ctrl-V Ctrl-T View from Top",
            "Ctrl-V Ctrl-S View from Top (rotated)",
            "Ctrl-V Ctrl-R View from Right",
            "Ctrl-V Ctrl-L View from Left",
            "Ctrl-V Ctrl-+ (Plus or Equal) Move out",
            "Ctrl-V Ctrl-- (Minus) Move in",
            "",
            "",
            "Ctrl-+ (plus) Increase text size",
            "Ctrl-- (minus) Decrease text size",
            "",
            "Ctrl-H Toggle Help Screen",
            "Ctrl-I Rotate Info Type",
            "Ctrl-Q Ctrl-Q Quit Application"
        };

        string[] parmtext =
        {
            "--roshost localhost",
            "--rosport 10004",
            "--zmqhost localhost",
            "--zmqport 10006",
            "--mode echo",
            "--mode rail2rail",
            "--mode rail2rail",
            "--mode tray2rail",
            "--mode rail2tray",
        };


        enum InfoLineMode {  None, Network, RobotPose, EffectorPose }
        List<InfoLineMode> nextInfoLineMode = new List<InfoLineMode>() { InfoLineMode.Network, InfoLineMode.RobotPose, InfoLineMode.EffectorPose, InfoLineMode.None };
        InfoLineMode infoLineMode = InfoLineMode.Network;

        string ang(float f)
        {
            var rv = f.ToString("f1");
            return rv;
        }

        string GetInfoLine()
        {
            switch (infoLineMode)
            {
                default:
                case InfoLineMode.Network:
                    var ntxt = $"zmq:{ this.zmqPublishedCount}  ros: { this.rosReceivedCount} speed:{initialSleedSpeed:f1} {trackSmoothness}";
                    return ntxt;
                case InfoLineMode.RobotPose:
                    var j = mmRobot.GetRobotPos();
                    var jtxt = "joints - ";
                    for (int i=0; i<6; i++)
                    {
                        jtxt += ang(j[i])+" ";
                    }
                    return jtxt;
                case InfoLineMode.EffectorPose:
                    var (ok,p,q) = mmRobot.GetEffectorPose();
                    var eftxt = "effector - ";
                    var ptxt = p.ToString("f3");
                    var qtxt = q.ToString("f3");
                    if (ok)
                    {
                        eftxt += $" p:{ptxt} q:{qtxt}";
                    }
                    else
                    {
                        eftxt += " not set";
                    }
                    return eftxt;
                case InfoLineMode.None:
                    return "";
            }
        }

        static int fontsize = 16;
        public void DoHelpScreen()
        {
            GUIStyle textstyle = GUI.skin.GetStyle("Label");
            textstyle.alignment = TextAnchor.UpperLeft;
            textstyle.fontSize = fontsize;
            textstyle.clipping = TextClipping.Overflow;
            textstyle.fontStyle = FontStyle.Bold;
            textstyle.normal.textColor = UnityUt.GetColorByName("indigo");

            var w = 600;
            var h = 20;
            var dy = textstyle.fontSize*1.1f;
            var x1 = Screen.width / 2 - 270;
            var x2 = Screen.width / 2 - 250;
            var y = 10f;

            if (showHelpText)
            {
                GUI.Label(new Rect(x1, y, w, h), "Help Text", textstyle);
                y += dy;
                foreach (var txt in helptext)
                {
                    GUI.Label(new Rect(x2, y, w, h), txt, textstyle);
                    y += dy;
                }
                y += dy;
                GUI.Label(new Rect(x1, y, w, h), "Parameter Text", textstyle);
                y += dy;
                foreach (var txt in parmtext)
                {
                    GUI.Label(new Rect(x2, y, w, h), txt, textstyle);
                    y += dy;
                }
            }
            else
            {
                var itxt = GetInfoLine();
                var msg = $"Ctrl-H For Help";
                if (itxt!="")
                {
                    msg += $" - {itxt}";
                }
                GUI.Label(new Rect(x1, y, w, h), msg, textstyle);
            }

        }



        Dictionary<InfoType, GUIStyle> styleTable = null;

        void InitStyleTable()
        {
            styleTable = new Dictionary<InfoType, GUIStyle>();

            GUIStyle infostyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            infostyle.alignment = TextAnchor.UpperLeft;
            infostyle.fontSize = 14;
            infostyle.normal.textColor = UnityUt.GetColorByName("darkgreen");

            GUIStyle warnstyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            warnstyle.alignment = TextAnchor.UpperLeft;
            warnstyle.fontSize = 14;
            warnstyle.normal.textColor = UnityUt.GetColorByName("orange");

            GUIStyle errstyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            errstyle.alignment = TextAnchor.UpperLeft;
            errstyle.fontSize = 14;
            errstyle.normal.textColor = UnityUt.GetColorByName("red");


            styleTable[InfoType.Info] = infostyle;
            styleTable[InfoType.Warn] = warnstyle;
            styleTable[InfoType.Error] = errstyle;
        }


        public void DoLogtext()
        {
            if (styleTable == null)
            {
                InitStyleTable();
            }
            if (showLogText)
            {
                var w = 400;
                var h = 20;
                var dy = 20;
                var x1 = Screen.width / 2 - 220;
                var x2 = Screen.width / 2 - 200;
                var y = 10 + dy;


                var maxlog = 15;
                var nlog = Mathf.Min(maxlog, messages.Count);


                for (int i = 0; i < nlog; i++)
                {
                    var idx = messages.Count - i - 1;
                    var msg = messages[idx];
                    var textstyle = styleTable[msg.intyp];
                    var txtime = msg.time.ToString("HH:mm:ss");
                    var txt = $"[{txtime}] {msg.msg}";
                    GUI.Label(new Rect(x1, y, w, h), txt, textstyle);
                    y += dy;
                }
            }
        }

        public void OnGUI()
        {
            DoHelpScreen();
            DoLogtext();
        }

    }
}