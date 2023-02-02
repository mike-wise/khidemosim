using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System.IO;

public enum RobotPose
{
    zero, deg10, rest, restr2r, key, ecartup, ecartdn, fcartup, fcartdn,
    key00up, key00dn, key01up, key01dn, key02up, key02dn, key03up, key03dn,
    key10up, key10dn, key11up, key11dn, key12up, key12dn, key13up, key13dn,
    key20up, key20dn, key21up, key21dn, key22up, key22dn, key23up, key23dn,
    Pose000000, Pose099900, Pose090000, Pose099999, Pose099000, Pose099990, Pose999999,
}

public enum KUrRobotPose
{
}

public enum RobotFamily
{
    KhiRs007, UniRob
}


public class RobotPoses : MonoBehaviour
{

    public Dictionary<RobotPose, float[]> jointPoses;
    Dictionary<(int, int), (RobotPose, RobotPose)> TrayUpDownJointPoses;

    public void DefineJointPose(RobotPose pose, (double a1, double a2, double a3, double a4, double a5, double a6) ptd)
    {
        //  Note we convert from doubles to floats here to make life easier
        var poseTuple = new float[6] { (float)ptd.a1, (float)ptd.a2, (float)ptd.a3, (float)ptd.a4, (float)ptd.a5, (float)ptd.a6 };
        jointPoses[pose] = poseTuple;
    }

    public void SetUpDownPoseForTrayPos((int, int) key, (RobotPose, RobotPose) poses)
    {
        TrayUpDownJointPoses[key] = poses;
    }
    public (RobotPose rpup, RobotPose rpdn) GetTrayUpAndDownPoses((int, int) TrayRowColPos)
    {
        return TrayUpDownJointPoses[TrayRowColPos];
    }

    public float[] GetPose(RobotPose p1)
    {
        var a1 = jointPoses[p1];
        return a1;
    }

    public float[] InterpolatePoses(RobotPose p1, RobotPose p2, float lamb)
    {
        var a1 = jointPoses[p1];
        var a2 = jointPoses[p1];
        var res = new List<float>();
        for (int i = 0; i < a1.Length; i++)
        {
            var val = lamb * (a2[i] - a1[i]) + a1[i];
            res.Add(val);
        }
        return res.ToArray();
    }

    public void LoadPoses()
    {
        //var jsonstr1 = Resources.Load<TextAsset>("JsonInitializers/JointPoses");
        //jointPoses = JsonConvert.DeserializeObject<Dictionary<RobotJointPose1, float[]>>(jsonstr1.ToString());
        //Debug.Log($"Loaded and Deserialized jointPoses.Count:{jointPoses.Count}");

        //var jsonstr2 = Resources.Load<TextAsset>("JsonInitializers/EffectorPoses");
        //effPoses = JsonConvert.DeserializeObject<Dictionary<RobotJointPose1, (Vector3, Quaternion)>>(jsonstr2.ToString());
        //Debug.Log($"Loaded and Deserialized effPoses.Count:{effPoses.Count}");
    }

