using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RsJ1Msg = RosMessageTypes.Rs007Control.Rs007Joints1Msg;
using RsJ6Msg = RosMessageTypes.Rs007Control.Rs007Joints6Msg;
using Unity.Robotics.ROSTCPConnector;
using Newtonsoft.Json;
using System.IO;

namespace KhiDemo
{

    public enum RobotCmdE { MoveTo }
    public class RobotSingleCommand
    {
        public MagneMotion magmo;
        public RobotCmdE cmd;
        public float duration;
        public float[] targetPos;

        public RobotSingleCommand(MagneMotion magmo,RobotCmdE cmd, float duration, float [] targetPos )
        {
            this.magmo = magmo;
            this.cmd = cmd;
            this.duration = duration;
            this.targetPos = targetPos;
        }
    }
    public class RobotCommand
    {
        public MagneMotion magmo;
        MmRobot robot;
        List<RobotSingleCommand> commands;
        public float duration;
        public float[] startPos;
        public RobotCommand(MagneMotion magmo,MmRobot robot)
        {
            this.magmo = magmo;
            this.robot = robot;
            commands = new List<RobotSingleCommand>();
            duration = 0;
            startPos = robot.GetRobotPos();
        }
        public void Add(RobotSingleCommand cmd)
        {
            commands.Add(cmd);
        }
    }

    public enum RobotJointPose { 
        zero, deg10, rest, restr2r, key, ecartup,ecartdn, fcartup, fcartdn, 
        key00up, key00dn, key01up, key01dn, key02up, key02dn, key03up, key03dn,
        key10up, key10dn, key11up, key11dn, key12up, key12dn, key13up, key13dn,
        key20up, key20dn, key21up, key21dn, key22up, key22dn, key23up, key23dn,
    }

    public class MmRobot : MonoBehaviour
    {
        [Header("Controls")]
        public bool enableUrdfInertialMatrix = false;
        public bool showEffectorPositionAtStartup = false;

        [Header("State")]
        public bool loadState;
        public Vector3 lastboxposition;

        [Header("Tracker")]
        public string trackingTargetName;

        [Header("Internals")]
        public MagneMotion magmo;
        public Transform vgriptrans;
        public Transform tooltrans;
        public MmBox box;
        public Transform trackingTarget = null;
        public Vector3 lastTrackingTargetPos;
        public Quaternion lastTrackingTargetRot;

        public RobotJointPose currentRobotPose;

        public MmEffector effector;

        public List<string> linknames;
        public List<Transform> xforms;
        public List<Matrix4x4> locToWorldMat;

        void Start()
        {
            magmo = FindObjectOfType<MagneMotion>();
            if (magmo==null)
            {
                magmo.ErrMsg($"MmRobot counld not find object of type MagneMotion");
            }
            var vacGripperName = "world/base_link/link1/link2/link3/link4/link5/link6/tool_link/gripper_base/Visuals/unnamed/RS007_Gripper_C_u";
            var tooltransName = "world/base_link/link1/link2/link3/link4/link5/link6/tool_link";
            vgriptrans = transform.Find(vacGripperName);
            tooltrans = transform.Find(tooltransName);
            InitializePoses();
            //DefineEffectorPoses(); // can't do this until later

            //magmo.rosconnection.RegisterPublisher<RsJ6Msg>("Rs007Joints6");

            if (showEffectorPositionAtStartup)
            {
                var gob = new GameObject("Effector");
                effector = gob.AddComponent<MmEffector>();
                effector.Init(this);
            }

            var mmTrajPlan = FindObjectOfType<MmTrajPlan>();
            (linknames, xforms) = mmTrajPlan.GetLinkNamesAndXforms();
            locToWorldMat = new List<Matrix4x4>();
            foreach(var xf in xforms)
            {
                locToWorldMat.Add(xf.localToWorldMatrix);
            }

            var linknamesextended = new List<string>(linknames);
            linknamesextended.Add("world/base_link/link1/link2/link3/link4/link5/link6/tool_link");
            linknamesextended.Add("world/base_link/link1/link2/link3/link4/link5/link6/tool_link/gripper_base");
            foreach (var linkname in linknamesextended)
            {
                var go = GameObject.Find(linkname);
                if (go != null)
                {
                    var ovc = go.AddComponent<OvPrim>();
                    ovc.Init("MmLink");
                }
                else
                {
                    Debug.LogError($"Cound not find {linkname}");
                }
            }

            if (trackingTargetName!="")
            {
                AssignTrackingTarget(trackingTargetName);
            }
        }

