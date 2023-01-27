using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KinArm : MonoBehaviour
{
    public enum RotAx { XP,XN,YP,YN,ZP,ZN }
    public enum ArmTyp { RS007N, RS007L }

    public enum ArmViewing { Meshes, Pivots }


    [Header("Viewing")]
    public ArmViewing armViewing;

    [Header("TestActions")]
    public bool performAction;
    public int actionIndex;
    public float actionAngle;

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
    float[] jointMin;
    float[] jointMax;
    float[] curAng;

    Transform[] xform;


    Transform FindLink(string linkname)
    {
        var go = GameObject.Find(linkname);
        return go.transform;
    }

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
                    linkName = new string[] { "link1piv", "link2piv", "link3piv", "link4piv", "link5piv", "link6piv" };
                    rotAx = new RotAx[] { RotAx.YP, RotAx.ZP, RotAx.ZP, RotAx.YP, RotAx.ZP, RotAx.YP };
                    jointMin = new float[] { -360, -90, -90, -360, -90, -360 };
                    jointMax = new float[] {  360,  90, 90,   360,  90,  360 };
                    curAng = new float[] { 0, 0, 0, 0, 0, 0 };
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
        moving = false;
        newRanPoint = false;
        performAction = false;
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
        var qrot = Quang(rotAx[jidx], ang);
        curAng[jidx] = ang;
        xf.localRotation = qrot;
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

    private void Start()
    {
        Init(ArmTyp.RS007N);
    }

    private void Update()
    {
        if (performAction)
        {
            SetAngle(actionIndex, actionAngle);
            performAction = false;
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
    }
}
