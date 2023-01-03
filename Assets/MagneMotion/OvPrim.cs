using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OvPrim : MonoBehaviour
{
    // Start is called before the first frame update
    OvCtrl ovctrl;
    public bool pubOvAlways = false;
    public bool pubOvOnce = false;

    public int npub = 0;

    public Vector3 lastPos;
    public Quaternion lastRot;

    public Matrix4x4 lastLocalToWorld;
    public Matrix4x4 lastLocalToWorldParentTransRemoved;

    string typ;
    string typlc;

    Transform nonFlattenedParent;
    public bool isflattend = false;
    public bool turnGravityBackOn = false;

    void Start()
    {
        ovctrl = FindObjectOfType<OvCtrl>();
    }

    public string GetTypLc()
    {
        return typlc;
    }
    public string GetTyp()
    {
        return typ;
    }

    public void Init(string typ)
    {
        this.typ = typ;
        typlc = typ.ToLower();
    }

    public string GetPathName()
    {
        var pname = $"{name}";
        //if (name == "FakeBox_01")
        //{
        //    Debug.Log("asdf");
        //}
        var gop = transform.parent;
        while (gop != null)
        {
            pname = $"{gop.name}/{pname}";
            if (gop.name == ovctrl.OV_Rootname) break;
            gop = gop.transform.parent;
        }
        var pathname = $"/{pname}";
        return pathname;
    }



        static float filterToZero(float f)
    {
        if (Mathf.Abs(f) < 1e-6)
        {
            return 0;
        }
        return f;
    }
    public static Vector3 FilterVector(Vector3 v)
    {
        var rv = new Vector3(filterToZero(v.x), filterToZero(v.y), filterToZero(v.z));
        return rv;
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

    public static float[,] FiltMat(Matrix4x4 m)
    {
        var rv = new float[4,4];

        rv[0,0] = filterToZero(m.m00);
        rv[0,1] = filterToZero(m.m01);
        rv[0,2] = filterToZero(m.m02);
        rv[0,3] = filterToZero(m.m03);

        rv[1,0] = filterToZero(m.m10);
        rv[1,1] = filterToZero(m.m11);
        rv[1,2] = filterToZero(m.m12);
        rv[1,3] = filterToZero(m.m13);

        rv[2,0] = filterToZero(m.m20);
        rv[2,1] = filterToZero(m.m21);
        rv[2,2] = filterToZero(m.m22);
        rv[2,3] = filterToZero(m.m23);

        rv[3,0] = filterToZero(m.m30);
        rv[3,1] = filterToZero(m.m31);
        rv[3,2] = filterToZero(m.m32);
        rv[3,3] = filterToZero(m.m33);
        return rv;
    }

    public static List<float> FiltMatToList(Matrix4x4 m)
    {
        var rv = new List<float>();

        rv.Add(filterToZero(m.m00));
        rv.Add(filterToZero(m.m01));
        rv.Add(filterToZero(m.m02));
        rv.Add(filterToZero(m.m03));

        rv.Add(filterToZero(m.m10));
        rv.Add(filterToZero(m.m11));
        rv.Add(filterToZero(m.m12));
        rv.Add(filterToZero(m.m13));

        rv.Add(filterToZero(m.m20));
        rv.Add(filterToZero(m.m21));
        rv.Add(filterToZero(m.m22));
        rv.Add(filterToZero(m.m23));

        rv.Add(filterToZero(m.m30));
        rv.Add(filterToZero(m.m31));
        rv.Add(filterToZero(m.m32));
        rv.Add(filterToZero(m.m33));
        return rv;
    }

    public static float [] FiltMatToArray(Matrix4x4 m)
    {
        var rv = FiltMatToList(m);
        return rv.ToArray();
    }


    bool CheckMoved()
    {
        var t = transform;
        var rv = (npub == 0);
        if (npub >= 0)
        {
            if (!Vector3.Equals(lastPos, t.position))
            {
                rv = true;
            }
            if (!Quaternion.Equals(lastRot, t.rotation))
            {
                rv = true;
            }
        }
        lastPos = t.position;
        lastRot = t.rotation;
        lastLocalToWorld = FilterMatrix(transform.localToWorldMatrix);
        lastLocalToWorldParentTransRemoved = FilterMatrix(transform.parent.localToWorldMatrix.inverse * transform.localToWorldMatrix);

        return rv;
    }

    public void Flatten(Transform roottransform)
    {
        if (!isflattend)
        {
            nonFlattenedParent = transform.parent;
            transform.SetParent(roottransform, worldPositionStays: true);
            var rigbod = gameObject.GetComponent<Rigidbody>();
            //if (name=="link1")
            //{
            //    Debug.Log("link1");
            //}
            if (rigbod!=null)
            {
                if (rigbod.useGravity)
                {
                    rigbod.useGravity = false;
                    turnGravityBackOn = true;
                }
            }
            var artbod = gameObject.GetComponent<ArticulationBody>();
            if (artbod != null)
            {
                if (artbod.useGravity)
                {
                    artbod.useGravity = false;
                    turnGravityBackOn = true;
                }
            }
        }
        isflattend = true;
    }
    public void Restore()
    {
        if (isflattend)
        {
            transform.SetParent(nonFlattenedParent, worldPositionStays: true);
            nonFlattenedParent = null;
            if (turnGravityBackOn)
            {
                var rigbod = gameObject.GetComponent<Rigidbody>();
                if (rigbod != null)
                {
                    rigbod.useGravity = true;
                }
                var artbod = gameObject.GetComponent<ArticulationBody>();
                if (artbod != null)
                {
                    artbod.useGravity = true;
                }
                turnGravityBackOn = false;
            }
        }
        isflattend = false;
    }

    void FixedUpdate()
    {
        if (ovctrl != null)
        {
            if (ovctrl.IsOvActive(this) || pubOvAlways || pubOvOnce)
            {
                if (CheckMoved())
                {
                    ovctrl.PublishState(this);
                }
                npub++;
                pubOvOnce = false;
            }
        }
    }
}

public class MmOvObj
{
    public string ovcls;
    public string typ;
    public string pathname;
    public string now;
    public string simtime;
    public string extras;
    public string ovtransctrl; 
    public Vector3 localscale;
    public Vector3 eulerangles;
    public Vector3 loceulerangles;
    public Vector3 position;
    public Vector3 locposition;
    public Matrix4x4 loctrans;
    public Matrix4x4 loctowctrans;

    public void Init(string pathname, OvCtrl ovc, OvPrim ovp)
    {
        var t = ovp.transform;
        this.ovcls = "MmOvObj";
        this.typ = ovp.GetTyp();
        this.pathname = pathname;
        this.extras = "";
        if (ovc.IsOvNeededFlat(ovp))
        {
            this.extras = "flattened";
        }
        this.now = System.DateTime.Now.ToString("O");
        this.simtime = Time.time.ToString("F6");
        this.localscale = OvPrim.FilterVector(t.localScale);
        this.eulerangles = OvPrim.FilterVector(t.rotation.eulerAngles);
        this.loceulerangles = OvPrim.FilterVector(t.localRotation.eulerAngles);
        this.position = OvPrim.FilterVector(t.position);
        this.locposition = OvPrim.FilterVector(t.localPosition);
        this.ovtransctrl = ovc.ovTransformControl.ToString();
        if (t.parent != null)
        {
            this.loctrans = OvPrim.FilterMatrix(t.parent.localToWorldMatrix.inverse * t.localToWorldMatrix);
        }
        else
        {
            this.loctrans = OvPrim.FilterMatrix(t.localToWorldMatrix);
        }
        this.loctowctrans = OvPrim.FilterMatrix(t.localToWorldMatrix);
    }
    public string MakeJsonString()
    {
        var s = JsonUtility.ToJson(this);
        return s;
    }
    public string MakeJsonNetString()
    {
        var s = Newtonsoft.Json.JsonConvert.SerializeObject(this);
        return s;
    }
}