        public void AssignTrackingTarget(string trackname)
        {
            var go = GameObject.Find(trackname);
            if (go != null)
            {
                trackingTarget = go.transform;
                lastTrackingTargetPos = trackingTarget.position;
                lastTrackingTargetRot = trackingTarget.rotation;
            }
        }

        public void SubcribeToRos()
        {
            magmo.rosconnection.Subscribe<RsJ1Msg>("Rs007Joints1", Rs007J1Change);
            magmo.rosconnection.Subscribe<RsJ6Msg>("Rs007Joints6", Rs007J6Change);
        }

        public void Clear()
        {
            if (box != null)
            {
                MmBox.ReturnToPool(box);
                box = null;
                lastboxposition = Vector3.zero;
            }
            loadState = false;
        }


        public void PublishJoints()
        {
            if (magmo.publishMovementsRos)
            {
                var ang = GetRobotPosDouble();
                // var j6msg = new RsJ6Msg(ang);
                //magmo.rosconnection.Publish("Rs007Joints6", j6msg);
            }
        }

        public void PublishJointsZmq()
        {
            if (magmo.publishMovementsZmq && magmo.zmqactivated)
            {
                magmo.IncZmqPublishedCount();
                var a = GetRobotPosDouble();
                var msg = $"j6|{a[0]:f1}, {a[1]:f1}, {a[2]:f1}, {a[3]:f1}, {a[4]:f1}, {a[5]:f1}";
                magmo.ZmqSendString(msg);
            }
        }

        void Rs007J1Change(RsJ1Msg j1msg)
        {
            magmo.IncRosReceivedCount();
            if (magmo.echoMovementsRos)
            {
                //Debug.Log($"RsJ1Msg:{j1msg.ToString()}");
                var idx = j1msg.idx;
                var joint = (float)j1msg.joint;
                var planner = magmo.planner;

                planner.PositionJoint(idx, joint);
            }
        }


        public bool IsLoaded()
        {
            return loadState;
        }

        void Rs007J6Change(RsJ6Msg j6msg)
        {
            magmo.IncRosReceivedCount();
            if (magmo.echoMovementsRos)
            {
                //Debug.Log($"RsJ6Msg:{j6msg.ToString()}");
                var planner = magmo.planner;
                for (int i = 0; i < 6; i++)
                {
                    planner.PositionJoint(i, (float)j6msg.joints[i]);
                }
            }
        }

        Dictionary<RobotJointPose, float []> jointPoses;
        Dictionary<(int,int),(RobotJointPose,RobotJointPose)> TrayUpDownJointPoses;
        Dictionary<RobotJointPose, (Vector3, Quaternion)> effPoses;

        public void DefineJointPose(RobotJointPose pose,(double a1, double a2, double a3, double a4, double a5, double a6) ptd)
        {
            //  Note we convert from doubles to floats here to make life easier
            var poseTuple = new float [6] { (float)ptd.a1, (float)ptd.a2, (float)ptd.a3, (float)ptd.a4, (float)ptd.a5, (float)ptd.a6 };
            jointPoses[pose] = poseTuple;
        }
        public void SetUpDownPoseForTrayPos((int,int) key,(RobotJointPose,RobotJointPose) poses)
        {
            TrayUpDownJointPoses[key] = poses;
        }
        public (RobotJointPose rpup,RobotJointPose rpdn) GetTrayUpAndDownPoses((int,int) TrayRowColPos)
        {
            return TrayUpDownJointPoses[TrayRowColPos];
        }

