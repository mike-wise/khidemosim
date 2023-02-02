using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KinUrdf : MonoBehaviour
{
    public enum RotAx { None, XP, XN, YP, YN, ZP, ZN }
    public enum ArmTyp { None, KHI_family, UR_family }

    public enum ArmViewing { Meshes, Skel }

    #region Visible Members

    [Header("Arm Type")]
    public ArmTyp armTyp;

    [Header("Behavior")]
    public bool clipJoints = true;

    [Header("Viewing")]
    public ArmViewing armViewing;

    [Header("Single Joint Test Actions")]
    public bool performAction;
    public int actionIndex;
    public RotAx actionAxis;
    public float actionAngle;
    public bool performIncAction;
    public bool performDecAction;

    [Header("Test Robot Movment")]
    public bool newRanPointForMovement;
    public bool continuousMove;
    public KinArmCtrl.MoveStyles moveStyle;
    public int currentMovePose;
    public List<RobotPose> movePoses;
    public RobotPoses robpose;

    [Header("Internal")]
    public int nlinks;
    public string eeLinkName;
    public string worldLinkName;
    public string baseLinkName;
    public string[] linkName;
    #endregion

    #region Private Members
    bool moving;
    float[] startAngles;
    float[] targAngles;
    float startTime;
    float moveTime;


    RotAx[] rotAx;
    float[] jointMin;
    float[] jointMax;
    float[] jointOrg;
    float[] jointSign;
    public float[] curAng;
    float[] baseAng;
    RotAx[] baseAxis;
    List<Transform> xform;

    // Dictionary for mapping strings to integers for easy access to joints
    Dictionary<string, int> jidxdict;
    #endregion

    #region Utilities

    static Transform BreathFirstFindLink(Transform parent, string seekName, int depth = 0, int nchk = 0)
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


    static Transform FindLink(Transform parent, string linkname)
    {
        // var go = GameObject.Find(linkname);
        var go = BreathFirstFindLink(parent, linkname);
        if (go == null)
        {
            return null;
        }
        return go.transform;
    }

    static Vector3 Vecax(RotAx rotax)
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

    static Quaternion Quang(RotAx rotax, float angle)
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

    static float ClipAng(float ang, float amin, float amax, string name, string linkname, bool quiet = true)
    {
        var rang = ang;
        if (ang < amin)
        {
            if (!quiet)
            {
                Debug.Log($"{name} min clipped {linkname} from {ang} to {amin}");
            }
            rang = amin;
        }
        if (ang > amax)
        {
            if (!quiet)
            {
                Debug.Log($"{name} max clipped {linkname} from {ang} to {amax}");
            }
            rang = amax;
        }
        return rang;
    }

    #endregion

    #region Purge Urdf
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
            DestroyComponent<Unity.Robotics.UrdfImporter.Control.FKRobot>(go);
            var visxf = go.transform.Find("Visuals");
            if (visxf != null)
            {
                DestroyComponent<Unity.Robotics.UrdfImporter.UrdfVisuals>(visxf.gameObject);
            }
            var colxf = go.transform.Find("Collisions");
            if (colxf != null)
            {
                DestroyComponent<Unity.Robotics.UrdfImporter.UrdfCollisions>(colxf.gameObject);
            }
            var basexf = go.transform.Find("base");
            if (basexf != null)
            {
                DestoryUrdfComponentsGo(basexf.gameObject);
            }
            var plugxf = go.transform.Find("Plugins");
            if (plugxf != null)
            {
                Destroy(plugxf.gameObject);
            }
            var tool0xf = go.transform.Find("tool0");
            if (tool0xf != null)
            {
                Destroy(tool0xf.gameObject);
            }

        }
    }

    void DestroyUrdfComponentsGoName(string linkname)
    {
        var t = FindLink(transform,linkname);
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
    #endregion

    #region Core Code
    public void Init()
    {
        curAng = new float[] { 0, 0, 0, 0, 0, 0 };
        startAngles= new float[] { 0, 0, 0, 0, 0, 0 };
        targAngles = new float[] { 0, 0, 0, 0, 0, 0 };
        if (armTyp==ArmTyp.None)
        {
            Debug.LogWarning("Armtype not specified assuming KHIfamily");
            armTyp = ArmTyp.KHI_family;
        }    
        switch (armTyp)
        {
            default:
            case ArmTyp.KHI_family:
                {
                    nlinks = 6;
                    baseLinkName = "base_link";
                    worldLinkName = "world";
                    eeLinkName = "tool_link";
                    linkName = new string[] { "link1", "link2", "link3", "link4", "link5", "link6" };
                    //rotAx = new RotAx[] { RotAx.YP, RotAx.YP, RotAx.YP, RotAx.YP, RotAx.YP, RotAx.YP };
                    rotAx = new RotAx[] { RotAx.YP, RotAx.YN, RotAx.YP, RotAx.YN, RotAx.YP, RotAx.YN };
                    jointMin = new float[] { -360, -90, -90, -360, -90, -360 };
                    jointMax = new float[] { +360, +90, +90, +360, +90, +360 };
                    jointOrg = new float[] { 0, 0, 0, 0, 0, 0 };
                    //jointSign = new float[] { -1, 1, -1, 1, -1, 1 };
                    jointSign = new float[] { 1, 1, 1, 1, 1, 1 };
                    //jointMin = new float[] { -360, -270, -310, -400, -250, -720 }; // from rs007n.urdf
                    //jointMax = new float[] { +360, +270, +310, +400, +250, +720 };
                    baseAxis = new RotAx[] { RotAx.None, RotAx.XP, RotAx.None, RotAx.XP, RotAx.XP, RotAx.XP };
                    baseAng = new float[] { 0, 270, 0, 90, 270, 270 };
                    break;
                }
            case ArmTyp.UR_family:
                {
                    nlinks = 6;
                    baseLinkName = "base_link";
                    worldLinkName = "world";
                    eeLinkName = "ee_link";
                    linkName = new string[] { "shoulder_link", "upper_arm_link", "forearm_link", "wrist_1_link", "wrist_2_link", "wrist_3_link" };
                    rotAx = new RotAx[] { RotAx.YP, RotAx.XP, RotAx.XP, RotAx.XP, RotAx.YN, RotAx.XP };
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
        InactivateUrdf();// purge urdf components
        var xformlist = new List<Transform>();
        foreach (var lname in linkName)
        {
            var xform = FindLink(transform,lname);
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
        newRanPointForMovement = false;
        performAction = false;
        oldState = ArmViewing.Skel;
        armViewing = ArmViewing.Meshes;
        moveTime = 5;
        if (actionIndex>nlinks)
        {
            actionIndex = 0;
        }
        actionAxis = rotAx[actionIndex];
        RealizeArmViewingState();
        for(var i=0; i<nlinks; i++)
        {
            SetAngleDegrees(i, curAng[i],quiet:true);
        }
    }


    public void SetAnglRadians(string linkname, float odeltangle, bool quiet = false)
    {
        SetAngleDegrees(linkname, odeltangle * 180f / Mathf.PI, quiet);
    }

    public void SetAnglRadians(int jidx, float odeltangle, bool quiet = false)
    {
        SetAngleDegrees(jidx, odeltangle * 180f / Mathf.PI, quiet);
    }

    public void SetAngleDegrees(string linkname, float oangle, bool quiet = false)
    {
        var jidx = jidxdict[linkname];
        SetAngleDegrees(jidx, oangle, quiet);
    }

    public void SetAngleDegrees(int jidx, float oangle,bool quiet=false)
    {
        //Debug.Log($"SetAngle {jidx} angle:{oangle}");
        if (jidx<0 || nlinks<=jidx)
        {
            Debug.LogWarning($"joint index out of range: {jidx} nlinks:{nlinks}");
            return;
        }
        var angle = jointSign[jidx] * (oangle - jointOrg[jidx]);
        var xf = xform[jidx];
        //var ang = Mathf.Min(jointMax[jidx], Mathf.Max(jointMin[jidx], angle));
        var ang = clipJoints ? ClipAng(angle, jointMin[jidx], jointMax[jidx], name,linkName[jidx]) : angle;
        var brot = Quang(baseAxis[jidx], baseAng[jidx]);
        var qrot = Quang(rotAx[jidx],ang);
        curAng[jidx] = oangle;
        xf.localRotation = brot*qrot;
        if (!quiet)
        {
            Debug.Log($"Set LocalRotation jidx:{jidx} ang:{ang} qrot:{qrot}");
        }
    }
    public void DeltAnglRadians(string linkname, float odeltangle, bool quiet = false)
    {
        DeltAngleDegrees(linkname, odeltangle * 180f / Mathf.PI, quiet);
    }

    public void DeltAnglRadians(int jidx, float odeltangle, bool quiet = false)
    {
        DeltAngleDegrees(jidx, odeltangle * 180f / Mathf.PI, quiet);
    }

    public void DeltAngleDegrees(string linkname, float odeltangle, bool quiet = false)
    {
        var jidx = jidxdict[linkname];
        DeltAngleDegrees(jidx, odeltangle, quiet);
    }
    public void DeltAngleDegrees(int jidx, float odeltangle,bool quiet=false)
    {
        if (jidx < 0 || nlinks <= jidx)
        {
            Debug.LogWarning($"joint index out of range: {jidx} nlinks:{nlinks}");
            return;
        }
        var deltang = jointSign[jidx] * odeltangle;
        var xf = xform[jidx];
        var deltangle = clipJoints ?  ClipAng(deltang, jointMin[jidx], jointMax[jidx], name, linkName[jidx]) : deltang;
        //var qrot = Quang(actionAxis, ang);
        var qrot = Quang(rotAx[jidx], deltangle);
        curAng[jidx] += odeltangle;
        xf.localRotation *= qrot;
        if (!quiet)
        {
            Debug.Log($"Inc/Dec LocalRotation  jidx:{jidx} deltang:{deltangle} curAng:{curAng[jidx]} qrot:{qrot}");
        }
    }
    #endregion

    #region Test Movment Code
    public void SetupMovePoseSequenceSingle(RobotPoses robpose, List<RobotPose> movePoses,int posenum)
    {
        var p = movePoses[posenum];
        Debug.Log($"{name} taking pose {p}");
        targAngles = robpose.GetPose(p);
        for (int i = 0; i < nlinks; i++)
        {
            //Debug.Log($"MoveToRandomPoint i:{i}");
            startAngles[i] = curAng[i];
        }
    }


    public void SetupMovePoseSequence(RobotPoses robpose,List<RobotPose> movePoses,int movetime=5)
    {
        moveStyle = KinArmCtrl.MoveStyles.rail2rail;
        this.movePoses = new List<RobotPose>(movePoses);
        this.robpose = robpose;
        currentMovePose = 0;
        if (targAngles == null)
        {
            targAngles = new float[nlinks];
            startAngles = new float[nlinks];
        }
        moveTime = movetime;
        SetupMovePoseSequenceSingle(robpose, movePoses, currentMovePose);
    }

    System.Random ranman = new System.Random(1234);

    public void SetupRandomPointToMoveTowards()
    {
        moveStyle = KinArmCtrl.MoveStyles.random;
        Debug.Log($"{name} generated new random point to move towards");
        for (var idx0 = 0; idx0 < nlinks; idx0++)
        {
            var ca = curAng[idx0];
            var sa = startAngles[idx0];
            var ta = targAngles[idx0];
            Debug.Log($"     before idx:{idx0}   ca:{ca:f1}   sa:{sa:f1}   ta:{ta:f1}");
        }
        if (targAngles == null)
        {
            targAngles = new float[nlinks];
            startAngles = new float[nlinks];
        }
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
    }

    public void StartMovment()
    {
        startTime = Time.time;
        moving = true;
    }

    public void StopMovement()
    {
        moving = false;
    }

    public void Moveit()
    {
        var delttime = Time.time - startTime;
        var lamb = delttime / moveTime;
        // Debug.Log($"KinUrdf - Moveit time:{Time.time} delt:{delttime} lamb:{lamb}");
        if (lamb >= 1)
        {
            moving = false;
            lamb = 1;
        }
        for (int jix = 0; jix < nlinks; jix++)
        {
            var newang = lamb * (targAngles[jix] - startAngles[jix]) + startAngles[jix];
            var js = jointSign[jix];
            var delt = newang - curAng[jix];
            //var ln = linkName[jix];
            //DeltAngle(ln, newang, quiet: true);
            DeltAngleDegrees(jix, js*delt,quiet:true);
        }
        if (!moving && continuousMove)
        {
            switch (moveStyle)
            {
                case KinArmCtrl.MoveStyles.random:
                    SetupRandomPointToMoveTowards();
                    break;
                case KinArmCtrl.MoveStyles.rail2rail:
                    currentMovePose = (currentMovePose + 1) % movePoses.Count;
                    SetupMovePoseSequenceSingle(robpose,movePoses,currentMovePose);
                    break;
            }
            startTime = Time.time;
            moving = true;
        }
    }
    #endregion

    #region Alternate Visuals Code
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
        var basexf = FindLink(transform,baseLinkName);
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
    #endregion

    #region Unity Events
    private void Start()
    {
        Init();
    }

    float lastReportTime = 0;
    private void Update()
    {
        // Single Joint Test Actions
        if (performAction)
        {
            SetAngleDegrees(actionIndex, actionAngle);
            performAction = false;
        }

        if (performIncAction)
        {
            DeltAngleDegrees(actionIndex, actionAngle);
            performIncAction = false;
        }

        if (performDecAction)
        {
            DeltAngleDegrees(actionIndex, -actionAngle);
            performDecAction = false;
        }

        // Test Movement
        if (newRanPointForMovement)
        {
            SetupRandomPointToMoveTowards();
            StartMovment();
            newRanPointForMovement = false;
        }

        if (moving)
        {
            Moveit();
            if (Time.time - lastReportTime >= 2)
            {
                var msg = $"{name}";
                foreach (var ca in curAng)
                {
                    msg += $" {ca:f1}";
                }
                Debug.Log(msg);
                lastReportTime = Time.time;
            }
        }
        RealizeArmViewingState();
    }
    #endregion
}
