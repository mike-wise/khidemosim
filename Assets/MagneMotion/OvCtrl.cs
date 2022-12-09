using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class OvCtrl : MonoBehaviour
{
    // Start is called before the first frame update
    [Header("State")]

    public string OV_JsonStateFile;
    public string OV_Rootname;
    public string OV_ActiveObjects;
    public string OV_FlattenObjects;
    public bool isFlattend = false;


    [Header("Actions")]
    public bool flatten;
    public bool restore;

    public void Init(string rootname)
    {
        this.OV_Rootname = rootname;
    }

    bool jsonStateFileInited = false;
    string nl = Environment.NewLine;
    public bool InitJsonStateFile()
    {
        if (!jsonStateFileInited)
        {
            try
            {
                var ds = DateTime.Now.ToString("s");
                ds = ds.Replace(':', '-');
                OV_JsonStateFile = $"JsonState/gobs-{ds}.jfm";
                jsonStateFileInited = true;
                File.AppendAllText(OV_JsonStateFile, $"# JsonStateFile {ds}{nl}");
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.Message);
                return false;
            }
        }
        return true;
    }

    public bool IsOvActive(OvPrim ovp)
    {
        var rv = OV_ActiveObjects.Contains(ovp.GetTypLc());
        return rv;
    }

    public bool IsOvNeededFlat(OvPrim ovp)
    {
        var rv = OV_FlattenObjects.Contains(ovp.GetTypLc());
        return rv;
    }

    //public string GetPathName(OvPrim ovp)
    //{
    //    var pname = $"{ovp.name}";
    //    if (ovp.name == "FakeBox_01")
    //    {
    //        Debug.Log("asdf");
    //    }
    //    var gop = ovp.transform.parent;
    //    while (gop != null)
    //    {
    //        pname = $"{gop.name}/{pname}";
    //        if (gop.name == OV_Rootname) break;
    //        gop = gop.transform.parent;
    //    }
    //    var pathname = $"/{pname}";
    //    return pathname;
    //}
    public void PublishState(OvPrim ovp)
    {
        var ok = InitJsonStateFile();
        if (ok)
        {
            var pathname = ovp.GetPathName();
            if (IsOvNeededFlat(ovp))
            {
                pathname = $"/{OV_Rootname}/{ovp.name}";
            }
            var ostate = new MmOvObj();
            ostate.Init(pathname, ovp);
            var ostr = ostate.MakeJsonString();
            File.AppendAllText(OV_JsonStateFile, ostr+nl);
            // Debug.Log($"OV Added {pathname}");
        }
    }

    public void Flatten()
    {
        var rootObject = GameObject.Find(OV_Rootname);

        if (rootObject==null)
        {
            Debug.LogError($"OV Rootobject:{OV_Rootname} could not be found - can not flatten");
            return;
        }
        var roottransform = rootObject.transform;
        var ovprimlist = FindObjectsOfType<OvPrim>();
        foreach (var ovp in ovprimlist)
        {
            if (IsOvNeededFlat(ovp))
            {
                ovp.Flatten(roottransform);
            }
        }
        isFlattend = true;
    }

    public void Restore()
    {
        var ovprimlist = FindObjectsOfType<OvPrim>();
        foreach (var ovp in ovprimlist)
        {
            ovp.Restore();
        }
        isFlattend = false;
    }

    void Update()
    {
        if (flatten)
        {
            Flatten();
            flatten = false;
        }
        if (restore)
        {
            Restore();
            restore = false;
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
    public Vector3 localscale;
    public Vector3 eulerangles;
    public Vector3 loceulerangles;
    public Vector3 position;
    public Vector3 locposition;
    public Matrix4x4 loctrans;
    public Matrix4x4 loctowctrans;

    public void Init(string pathname, OvPrim ovp)
    {
        var t = ovp.transform;
        this.ovcls = "MmOvObj";
        this.typ = ovp.GetTyp();
        this.pathname = pathname;
        this.now = DateTime.Now.ToString("O");
        this.simtime = Time.time.ToString("F6");
        this.localscale = OvPrim.FilterVector(t.localScale);
        this.eulerangles = OvPrim.FilterVector(t.rotation.eulerAngles);
        this.loceulerangles = OvPrim.FilterVector(t.localRotation.eulerAngles);
        this.position = OvPrim.FilterVector(t.position);
        this.locposition = OvPrim.FilterVector(t.localPosition);
        if (t.parent != null)
        {
            this.loctrans = OvPrim.FilterMatrix( t.parent.localToWorldMatrix.inverse * t.localToWorldMatrix );
        }
        else
        {
            this.loctrans =  OvPrim.FilterMatrix(t.localToWorldMatrix);
        }
        this.loctrans = OvPrim.FilterMatrix(t.localToWorldMatrix);
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