        public float[] InterpolatePoses(RobotJointPose p1, RobotJointPose p2,float lamb)
        {
            var a1 = jointPoses[p1];
            var a2 = jointPoses[p1];
            var res = new List<float>();
            for (int i=0; i<a1.Length; i++)
            {
                var val = lamb*(a2[i]-a1[i]) + a1[i];
                res.Add(val);
            }
            return res.ToArray();
        }
        public void InitializePoses()
        {
            jointPoses = new Dictionary<RobotJointPose, float[]>();
            DefineJointPose(RobotJointPose.zero, (0, 0, 0, 0, 0, 0));
            DefineJointPose(RobotJointPose.deg10, (10, 10, 10, 10, 10, 10));
            DefineJointPose(RobotJointPose.rest, (-16.805, 16.073, -100.892, 0, -63.021, 106.779));

            
            DefineJointPose(RobotJointPose.fcartup, (-26.76, 32.812, -74.172, 0, -73.061, 26.755));
            DefineJointPose(RobotJointPose.fcartdn, (-26.749, 40.511, -80.809, 0, -58.682, 26.75));

            DefineJointPose(RobotJointPose.ecartup, (-50.35, 49.744, -46.295, 0, -83.959, 50.347));
            DefineJointPose(RobotJointPose.ecartdn, (-50.351, 55.206, -54.692, 0, -70.107, 50.352));

            DefineJointPose(RobotJointPose.key00up, (-14.864, 26.011, -87.161, 0, -66.826, 102.537));
            DefineJointPose(RobotJointPose.key00dn, (-14.48, 28.642, -89.821, 0, -61.519, 104.49));
            DefineJointPose(RobotJointPose.key01up, (-16.88, 16.142, -101.146, 0, -62.727, 106.877));
            DefineJointPose(RobotJointPose.key01dn, (-16.808, 19.303, -103.537, 0, -57.146, 106.813));
            DefineJointPose(RobotJointPose.key02up, (-20.924, 3.754, -115.647, -0.001, -60.607, 110.92));
            DefineJointPose(RobotJointPose.key02dn, (-20.921, 7.105, -118.181, -0.001, -54.702, 110.919));
            DefineJointPose(RobotJointPose.key03up, (-25.945, -6.875, -125.815, -0.001, -61.063, 115.944));
            DefineJointPose(RobotJointPose.key03dn, (-25.942, -1.839, -129.447, -0.001, -52.394, 115.943));

            DefineJointPose(RobotJointPose.key10up, (-3.833, 24.123, -90.829, 0, -65.057, 93.834));
            DefineJointPose(RobotJointPose.key10dn, (-3.839, 27.028, -93.268, 0, -59.685, 93.835));
            DefineJointPose(RobotJointPose.key11up, (-4.485, 16.308, -106.377, 0, -57.305, 94.482));
            DefineJointPose(RobotJointPose.key11dn, (-4.487, 17.444, -107.108, 0, -55.446, 94.486));
            DefineJointPose(RobotJointPose.key12up, (-5.674, 2.826, -121.133, 0, -56.032, 95.67));
            DefineJointPose(RobotJointPose.key12dn, (-5.677, 4.939, -122.452, 0, -52.61, 95.675));
            DefineJointPose(RobotJointPose.key13up, (-7.204, -11.615, -129.863, 0, -61.795, 97.205));
            DefineJointPose(RobotJointPose.key13dn, (-7.207, -7.853, -132.776, 0, -55.074, 97.205));

            DefineJointPose(RobotJointPose.key20up, (7.072, 24.158, -89.905, 0, -65.933, 82.92));
            DefineJointPose(RobotJointPose.key20dn, (7.07, 27.478, -92.784, 0, -59.743, 82.929));
            DefineJointPose(RobotJointPose.key21up, (8.251, 16.929, -105.936, 0, -57.122, 81.753));
            DefineJointPose(RobotJointPose.key21dn, (8.249, 17.868, -106.538, 0, -55.594, 81.752));
            DefineJointPose(RobotJointPose.key22up, (8.251, 16.929, -105.936, 0, -57.122, 81.753));
            DefineJointPose(RobotJointPose.key22dn, (8.249, 17.868, -106.538, 0, -55.594, 81.752));
            DefineJointPose(RobotJointPose.key23up, (-7.204, -11.615, -129.863, 0, -61.795, 97.205));
            DefineJointPose(RobotJointPose.key23dn, (-7.207, -7.853, -132.776, 0, -55.074, 97.205));

            //p1(RobotPose.restr2r, (-21.7, 55.862, -39.473, -0.008, -84.678, 13.565));
            var a = InterpolatePoses(RobotJointPose.fcartup, RobotJointPose.ecartup, 0.5f);
            DefineJointPose(RobotJointPose.restr2r, (a[0], a[1], a[2], a[3], a[4], a[5]));


            LoadPoses();

            TrayUpDownJointPoses = new Dictionary<(int, int), (RobotJointPose,RobotJointPose)>();
            SetUpDownPoseForTrayPos((0, 0), (RobotJointPose.key00up, RobotJointPose.key00dn));
            SetUpDownPoseForTrayPos((0, 1), (RobotJointPose.key01up, RobotJointPose.key01dn));
            SetUpDownPoseForTrayPos((0, 2), (RobotJointPose.key02up, RobotJointPose.key02dn));
            SetUpDownPoseForTrayPos((0, 3), (RobotJointPose.key03up, RobotJointPose.key03dn));

            SetUpDownPoseForTrayPos((1, 0), (RobotJointPose.key10up, RobotJointPose.key10dn));
            SetUpDownPoseForTrayPos((1, 1), (RobotJointPose.key11up, RobotJointPose.key11dn));
            SetUpDownPoseForTrayPos((1, 2), (RobotJointPose.key12up, RobotJointPose.key12dn));
            SetUpDownPoseForTrayPos((1, 3), (RobotJointPose.key13up, RobotJointPose.key13dn));

            SetUpDownPoseForTrayPos((2, 0), (RobotJointPose.key20up, RobotJointPose.key20dn));
            SetUpDownPoseForTrayPos((2, 1), (RobotJointPose.key21up, RobotJointPose.key21dn));
            SetUpDownPoseForTrayPos((2, 2), (RobotJointPose.key22up, RobotJointPose.key22dn));
            SetUpDownPoseForTrayPos((2, 3), (RobotJointPose.key23up, RobotJointPose.key23dn));
        }

