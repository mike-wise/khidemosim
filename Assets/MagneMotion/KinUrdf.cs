using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KinUrdf : MonoBehaviour
{
    public enum RotAx { XP, XN, YP, YN, ZP, ZN }
    public enum ArmTyp { RS007N, RS007L, UR10 }

    public enum JointType { Revolute, Fixed, Prisimatic, NotJoint }

    public enum ArmViewing { Meshes, Pivots }


    [Header("Viewing")]
    public ArmViewing armViewing;

    [Header("TestActions")]
    public bool performAction;
    public int actionIndex;
    public RotAx actionAxis;
    public float actionAngle;
    public bool performIncAction;
    public bool performDecAction;

    [Header("Random Actions")]
    public bool newRanPoint;
    public bool continuousMove;

    bool moving;
    float[] startAngles;
    float[] targAngles;
    float startTime;
    float moveTime;

    [Header("Internal")]
    public ArmTyp armTyp;
    public int nlinks;
    public string[] linkName;
    RotAx[] rotAx;
    JointType[] jointType;
    //Vector3[] rotAxis;
    float[] jointMin;
    float[] jointMax;
    float[] curAng;
    float[] baseAng;
    RotAx[] baseAxis;

    Transform[] xform;


    Transform BreathFirstFindLink(Transform parent, string seekName, int depth = 0, int nchk = 0)
    {
        // First breath
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            nchk++;
            if (child.name == seekName)
            {
                // Debug.Log($"Found {seekName} at depth {depth} nchk:{nchk}");
                return child;
            }
        }
        // Then deep
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            var deepChild = BreathFirstFindLink(child, seekName, depth++, nchk);
            if (deepChild != null)
            {
                return deepChild;
            }
        }
        return null;
    }


    Transform FindLink(string linkname)
    {
        // var go = GameObject.Find(linkname);
        var go = BreathFirstFindLink(transform, linkname);
        if (go == null)
        {
            return null;
        }
        return go.transform;
    }

    void DestroyComponent<T>(GameObject go)
    {
        var comp = go.GetComponent<T>() as Object;
        Destroy(comp);
    }

    void DestoryUrdfComponents(GameObject go)
    {
        if (go != null)
        {
            DestroyComponent<Unity.Robotics.UrdfImporter.UrdfInertial>(go);
            DestroyComponent<Unity.Robotics.UrdfImporter.UrdfJointRevolute>(go);
            DestroyComponent<Unity.Robotics.UrdfImporter.UrdfJointFixed>(go);
            DestroyComponent<Unity.Robotics.UrdfImporter.UrdfJointPrismatic>(go);
            DestroyComponent<ArticulationBody>(go);
            DestroyComponent<Unity.Robotics.UrdfImporter.UrdfLink>(go);
            DestroyComponent<Unity.Robotics.UrdfImporter.Control.Controller>(go);
            DestroyComponent<Unity.Robotics.UrdfImporter.UrdfRobot>(go);
        }
    }

    void DestroyUrdfName(string linkname)
    {
        var t = FindLink(linkname);
        if (t == null)
        {
            Debug.LogWarning($"Cound not find link {linkname} in Robot {name}");
        }
        else
        {
            DestoryUrdfComponents(t.gameObject);
        }
    }

    void InactivateUrdf()
    {
        foreach (var ln in linkName)
        {
            DestroyUrdfName(ln);
        }
        DestroyUrdfName("base_link");
        DestroyUrdfName("world");
        DestoryUrdfComponents(this.gameObject);
    }
//    public void FishOutUrdfValuesFromLink(int jidx,string linkname)
//    {
//        var t = FindLink(linkname);
//        if (t == null)
//        {
//            Debug.LogWarning($"Cound not find link {linkname} in Robot {name}");
//        }
//        else
//        {
//            var go = t.gameObject;
//            var urdflink = go.GetComponent<Unity.Robotics.UrdfImporter.UrdfLink>();
//            if (urdflink == null)
//            {
//                Debug.LogWarning($"Link {linkname} does not have a urdf link");
//            }
//            else
//            {
//                jointType[jidx] = JointType.Revolute;
//                var jrev = go.GetComponent<Unity.Robotics.UrdfImporter.UrdfJointRevolute>();
//                var art = go.GetComponent<ArticulationBody>();
////                Quaternion rot = Quaternion.AngleAxis(-deltaState * Mathf.Rad2Deg, unityJoint.axis);
////                transform.rotation = transform.rotation * rot;
//            }
//            DestoryUrdfComponents(t.gameObject);
//        }

