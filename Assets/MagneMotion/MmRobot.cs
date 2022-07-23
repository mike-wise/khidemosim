using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RsJ1Msg = RosMessageTypes.Rs007Control.Rs007Joints1Msg;
using RsJ6Msg = RosMessageTypes.Rs007Control.Rs007Joints6Msg;
using Unity.Robotics.ROSTCPConnector;

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

    public enum RobotPose { 
        zero, deg10, rest, restr2r, key, ecartup,ecartdn, fcartup, fcartdn, 
        key00up, key00dn, key01up, key01dn, key02up, key02dn, key03up, key03dn,
        key10up, key10dn, key11up, key11dn, key12up, key12dn, key13up, key13dn,
        key20up, key20dn, key21up, key21dn, key22up, key22dn, key23up, key23dn,
    }

    public class MmRobot : MonoBehaviour
    {
        public bool loadState;
        public MagneMotion magmo;
        public Transform vgriptrans;
        public MmBox box;

        public RobotPose currentRobotPose;

        void Start()
        {
            magmo = FindObjectOfType<MagneMotion>();
            if (magmo==null)
            {
                magmo.ErrMsg($"MmRobot counld not find object of type MagneMotion");
            }
            var vacGripperName = "world/base_link/link1/link2/link3/link4/link5/link6/tool_link/gripper_base/Visuals/unnamed/RS007_Gripper_C_u";
            vgriptrans = transform.Find(vacGripperName);
            InitializePoses();

            //magmo.rosconnection.RegisterPublisher<RsJ6Msg>("Rs007Joints6");
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
            }
            loadState = false;
        }


        public void PublishJoints()
        {
            if (magmo.publishMovementsRos)
            {
                var ang = GetRobotPosDouble();
                var j6msg = new RsJ6Msg(ang);
                //magmo.rosconnection.Publish("Rs007Joints6", j6msg);
            }
        }

        public void PublishJointsZmq()
        {
            if (magmo.publishMovementsZmq)
            {
                var a = GetRobotPosDouble();
                var msg = $"j6|{a[0]:f1}, {a[1]:f1}, {a[2]:f1}, {a[3]:f1}, {a[4]:f1}, {a[5]:f1}";
                magmo.ZmqSendString(msg);
            }
        }

        void Rs007J1Change(RsJ1Msg j1msg)
        {
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

        Dictionary<RobotPose, float []> poses;
        Dictionary<(int,int),(RobotPose,RobotPose)> kez;

        public void p1(RobotPose pose,(double a1, double a2, double a3, double a4, double a5, double a6) ptd)
        {
            var poseTuple = new float [6] { (float)ptd.a1, (float)ptd.a2, (float)ptd.a3, (float)ptd.a4, (float)ptd.a5, (float)ptd.a6 };
            poses[pose] = poseTuple;
        }
        public void k1((int,int) key,(RobotPose,RobotPose) poses)
        {
            kez[key] = poses;
        }
        public (RobotPose rpup,RobotPose rpdn) GetPoses((int,int) key)
        {
            return kez[key];
        }

        public float[] InterpolatePoses(RobotPose p1, RobotPose p2,float lamb)
        {
            var a1 = poses[p1];
            var a2 = poses[p1];
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
            poses = new Dictionary<RobotPose, float[]>();
            p1(RobotPose.zero, (0, 0, 0, 0, 0, 0));
            p1(RobotPose.deg10, (10, 10, 10, 10, 10, 10));
            p1(RobotPose.rest, (-16.805, 16.073, -100.892, 0, -63.021, 106.779));

            
            p1(RobotPose.fcartup, (-26.76, 32.812, -74.172, 0, -73.061, 26.755));
            p1(RobotPose.fcartdn, (-26.749, 40.511, -80.809, 0, -58.682, 26.75));

            p1(RobotPose.ecartup, (-50.35, 49.744, -46.295, 0, -83.959, 50.347));
            p1(RobotPose.ecartdn, (-50.351, 55.206, -54.692, 0, -70.107, 50.352));

            p1(RobotPose.key00up, (-14.864, 26.011, -87.161, 0, -66.826, 102.537));
            p1(RobotPose.key00dn, (-14.48, 28.642, -89.821, 0, -61.519, 104.49));
            p1(RobotPose.key01up, (-16.88, 16.142, -101.146, 0, -62.727, 106.877));
            p1(RobotPose.key01dn, (-16.808, 19.303, -103.537, 0, -57.146, 106.813));
            p1(RobotPose.key02up, (-20.924, 3.754, -115.647, -0.001, -60.607, 110.92));
            p1(RobotPose.key02dn, (-20.921, 7.105, -118.181, -0.001, -54.702, 110.919));
            p1(RobotPose.key03up, (-25.945, -6.875, -125.815, -0.001, -61.063, 115.944));
            p1(RobotPose.key03dn, (-25.942, -1.839, -129.447, -0.001, -52.394, 115.943));

            p1(RobotPose.key10up, (-3.833, 24.123, -90.829, 0, -65.057, 93.834));
            p1(RobotPose.key10dn, (-3.839, 27.028, -93.268, 0, -59.685, 93.835));
            p1(RobotPose.key11up, (-4.485, 16.308, -106.377, 0, -57.305, 94.482));
            p1(RobotPose.key11dn, (-4.487, 17.444, -107.108, 0, -55.446, 94.486));
            p1(RobotPose.key12up, (-5.674, 2.826, -121.133, 0, -56.032, 95.67));
            p1(RobotPose.key12dn, (-5.677, 4.939, -122.452, 0, -52.61, 95.675));
            p1(RobotPose.key13up, (-7.204, -11.615, -129.863, 0, -61.795, 97.205));
            p1(RobotPose.key13dn, (-7.207, -7.853, -132.776, 0, -55.074, 97.205));

            p1(RobotPose.key20up, (7.072, 24.158, -89.905, 0, -65.933, 82.92));
            p1(RobotPose.key20dn, (7.07, 27.478, -92.784, 0, -59.743, 82.929));
            p1(RobotPose.key21up, (8.251, 16.929, -105.936, 0, -57.122, 81.753));
            p1(RobotPose.key21dn, (8.249, 17.868, -106.538, 0, -55.594, 81.752));
            p1(RobotPose.key22up, (8.251, 16.929, -105.936, 0, -57.122, 81.753));
            p1(RobotPose.key22dn, (8.249, 17.868, -106.538, 0, -55.594, 81.752));
            p1(RobotPose.key23up, (-7.204, -11.615, -129.863, 0, -61.795, 97.205));
            p1(RobotPose.key23dn, (-7.207, -7.853, -132.776, 0, -55.074, 97.205));

            //p1(RobotPose.restr2r, (-21.7, 55.862, -39.473, -0.008, -84.678, 13.565));
            var a = InterpolatePoses(RobotPose.fcartup, RobotPose.ecartup, 0.5f);
            p1(RobotPose.restr2r, (a[0], a[1], a[2], a[3], a[4], a[5]));



            kez = new Dictionary<(int, int), (RobotPose,RobotPose)>();
            k1((0, 0), (RobotPose.key00up, RobotPose.key00dn));
            k1((0, 1), (RobotPose.key01up, RobotPose.key01dn));
            k1((0, 2), (RobotPose.key02up, RobotPose.key02dn));
            k1((0, 3), (RobotPose.key03up, RobotPose.key03dn));

            k1((1, 0), (RobotPose.key10up, RobotPose.key10dn));
            k1((1, 1), (RobotPose.key11up, RobotPose.key11dn));
            k1((1, 2), (RobotPose.key12up, RobotPose.key12dn));
            k1((1, 3), (RobotPose.key13up, RobotPose.key13dn));

            k1((2, 0), (RobotPose.key20up, RobotPose.key20dn));
            k1((2, 1), (RobotPose.key21up, RobotPose.key21dn));
            k1((2, 2), (RobotPose.key22up, RobotPose.key22dn));
            k1((2, 3), (RobotPose.key23up, RobotPose.key23dn));
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
            this.box = box;
            Debug.Log($"Attaching Box to Robot - {box.boxid1} {box.boxid2} {box.boxclr})");
            box.transform.parent = null;
            box.transform.localRotation = Quaternion.Euler(90, 0, 0);
            box.transform.localPosition = new Vector3(0, -0.14f, 0);
            box.transform.SetParent(vgriptrans, worldPositionStays: false);
            loadState = true;
            box.SetBoxStatus(BoxStatus.onRobot);
        }
        public MmBox DetachhBoxFromRobot()
        {
            var oldbox = box;
            if (oldbox != null)
            {
                Debug.Log($"Detaching Box from Robot - {oldbox.boxid1} {oldbox.boxid2} {oldbox.boxclr})");
                oldbox.SetBoxStatus(BoxStatus.free);
            }
            else
            {
                Debug.Log($"Detaching Box from Robot (null box)");
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
                    DetachhBoxFromRobot();
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

        public void RealiseRobotPose(RobotPose pose)
        {
            if (!poses.ContainsKey(pose))
            {
                magmo.ErrMsg($"MmRobot.AssumpPose pose {pose} not assigned");
                return;
            }
            var angles = poses[pose];
            var planner = magmo.planner;
            for (int i = 0; i < angles.Length; i++)
            {
                planner.PositionJoint(i, angles[i]);
            }
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

        RobotPose oldPoseTuple;
        int updatecount;
        void Update()
        {
            DoRobotPose();
            updatecount++;
        }
    }
}