        public Vector3 TransformToRobotCoordinates(Vector3 pt)
        {
            var rot = Quaternion.Inverse(transform.rotation);
            var pivotpt = transform.position;
            var tpt = rot * (pt - pivotpt);

            // Debug.Log($"TransformToRobotCoordinates rot:{rot} pivotpt:{pivotpt:f3} mapped:{pt:f3} to {tpt:f3}");

            return tpt;
        }

        public void LoadPoses()
        {
            var jsonstr1 = Resources.Load<TextAsset>("JsonInitializers/JointPoses");
            jointPoses = JsonConvert.DeserializeObject<Dictionary<RobotJointPose, float[]>>(jsonstr1.ToString());
            Debug.Log($"Loaded and Deserialized jointPoses.Count:{jointPoses.Count}");

            var jsonstr2 = Resources.Load<TextAsset>("JsonInitializers/EffectorPoses");
            effPoses = JsonConvert.DeserializeObject<Dictionary<RobotJointPose, (Vector3, Quaternion)>>(jsonstr2.ToString());
            Debug.Log($"Loaded and Deserialized effPoses.Count:{effPoses.Count}");
        }

        public bool definingEffectorPoses = false;

        public void WriteOutPosesToJsonFiles()
        {
            // Joint Poses
            var jsonSettings = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };// Quaternions cause trouble
            string jsonstr1 = JsonConvert.SerializeObject(jointPoses, jsonSettings );
            Debug.Log($"DeSerializing");
            var testjp = JsonConvert.DeserializeObject<Dictionary<RobotJointPose,float[]>>(jsonstr1);
            Debug.Log($"Deserialized jointPoses.Count:{jointPoses.Count} testjp.Count:{testjp.Count}");
            File.WriteAllText("JointPoses.json", jsonstr1);

