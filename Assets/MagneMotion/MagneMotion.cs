using System;
using System.Collections.Generic;
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
    public enum MmBoxMode { RealPooled, FakePooled }

    public enum MmSegForm { None, Straight, Curved }

    public enum MmTableStyle {  MftDemo, Simple }

    public enum MmMode { None, Echo, Planning, SimuRailToRail, StartRailToTray, StartTrayToRail }
    public enum MmSubMode { None, RailToTray, TrayToRail }

    public enum InfoType {  Info, Warn, Error }

    public class MagneMotion : MonoBehaviour
    {
        [Header("Scene Elements")]
        public MmController mmctrl;
        public MmTable mmt;
        public MmRobot mmRobot = null;
        public MmTray mmtray = null;
        public GameObject mmego = null;
        public MmTrajPlan planner = null;

        [Header("Scene Element Forms")]
        public MmSled.SledForm sledForm = MmSled.SledForm.Prefab;
        public MmBox.BoxForm boxForm = MmBox.BoxForm.Prefab;
        public MmTableStyle mmTableStyle = MmTableStyle.MftDemo;
        public bool addPathMarkers = false;
        public bool addPathSledsOnStartup = true;
        public bool positionOnFloor = false;
        public bool enclosureOn = false;
        public bool enclosureLoaded = false;

        [Header("Components")]
        public GameObject mmtgo;
        public GameObject mmtctrlgo;

        [Header("Behaviour")]
        public MmMode mmMode = MmMode.None;
        public bool stopSimulation = false;

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

        List<(InfoType intyp, DateTime time, string msg)> messages;

        GameObject planningCanvas;
        GameObject target;
        GameObject targetPlacement;


        GameObject InitGo(string seekname)
        {
            //Debug.Log($"Seeking Name {seekname}");
            var cango = GameObject.Find(seekname);
            if (cango == null)
            {
                ErrMsg($"MagneMotion.InitGo - Can't find object named \"{seekname}\"");
                return null;
            }
            return cango;
        }

        private void Awake()
        {
            // Messages need to be alocated
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

            planningCanvas = InitGo("PlanningCanvas");
            target = InitGo("Target");
            targetPlacement = InitGo("TargetPlacement");

            rosconnection = ROSConnection.GetOrCreateInstance();
            rosconnection.ShowHud = false;
            rosconnection.ConnectOnStart = false;

            // UnityUt.AddArgs(new string [] { "--roshost","localhost","BlueTina","--mode","echo" });


            GetNetworkParms();
            GetOtherParms();
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
            rosconnection.InitializeHUD();
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
                Debug.LogError($"Zmq received not okay after sending {str} - deactivating zmq");
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

            mmtgo = new GameObject("MmTable");
            mmt = mmtgo.AddComponent<MmTable>();
            mmt.Init(this);

            MmBox.AllocatePools(this);

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

            // Initialize Robot


            var mmgo = mmt.SetupGeometry(addPathMarkers: addPathMarkers, positionOnFloor: positionOnFloor);
            mmgo.transform.SetParent(transform, false);
            if (addPathSledsOnStartup)
            {
                mmt.AddSleds();
            }

            mmtray = FindObjectOfType<MmTray>();
            if (mmtray != null)
            {
                mmtray.Init(this);
            }

            // needs ot go last
            mmtctrlgo = new GameObject("MmCtrl");
            mmctrl = mmtctrlgo.AddComponent<MmController>();
            mmctrl.Init(this);
            mmctrl.SetMode(mmMode,clear:false); // first call should not try and clear
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

        float ctrlQhitTime = 0;
        float ctrlVhitTime = 0;
        float F5hitTime = 0;
        float F6hitTime = 0;
        float F10hitTime = 0;
        float plusHitTime = 0;
        float minusHitTime = 0;
        public void KeyProcessing()
        {
            var plushit = Input.GetKey(KeyCode.Plus) || Input.GetKey(KeyCode.KeypadPlus);
            var minushit = Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus);
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
            if (ctrlhit && plushit && Time.time - plusHitTime > 0.1f)
            {
                Debug.Log("Hit LCtrl-Plus");
                fontsize += 2;
                if (fontsize > 30) fontsize = 30;
                plusHitTime = Time.time;
            }
            if (ctrlhit && minushit && Time.time - minusHitTime > 0.1f)
            {
                Debug.Log("Hit LCtrl-Minus");
                fontsize -= 2;
                if (fontsize < 12) fontsize = 12;
                minusHitTime = Time.time;
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
                mmt.AdjustSledSpeedFactor(2);
                mmctrl.AdjustRobotSpeedFactor(2);
            }
            if (ctrlhit && Input.GetKeyDown(KeyCode.S))
            {
                Debug.Log("Hit LCtrl-S");
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
                mmt.pathgos.SetActive(!enclosureOn);
                mmego.SetActive(enclosureOn);
                var fgo = GameObject.Find("FloorObject");
                if (fgo != null)
                {
                    if (enclosureOn)
                    {
                        fgo.transform.localScale = new Vector3(0.02f, 1, 0.02f);
                    }
                    else
                    {
                        fgo.transform.localScale = new Vector3(1, 1, 1);
                    }
                }
            }
        }


        int fupdatecount = 0;
        // Update is called once per frame
        void FixedUpdate()
        {
            PhysicsStep();
            fupdatecount++;
        }

        int updatecount = 0;
        // Update is called once per frame
        void Update()
        {
            KeyProcessing();
            ChangeSledFormIfRequested();
            ChangeBoxFormIfRequested();
            //ChangeModeIfRequested();
            updatecount++;
        }

        bool showHelpText=false;
        bool showLogText = false;

        string[] helptext =
        {
            "Ctrl-E Echo Mode",
            "Ctrl-P RailToRail Mode",
            "Ctrl-L RailToRail Mode",
            "Ctrl-T TrayToRail Mode",
            "Ctrl-R Reverse TrayRail",
            "",
            "Ctrl-F Speed up",
            "Ctrl-S Slow down",
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
            "",
            "",
            "Ctrl-+ (plus) Increase text size",
            "Ctrl-- (minus) Decrease text size",
            "",
            "Ctrl-H Toggle Help Screen",
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

        static int fontsize = 16;
        public void DoHelpScreen()
        {
            GUIStyle textstyle = GUI.skin.GetStyle("Label");
            textstyle.alignment = TextAnchor.UpperLeft;
            textstyle.fontSize = fontsize;
            textstyle.clipping = TextClipping.Overflow;
            textstyle.fontStyle = FontStyle.Bold;
            textstyle.normal.textColor = UnityUt.GetColorByName("indigo");

            var w = 400;
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
                GUI.Label(new Rect(x1, y, w, h), "Ctrl-H For Help", textstyle);
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