//    }
//    public void FishOutUrdfValues()
//    {
//        int idx = 0;
//        foreach( var ln in linkName )
//        {
//            FishOutUrdfValuesFromLink(idx,ln);
//            idx++;
//        }
//    }

    public void Init( ArmTyp armTyp )
    {
        this.armTyp = armTyp;
        switch(armTyp)
        {
            default:

            case ArmTyp.RS007L:
            case ArmTyp.RS007N:
                {
                    nlinks = 6;
                    linkName = new string[] { "link1", "link2", "link3", "link4", "link5", "link6" };
                    rotAx = new RotAx[] { RotAx.YP, RotAx.YP, RotAx.YP, RotAx.YP, RotAx.YP, RotAx.YP };
                    jointMin = new float[] { -360, -90, -90, -360, -90, -360 };
                    jointMax = new float[] {  360,  90, 90,   360,  90,  360 };
                    //jointMin = new float[] { -360, -270, -310, -400, -250, -720 }; // from rs007n.urdf
                    //jointMax = new float[] { +360, +270, +310, +400, +250, +720 };
                    curAng = new float[] { 0, 0, 0, 0, 0, 0 };
                    baseAxis = new RotAx[] { RotAx.XP, RotAx.XP, RotAx.XP, RotAx.XP, RotAx.XP, RotAx.XP };
                    baseAng = new float[] { 0, 270, 0, 90, 270, 270 };
                    var vr = JointType.Revolute;
                    jointType = new JointType[] { vr, vr, vr, vr, vr, vr };
                    InactivateUrdf();
                    break;
                }
            case ArmTyp.UR10:
                {
                    nlinks = 6;
                    linkName = new string[] { "shoulder_link", "upper_arm_link", "forearm_link", "wrist_1_link", "wrist_2_link", "wrist_3_link" };
                    rotAx = new RotAx[] { RotAx.YP, RotAx.YP, RotAx.YP, RotAx.YP, RotAx.YP, RotAx.YP };
                    jointMin = new float[] { -360, -90, -90, -360, -90, -360 };
                    jointMax = new float[] { 360, 90, 90, 360, 90, 360 };
                    //jointMin = new float[] { -360, -270, -310, -400, -250, -720 }; // from rs007n.urdf
                    //jointMax = new float[] { +360, +270, +310, +400, +250, +720 };
                    curAng = new float[] { 0, 0, 0, 0, 0, 0 };
                    baseAxis = new RotAx[] { RotAx.XP, RotAx.XP, RotAx.XP, RotAx.XP, RotAx.XP, RotAx.XP };
                    baseAng = new float[] { 0, 270, 0, 90, 270, 270 };
                    var vr = JointType.Revolute;
                    jointType = new JointType[] { vr, vr, vr, vr, vr, vr };
                    InactivateUrdf();
                    break;
                }
        }
        var xformlist = new List<Transform>();
        foreach (var lname in linkName)
        {
            var xform = FindLink(lname);
            if (xform==null)
            {
                Debug.LogWarning($"Cound not find link:{lname}");
            }
            xformlist.Add(xform);
        }
        xform = xformlist.ToArray();
        AddPivots(initialActiveStatus: false);
        moving = false;
        newRanPoint = false;
        performAction = false;
        oldState = ArmViewing.Pivots;
        armViewing = ArmViewing.Meshes;
        RealizeArmViewingState();
    }

    Quaternion Quang(RotAx rotax,float angle)
    {
        switch (rotax)
        {
            default:
            case RotAx.XP: return Quaternion.Euler(+angle, 0, 0);
            case RotAx.XN: return Quaternion.Euler(-angle, 0, 0);
            case RotAx.YP: return Quaternion.Euler(0,+angle, 0);
            case RotAx.YN: return Quaternion.Euler(0,-angle, 0);
            case RotAx.ZP: return Quaternion.Euler(0,0,+angle);
            case RotAx.ZN: return Quaternion.Euler(0,0,-angle);
        }
    }

    public void SetAngle(int jidx, float angle)
    {
        if (jidx<0 || nlinks<=jidx)
        {
            Debug.LogWarning($"joint index out of range: {jidx} nlinks:{nlinks}");
            return;
        }
        var xf = xform[jidx];
        var ang = Mathf.Min(jointMax[jidx], Mathf.Max(jointMin[jidx], angle));
        var brot = Quang(baseAxis[jidx], baseAng[jidx]);
        var qrot = Quang(rotAx[jidx],ang);
        //var qrot = Quaternion.AngleAxis(ang, rotAxis[jidx]);
        curAng[jidx] = ang;
        xf.localRotation = brot*qrot;
        Debug.Log($"Set LocalRotation:{qrot}");
    }

    public void IncAngle(int jidx, float angle)
    {
        if (jidx < 0 || nlinks <= jidx)
        {
            Debug.LogWarning($"joint index out of range: {jidx} nlinks:{nlinks}");
            return;
        }
        var xf = xform[jidx];
        var ang = Mathf.Min(jointMax[jidx], Mathf.Max(jointMin[jidx], angle));
        var qrot = Quang(actionAxis, ang);
        //var qrot = Quaternion.AngleAxis(ang, rotAxis[jidx]);
        curAng[jidx] += ang;
        xf.localRotation *= qrot;
        Debug.Log($"Inc LocalRotation:{qrot}");
    }

    public void DecAngle(int jidx, float angle)
    {
        IncAngle(jidx, -angle);
    }

    System.Random ranman = new System.Random(1234);

    public void SetupRandomPointToMoveTowards()
    {
        targAngles = new float[nlinks];
        startAngles = new float[nlinks];
        for(int i = 0; i<nlinks; i++)
        {
            //Debug.Log($"MoveToRandomPoint i:{i}");
            startAngles[i] = curAng[i];
            var jmin = jointMin[i];
            var jmax = jointMax[i];
            targAngles[i] = ((jmax-jmin)*ranman.Next(0, 1000) / 1000f) + jmin;
        }
        startTime = Time.time;
        moveTime = 5f;
        moving = true;
    }

    public void Moveit()
    {
        var delttime = Time.time - startTime;
        var lamb = delttime / moveTime;
        //Debug.Log($"Moving time:{Time.time} delt:{delttime} lamb:{lamb}");
        if (lamb >= 1)
        {
            moving = false;
            lamb = 1;
        }
        for (int i = 0; i < nlinks; i++)
        {
            var newang = lamb * (targAngles[i] - startAngles[i]) + startAngles[i];
            SetAngle(i, newang);
        }
        if (!moving && continuousMove)
        {
            SetupRandomPointToMoveTowards();
        }
    }

    public void ActivateVisuals(bool activeStatus)
    {
        foreach (var xf in xform)
        {
            if (xf != null)
            {
                var visgo = BreathFirstFindLink(xf, "Visuals");
                if (visgo == null)
                {
                    Debug.LogWarning($"Can't find visuals for {xf.name}");
                }
                else
                {
                    visgo.gameObject.SetActive(activeStatus);
                }
            }
        }
    }

    public void ActivatePivots(bool activeStatus)
    {
        foreach (var xf in xform)
        {
            if (xf != null)
            {
                var visgo = BreathFirstFindLink(xf, "Pivots");
                if (visgo == null)
                {
                    Debug.LogWarning($"Can't find Pivots for {xf.name}");
                }
                else
                {
                    visgo.gameObject.SetActive(activeStatus);
                }
            }
        }
    }

    GameObject MakeCub(Transform parent,string name,Vector3 ofs,float sz,Color clr )
    {
        var cub = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cub.name = name;
        cub.transform.localScale = new Vector3(sz, sz, sz);
        cub.transform.position = ofs;
        cub.transform.SetParent(parent, worldPositionStays: false);
        var material = cub.GetComponent<Renderer>().material;
        material.color = clr;
        return cub;
    }
    public void AddPivots(bool initialActiveStatus)
    {
        foreach (var xf in xform)
        {
            var sz = 1f;
            var sz2 = sz/2;
            var basecub = MakeCub(xf, "Pivots", Vector3.zero, sz, Color.white);
            var xfbc = basecub.transform;
            MakeCub(xfbc, "X", new Vector3(sz2, 0, 0), sz2, Color.red);
            MakeCub(xfbc, "Y", new Vector3(0, sz2, 0), sz2, Color.green);
            MakeCub(xfbc, "Z", new Vector3(0, 0, sz2), sz2, Color.blue);

            var csz = 0.02f;
            basecub.transform.localScale = new Vector3(csz,csz,csz);
            basecub.SetActive(initialActiveStatus);
            basecub.transform.SetAsFirstSibling();
        }
    }

    ArmViewing oldState;

    public void RealizeArmViewingState()
    {
        if (oldState!=armViewing)
        {
            switch (armViewing)
            {
                case ArmViewing.Meshes:
                    ActivatePivots(false);
                    ActivateVisuals(true);
                    break;
                case ArmViewing.Pivots:
                    ActivatePivots(true);
                    ActivateVisuals(false);
                    break;
            }
            oldState = armViewing;
        }
    }

    private void Start()
    {
        if (name.StartsWith("khi_rs007n"))
        {
            Init(ArmTyp.RS007N);
        }
        else if (name.StartsWith("khi_rs007l"))
        {
            Init(ArmTyp.RS007L);
        }
        else if (name.StartsWith("ur10"))
        {
            Init(ArmTyp.UR10);
        }
    }

    private void Update()
    {
        if (performAction)
        {
            SetAngle(actionIndex, actionAngle);
            performAction = false;
        }

        if (performIncAction)
        {
            IncAngle(actionIndex, actionAngle);
            performIncAction = false;
        }

        if (performDecAction)
        {
            DecAngle(actionIndex, actionAngle);
            performDecAction = false;
        }

        if (newRanPoint)
        {
            SetupRandomPointToMoveTowards();
            newRanPoint = false;
        }

        if (moving)
        {
            Moveit();
        }

        RealizeArmViewingState();
    }
}