            // Effector Poses
            string jsonstr2 = JsonConvert.SerializeObject(effPoses, jsonSettings);
            Debug.Log($"DeSerializing");
            var testep = JsonConvert.DeserializeObject<Dictionary<RobotJointPose, (Vector3, Quaternion) >>(jsonstr2);
            Debug.Log($"Deserialized effPoses.Count:{effPoses.Count} testep.Count:{testep.Count}");
            File.WriteAllText("EffectorPoses.json", jsonstr2);
        }

        public IEnumerator DefineEffectorPoses(float sleepsecs = 0.5f)
        {
            definingEffectorPoses = true;
            yield return new WaitForSeconds(sleepsecs); // wait for others to pick up on signal

            GameObject effroot = null;
            if (magmo.effectorPoseMarkers)
            {
                effroot = new GameObject("EffectorMarkers");
            }

            effPoses = new Dictionary<RobotJointPose, (Vector3, Quaternion)>();
            foreach (var key in jointPoses.Keys)
            {
                var joints = jointPoses[key];

                //magmo.planner.RealizeJointsMagicMovement(joints);
                this.RealiseRobotPose(key);
                yield return new WaitForSeconds(sleepsecs);

                var (ok,pwc, q) = this.GetEffectorPose();
                var p = pwc;
                //var p = TransformToRobotCoordinates(pwc);
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
                effPoses[key] = (p, q);
                if (magmo.effectorPoseMarkers)
                {
                    var sz = 0.2f / 10;
                    var sz2 = sz / 2;
                    var ego = UnityUt.CreateSphere(null, "limegreen", sz, collider: false);
                    ego.name = "Sph-" + key.ToString();
                    var xax = UnityUt.CreateSphere(ego, "red", sz / 5, collider: false);
                    xax.transform.position += new Vector3(sz2, 0, 0);
                    var yax = UnityUt.CreateSphere(ego, "green", sz / 5, collider: false);
                    yax.transform.position += new Vector3(0, sz2, 0);
                    var zax = UnityUt.CreateSphere(ego, "blue", sz / 5, collider: false);
                    zax.transform.position += new Vector3(0, 0, sz2);
                    ego.transform.position = p;
                    ego.transform.rotation = q;
                    ego.transform.parent = effroot.transform;
                }
                Debug.Log($"{key} {eftxt}");
            }

            WriteOutPosesToJsonFiles();

            definingEffectorPoses = false;
        }