    public void InitializePoses()
    {
        jointPoses = new Dictionary<RobotPose, float[]>();
        DefineJointPose(RobotPose.zero, (0, 0, 0, 0, 0, 0));
        DefineJointPose(RobotPose.deg10, (10, 10, 10, 10, 10, 10));
        DefineJointPose(RobotPose.rest, (-16.805, 16.073, -100.892, 0, -63.021, 106.779));


        DefineJointPose(RobotPose.fcartup, (-26.76, 32.812, -74.172, 0, -73.061, 26.755));
        DefineJointPose(RobotPose.fcartdn, (-26.749, 40.511, -80.809, 0, -58.682, 26.75));

        DefineJointPose(RobotPose.ecartup, (-50.35, 49.744, -46.295, 0, -83.959, 50.347));
        DefineJointPose(RobotPose.ecartdn, (-50.351, 55.206, -54.692, 0, -70.107, 50.352));

        DefineJointPose(RobotPose.key00up, (-14.864, 26.011, -87.161, 0, -66.826, 102.537));
        DefineJointPose(RobotPose.key00dn, (-14.48, 28.642, -89.821, 0, -61.519, 104.49));
        DefineJointPose(RobotPose.key01up, (-16.88, 16.142, -101.146, 0, -62.727, 106.877));
        DefineJointPose(RobotPose.key01dn, (-16.808, 19.303, -103.537, 0, -57.146, 106.813));
        DefineJointPose(RobotPose.key02up, (-20.924, 3.754, -115.647, -0.001, -60.607, 110.92));
        DefineJointPose(RobotPose.key02dn, (-20.921, 7.105, -118.181, -0.001, -54.702, 110.919));
        DefineJointPose(RobotPose.key03up, (-25.945, -6.875, -125.815, -0.001, -61.063, 115.944));
        DefineJointPose(RobotPose.key03dn, (-25.942, -1.839, -129.447, -0.001, -52.394, 115.943));

        DefineJointPose(RobotPose.key10up, (-3.833, 24.123, -90.829, 0, -65.057, 93.834));
        DefineJointPose(RobotPose.key10dn, (-3.839, 27.028, -93.268, 0, -59.685, 93.835));
        DefineJointPose(RobotPose.key11up, (-4.485, 16.308, -106.377, 0, -57.305, 94.482));
        DefineJointPose(RobotPose.key11dn, (-4.487, 17.444, -107.108, 0, -55.446, 94.486));
        DefineJointPose(RobotPose.key12up, (-5.674, 2.826, -121.133, 0, -56.032, 95.67));
        DefineJointPose(RobotPose.key12dn, (-5.677, 4.939, -122.452, 0, -52.61, 95.675));
        DefineJointPose(RobotPose.key13up, (-7.204, -11.615, -129.863, 0, -61.795, 97.205));
        DefineJointPose(RobotPose.key13dn, (-7.207, -7.853, -132.776, 0, -55.074, 97.205));

        DefineJointPose(RobotPose.key20up, (7.072, 24.158, -89.905, 0, -65.933, 82.92));
        DefineJointPose(RobotPose.key20dn, (7.07, 27.478, -92.784, 0, -59.743, 82.929));
        DefineJointPose(RobotPose.key21up, (8.251, 16.929, -105.936, 0, -57.122, 81.753));
        DefineJointPose(RobotPose.key21dn, (8.249, 17.868, -106.538, 0, -55.594, 81.752));
        DefineJointPose(RobotPose.key22up, (8.251, 16.929, -105.936, 0, -57.122, 81.753));
        DefineJointPose(RobotPose.key22dn, (8.249, 17.868, -106.538, 0, -55.594, 81.752));
        DefineJointPose(RobotPose.key23up, (-7.204, -11.615, -129.863, 0, -61.795, 97.205));
        DefineJointPose(RobotPose.key23dn, (-7.207, -7.853, -132.776, 0, -55.074, 97.205));

        //p1(RobotPose.restr2r, (-21.7, 55.862, -39.473, -0.008, -84.678, 13.565));
        var a = InterpolatePoses(RobotPose.fcartup, RobotPose.ecartup, 0.5f);
        DefineJointPose(RobotPose.restr2r, (a[0], a[1], a[2], a[3], a[4], a[5]));

        DefineJointPose(RobotPose.Pose000000, (0, 0, 0, 0, 0, 0));
        DefineJointPose(RobotPose.Pose099900, (0, -90, -90, -90, 0, 0));
        DefineJointPose(RobotPose.Pose090000, (0, -90, 0, 0, 0, 0));
        DefineJointPose(RobotPose.Pose099999, (0, -90, -90, -90, -90, -90));
        DefineJointPose(RobotPose.Pose099000, (0, -90, -90, 0, 0, 0));
        DefineJointPose(RobotPose.Pose099990, (0, -90, -90, -90, -90, 0));
        DefineJointPose(RobotPose.Pose999999, (-90, -90, -90, -90, -90, -90));

        LoadPoses();

        TrayUpDownJointPoses = new Dictionary<(int, int), (RobotPose, RobotPose)>();
        SetUpDownPoseForTrayPos((0, 0), (RobotPose.key00up, RobotPose.key00dn));
        SetUpDownPoseForTrayPos((0, 1), (RobotPose.key01up, RobotPose.key01dn));
        SetUpDownPoseForTrayPos((0, 2), (RobotPose.key02up, RobotPose.key02dn));
        SetUpDownPoseForTrayPos((0, 3), (RobotPose.key03up, RobotPose.key03dn));

        SetUpDownPoseForTrayPos((1, 0), (RobotPose.key10up, RobotPose.key10dn));
        SetUpDownPoseForTrayPos((1, 1), (RobotPose.key11up, RobotPose.key11dn));
        SetUpDownPoseForTrayPos((1, 2), (RobotPose.key12up, RobotPose.key12dn));
        SetUpDownPoseForTrayPos((1, 3), (RobotPose.key13up, RobotPose.key13dn));

        SetUpDownPoseForTrayPos((2, 0), (RobotPose.key20up, RobotPose.key20dn));
        SetUpDownPoseForTrayPos((2, 1), (RobotPose.key21up, RobotPose.key21dn));
        SetUpDownPoseForTrayPos((2, 2), (RobotPose.key22up, RobotPose.key22dn));
        SetUpDownPoseForTrayPos((2, 3), (RobotPose.key23up, RobotPose.key23dn));
    }

    public bool definingEffectorPoses = false;

    public void WriteOutPosesToJsonFiles()
    {
        // Joint Poses
        //var jsonSettings = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };// Quaternions cause trouble
        //string jsonstr1 = JsonConvert.SerializeObject(jointPoses, jsonSettings);
        //Debug.Log($"DeSerializing");
        //var testjp = JsonConvert.DeserializeObject<Dictionary<RobotJointPose1, float[]>>(jsonstr1);
        //Debug.Log($"Deserialized jointPoses.Count:{jointPoses.Count} testjp.Count:{testjp.Count}");
        //File.WriteAllText("JointPoses.json", jsonstr1);

        //// Effector Poses
        //string jsonstr2 = JsonConvert.SerializeObject(effPoses, jsonSettings);
        //Debug.Log($"DeSerializing");
        //var testep = JsonConvert.DeserializeObject<Dictionary<RobotJointPose1, (Vector3, Quaternion)>>(jsonstr2);
        //Debug.Log($"Deserialized effPoses.Count:{effPoses.Count} testep.Count:{testep.Count}");
        //File.WriteAllText("EffectorPoses.json", jsonstr2);
    }


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
