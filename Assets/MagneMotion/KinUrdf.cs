using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KinUrdf : MonoBehaviour
{
    public enum RotAx { None, XP, XN, YP, YN, ZP, ZN }
    public enum ArmTyp { RS007N, RS007L, UR10, URFAM }


    public enum ArmViewing { Meshes, Skel }

    [Header("Behavior")]
    public bool clipJoints = true;


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
    public string eeLinkName;
    public string worldLinkName;
    public string baseLinkName;
    public string[] linkName;
    RotAx[] rotAx;
    float[] jointMin;
    float[] jointMax;
    float[] jointOrg;
    float[] jointSign;
    float[] curAng;
    float[] baseAng;
    RotAx[] baseAxis;

    Dictionary<string, int> jidxdict;

    List<Transform> xform;


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

    void DestoryUrdfComponentsGo(GameObject go)
    {
        if (go != null)
        {
            DestroyComponent<JointControl>(go);
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

    void DestroyUrdfComponentsGoName(string linkname)
    {
        var t = FindLink(linkname);
        if (t == null)
        {
            Debug.LogWarning($"Cound not find link {linkname} in Robot {name}in DestroyUrdfComponentsGoName");
        }
        else
        {
            DestoryUrdfComponentsGo(t.gameObject);
        }
    }

    void InactivateUrdf()
    {
        foreach (var ln in linkName)
        {
            DestroyUrdfComponentsGoName(ln);
        }
        DestroyUrdfComponentsGoName(baseLinkName);
        DestroyUrdfComponentsGoName(worldLinkName);
        DestroyUrdfComponentsGoName(eeLinkName);
        DestoryUrdfComponentsGo(this.gameObject);
    }

    public void Init( ArmTyp armTyp )
    {
        this.armTyp = armTyp;
        curAng = new float[] { 0, 0, 0, 0, 0, 0 };
        startAngles= new float[] { 0, 0, 0, 0, 0, 0 };
        targAngles = new float[] { 0, 0, 0, 0, 0, 0 };
        switch (armTyp)
        {
            default:

            case ArmTyp.RS007L:
            case ArmTyp.RS007N:
                {
                    nlinks = 6;
                    baseLinkName = "base_link";
                    worldLinkName = "world";
                    eeLinkName = "tool_link";
                    linkName = new string[] { "link1", "link2", "link3", "link4", "link5", "link6" };
                    rotAx = new RotAx[] { RotAx.YP, RotAx.YP, RotAx.YP, RotAx.YP, RotAx.YP, RotAx.YP };
                    jointMin = new float[] { -360, -90, -90, -360, -90, -360 };
                    jointMax = new float[] { +360, +90, +90, +360, +90, +360 };
                    jointOrg = new float[] { 0, 0, 0, 0, 0, 0 };
                    jointSign = new float[] { -1, 1, -1, 1, -1, 1 };
                    //jointMin = new float[] { -360, -270, -310, -400, -250, -720 }; // from rs007n.urdf
                    //jointMax = new float[] { +360, +270, +310, +400, +250, +720 };
                    baseAxis = new RotAx[] { RotAx.None, RotAx.XP, RotAx.None, RotAx.XP, RotAx.XP, RotAx.XP };
                    baseAng = new float[] { 0, 270, 0, 90, 270, 270 };
                    break;
                }
            case ArmTyp.URFAM:
                {
                    nlinks = 6;
                    baseLinkName = "base_link";
                    worldLinkName = "world";
                    eeLinkName = "ee_link";
                    linkName = new string[] { "shoulder_link", "upper_arm_link", "forearm_link", "wrist_1_link", "wrist_2_link", "wrist_3_link" };
                    rotAx = new RotAx[] { RotAx.YP, RotAx.XP, RotAx.XP, RotAx.XP, RotAx.YP, RotAx.XP };
                    jointMin = new float[] { -360, -90, -90, -90,  -90, -360 };
                    jointMax = new float[] { +360, +90, +90, +90,  +90, +360 };
                    jointOrg = new float[] {    0,  -90,  0,  -90,   0, 0 };
                    jointSign = new float[] { 1, 1, 1, 1, 1, 1 };
                    curAng = new float[] { 0, 0, 0, 0, 0, 0 };
                    baseAxis = new RotAx[] { RotAx.None, RotAx.None, RotAx.None, RotAx.None, RotAx.None, RotAx.None };
                    baseAng = new float[] { 0, 0, 0, 0, 0, 0 };
                    break;
                }
        }
        jidxdict = new Dictionary<string, int>();
        int idx = 0;
        foreach(var ln in linkName)
        {
            jidxdict[ln] = idx++;
        }
        InactivateUrdf();
        var xformlist = new List<Transform>();
        foreach (var lname in linkName)
        {
            var xform = FindLink(lname);
            if (xform==null)
            {
                Debug.LogWarning($"Cound not find link:{lname}");
            }
            Debug.Log($"Found link {lname} transform pos:{xform.position} lpos:{xform.localPosition}");
            xformlist.Add(xform);
        }
        xform = xformlist;
        AddSkels(initialActiveStatus: false);
        moving = false;
        newRanPoint = false;
        performAction = false;
        oldState = ArmViewing.Skel;
        armViewing = ArmViewing.Meshes;
        if (actionIndex>nlinks)
        {
            actionIndex = 0;
        }
        actionAxis = rotAx[actionIndex];
        RealizeArmViewingState();
        for(var i=0; i<nlinks; i++)
        {
            SetAngle(i, curAng[i],quiet:true);
        }
    }

    Vector3 Vecax(RotAx rotax)
    {
        switch (rotax)
        {
            default:
            case RotAx.None: 
            case RotAx.XP: return new Vector3(+1, 0, 0);
            case RotAx.XN: return new Vector3(-1, 0, 0);
            case RotAx.YP: return new Vector3(0, +1, 0);
            case RotAx.YN: return new Vector3(0, -1, 0);
            case RotAx.ZP: return new Vector3(0, 0, +1);
            case RotAx.ZN: return new Vector3(0, 0, -1);
        }
    }

    Quaternion Quang(RotAx rotax,float angle)
    {
        switch (rotax)
        {
            default:
            case RotAx.None: return Quaternion.identity;
            case RotAx.XP: return Quaternion.Euler(+angle, 0, 0);
            case RotAx.XN: return Quaternion.Euler(-angle, 0, 0);
            case RotAx.YP: return Quaternion.Euler(0, +angle, 0);
            case RotAx.YN: return Quaternion.Euler(0, -angle, 0);
            case RotAx.ZP: return Quaternion.Euler(0, 0, +angle);
            case RotAx.ZN: return Quaternion.Euler(0, 0, -angle);
        }
        //var rv = Quaternion.AngleAxis(angle, Vecax(rotax));
        //return rv;
    }

    float ClipAng(float ang,float amin,float amax,int jidx)
    {
        var rang = ang;
        if (ang < amin)
        {
            Debug.Log($"{name} clipped {linkName[jidx]} from {ang} to {amin}");
            rang = amin;
        }
        if (ang > amax)
        {
            Debug.Log($"{name} clipped {linkName[jidx]} from {ang} to {amax}");
            rang = amax;
        }
        return rang;
    }
    public void SetAngle(string linkname, float oangle, bool quiet = false)
    {
        var jidx = jidxdict[linkname];
        SetAngle(jidx, oangle, quiet);
    }

    public void SetAngle(int jidx, float oangle,bool quiet=false)
    {
        if (jidx<0 || nlinks<=jidx)
        {
            Debug.LogWarning($"joint index out of range: {jidx} nlinks:{nlinks}");
            return;
        }
        var angle = jointSign[jidx] * (oangle - jointOrg[jidx]);
        var xf = xform[jidx];
        //var ang = Mathf.Min(jointMax[jidx], Mathf.Max(jointMin[jidx], angle));
        var ang = clipJoints ? ClipAng(angle, jointMin[jidx], jointMax[jidx], jidx) : angle;
        var brot = Quang(baseAxis[jidx], baseAng[jidx]);
        var qrot = Quang(rotAx[jidx],ang);
        curAng[jidx] = ang;
        xf.localRotation = brot*qrot;
        if (!quiet)
        {
            Debug.Log($"Set LocalRotation jidx:{jidx} ang:{ang} qrot:{qrot}");
        }
    }

    public void DeltAngle(int jidx, float oangle,bool quiet=false)
    {
        if (jidx < 0 || nlinks <= jidx)
        {
            Debug.LogWarning($"joint index out of range: {jidx} nlinks:{nlinks}");
            return;
        }
        var angle = jointSign[jidx] * oangle;
        var xf = xform[jidx];
        var ang = clipJoints ?  ClipAng(angle, jointMin[jidx], jointMax[jidx], jidx) : angle;
        var qrot = Quang(actionAxis, ang);
        curAng[jidx] += ang;
        xf.localRotation *= qrot;
        if (!quiet)
        {
            Debug.Log($"Inc/Dec LocalRotation  jidx:{jidx} ang:{ang} qrot:{qrot}");
        }
    }

    System.Random ranman = new System.Random(1234);

    public void SetupRandomPointToMoveTowards()
    {
        Debug.Log($"{name} generated new random point to move towards");
        for (var idx0 = 0; idx0 < nlinks; idx0++)
        {
            var ca = curAng[idx0];
            var sa = startAngles[idx0];
            var ta = targAngles[idx0];
            Debug.Log($"     before idx:{idx0}   ca:{ca:f1}   sa:{sa:f1}   ta:{ta:f1}");
        }
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
        for (var idx1=0; idx1<nlinks; idx1++)
        {
            var ca = curAng[idx1];
            var sa = startAngles[idx1];
            var ta = targAngles[idx1];
            Debug.Log($"     after idx:{idx1}   ca:{ca:f1}   sa:{sa:f1}   ta:{ta:f1}");
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
        for (int jix = 0; jix < nlinks; jix++)
        {
            var js = jointSign[jix];
            var newang = lamb * (js*targAngles[jix] - startAngles[jix]) + startAngles[jix];
            //var ln = linkName[jix];
            //SetAngle(ln, newang, quiet: true);
            SetAngle(i, newang,quiet:true);
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

    public void ActivateSkels(bool activeStatus)
    {
        foreach (var xf in xform)
        {
            if (xf != null)
            {
                var visgo = BreathFirstFindLink(xf, "Skels");
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
    public void AddSkels(bool initialActiveStatus)
    {
        var basexf = FindLink(baseLinkName);
        var parxf = basexf;
        foreach (var xf in xform)
        {
            var skelgo = new GameObject("Skels");
            skelgo.transform.SetParent(xf, worldPositionStays: false);

            var sz = 1f;
            var sz2 = sz/2;
            var basecub = MakeCub(skelgo.transform, "Pivot", Vector3.zero, sz, Color.white);
            var xfbc = basecub.transform;
            MakeCub(xfbc, "X", new Vector3(sz2, 0, 0), sz2, Color.red);
            MakeCub(xfbc, "Y", new Vector3(0, sz2, 0), sz2, Color.green);
            MakeCub(xfbc, "Z", new Vector3(0, 0, sz2), sz2, Color.blue);

            var csz = 0.02f;
            basecub.transform.localScale = new Vector3(csz,csz,csz);

            skelgo.SetActive(initialActiveStatus);
            skelgo.transform.SetAsFirstSibling();
            parxf = xf;
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
                    ActivateSkels(false);
                    ActivateVisuals(true);
                    break;
                case ArmViewing.Skel:
                    ActivateSkels(true);
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
        else if (name.StartsWith("ur10") || name.StartsWith("ur5") || name.StartsWith("ur3"))
        {
            Init(ArmTyp.URFAM);
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
            DeltAngle(actionIndex, actionAngle);
            performIncAction = false;
        }

        if (performDecAction)
        {
            DeltAngle(actionIndex, -actionAngle);
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