        public void AddBoxToRobot()
        {
            if (vgriptrans == null)
            {
                magmo.ErrMsg("AddBoxToRobot - Robot is null");
                return;
            }
            switch (magmo.mmctrl.mmBoxMode)
            {
                case MmBoxMode.FakePooled:
                    {
                        var lbox = MmBox.GetFreePooledBox(BoxStatus.onRobot);
                        AttachBoxToRobot(lbox);
                        ActivateRobBox(false);
                        break;
                    }
            }
        }
        Vector3 robotoffset = new Vector3(0, -0.16f, 0);
        public void AttachBoxToRobot(MmBox box)
        {
            if (vgriptrans == null)
            {
                magmo.ErrMsg("AttachBoxToRobot - Robot is null");
                return;
            }
            if (box==null)
            {
                magmo.ErrMsg("AttachBoxToRobot - Box is null");
                return;
            }
            //Debug.Log($"Attaching Box to Robot - {box.boxid1} {box.boxid2} {box.boxclr} {magmo.GetHoldMethod()})");
            this.box = box;
            switch (magmo.GetBoxSimMode())
            {
                case MmBoxSimMode.Hierarchy:
                    // Debug.Log($"Attaching Box to Robot - Hierarchy");
                    box.transform.parent = null;
                    box.transform.localRotation = Quaternion.Euler(0, 90, 0);
                    box.transform.localPosition = robotoffset;
                    box.transform.SetParent(vgriptrans, worldPositionStays: false);
                    break;
                case MmBoxSimMode.Physics:
                case MmBoxSimMode.Kinematics:
                    // the proper way to do Physics for a vaccume gripper would involve Fluid Dynamics and we are not going there
                    // Debug.Log($"Associating Box to Robot - Dragged and Physics");
                    if (box != null)
                    {
                        if (box.rigbod != null)
                        {
                            box.rigbod.isKinematic = true;
                        }
                        box.transform.position = vgriptrans.transform.position + robotoffset;
                        box.transform.rotation = vgriptrans.transform.rotation * Quaternion.Euler(0, 90, 0);
                    }
                    break;
            }
            loadState = true;
            box.SetBoxStatus(BoxStatus.onRobot);
        }
        public MmBox DetachhBoxFromRobot(bool warnIfInconsistent=true)
        {
            var oldbox = box;
            if (oldbox != null)
            {
                // Debug.Log($"Detaching Box from Robot - {oldbox.boxid1} {oldbox.boxid2} {oldbox.boxclr})");
                oldbox.SetBoxStatus(BoxStatus.free);
            }
            else
            {
                if (warnIfInconsistent)
                {
                    Debug.LogWarning($"Detaching Null Box from Robot - potential problem");
                }
            }
            box = null;
            loadState = false;
            return oldbox;
        }


        public void InitRobotBoxState(bool startLoadState)
        {
            if (magmo.mmctrl.mmBoxMode == MmBoxMode.FakePooled)
            {
                AddBoxToRobot();
                ActivateRobBox(startLoadState);
            }
            else if (magmo.mmctrl.mmBoxMode == MmBoxMode.RealPooled)
            {
                if( startLoadState)
                {
                    AddBoxToRobot();
                }
                else
                {
                    DetachhBoxFromRobot(warnIfInconsistent:false);
                }
            }
        }
        public bool ActivateRobBox(bool newstat)
        {
            var rv = false;
            if (box != null)
            {
                rv = box.gameObject.activeSelf;
                box.gameObject.SetActive(newstat);
                loadState = newstat;
            }
            return rv;
        }

        public void MutateRobotPose(RobotJointPose startpose,RobotJointPose endpose)
        {
            RealiseRobotPose(endpose);
        }

        public void RealiseRobotPose(RobotJointPose pose)
        {
            if (!jointPoses.ContainsKey(pose))
            {
                magmo.ErrMsg($"MmRobot.AssumpPose pose {pose} not assigned");
                return;
            }
            var angles = jointPoses[pose];
            var planner = magmo.planner;
            for (int i = 0; i < angles.Length; i++)
            {
                planner.PositionJoint(i, angles[i]);
            }
        }

        public (bool, Vector3, Quaternion) GetEffectorPose()
        {
            var p = Vector3.zero;
            var q = Quaternion.identity;
            var ok = false;
            if (tooltrans != null)
            {
                p = this.tooltrans.position;
                q = this.tooltrans.rotation;
                ok = true;
            }
            return (ok, p, q);
        }

        public float [] GetRobotPos()
        {
            var far = new List<float>();
            var planner = magmo.planner;
            for (int i = 0; i < 6; i++)
            {
                far.Add(planner.GetJointPosition(i));
            }
            return far.ToArray();
        }

        public double[] GetRobotPosDouble()
        {
            var far = new List<double>();
            var planner = magmo.planner;
            for (int i = 0; i < 6; i++)
            {
                far.Add(planner.GetJointPosition(i));
            }
            return far.ToArray();
        }

        void DoRobotPose()
        {
            if (updatecount == 0)
            {
                oldPoseTuple = this.currentRobotPose;
                return;
            }
            if (oldPoseTuple!=currentRobotPose)
            {
                RealiseRobotPose(currentRobotPose);
                oldPoseTuple = currentRobotPose;
            }
        }

        int fixedUpdateCount = 0;
        bool lastEnableUrdfInertialMatrix;

        private void FixedUpdate()
        {
            if (fixedUpdateCount==0)
            {
                Debug.Log($"FixedUpdate count initilized in MmRobot");
                lastEnableUrdfInertialMatrix = enableUrdfInertialMatrix;
            }
            fixedUpdateCount++;
            if (lastEnableUrdfInertialMatrix!=enableUrdfInertialMatrix)
            {
                var urdfList = FindObjectsOfType<Unity.Robotics.UrdfImporter.UrdfInertial>();
                Debug.Log($"MmRobot - Toggling UrdfInertial to {enableUrdfInertialMatrix} for {urdfList.Length} components");
                foreach ( var urdf in urdfList)
                {
                    urdf.enabled = enableUrdfInertialMatrix;
                    urdf.displayInertiaGizmo = enableUrdfInertialMatrix;
                }
                lastEnableUrdfInertialMatrix = enableUrdfInertialMatrix;
            }
            if (box != null)
            {
                switch(magmo.GetBoxSimMode())
                {
                    case MmBoxSimMode.Physics:  // FixedUpdate
                    case MmBoxSimMode.Kinematics:  // FixedUpdate
                        if (box.rigbod!=null)
                        {
                            box.rigbod.isKinematic = true;
                        }
                        box.transform.position = vgriptrans.transform.position + robotoffset;
                        box.transform.rotation = vgriptrans.transform.rotation * Quaternion.Euler(0, 90, 0);
                        break;
                }
            }
            int i = 0;
            foreach (var xf in xforms)
            {
                locToWorldMat[i] = FilterMatrix(xf.localToWorldMatrix);
                i++;
            }
        }

        static float filterToZero(float f)
        {
            if (Mathf.Abs(f)<1e-6)
            {
                return 0;
            }
            return f;
        }


        public static Matrix4x4 FilterMatrix(Matrix4x4 m)
        {
            Matrix4x4 rv;
            rv.m00 = filterToZero(m.m00);
            rv.m01 = filterToZero(m.m01);
            rv.m02 = filterToZero(m.m02);
            rv.m03 = filterToZero(m.m03);

            rv.m10 = filterToZero(m.m10);
            rv.m11 = filterToZero(m.m11);
            rv.m12 = filterToZero(m.m12);
            rv.m13 = filterToZero(m.m13);

            rv.m20 = filterToZero(m.m20);
            rv.m21 = filterToZero(m.m21);
            rv.m22 = filterToZero(m.m22);
            rv.m23 = filterToZero(m.m23);

            rv.m30 = filterToZero(m.m30);
            rv.m31 = filterToZero(m.m31);
            rv.m32 = filterToZero(m.m32);
            rv.m33 = filterToZero(m.m33);
            return rv;
        }

        public void TrackTarget()
        {
            if (trackingTarget!=null)
            {
                var deltapos =  trackingTarget.position - lastTrackingTargetPos;
                var delatrot = trackingTarget.rotation * Quaternion.Inverse(lastTrackingTargetRot);
                transform.position += deltapos;
                transform.rotation *= delatrot;
            }
        }

        RobotJointPose oldPoseTuple;
        int updatecount;
        void Update()
        {
            DoRobotPose();
            TrackTarget();
            updatecount++;
        }
    }
}