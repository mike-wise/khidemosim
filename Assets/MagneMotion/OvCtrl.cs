using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum OvTransCtrl { Correct, NoTranspose, OnlyLeftT, OnlyRightT, NoT, NoTandNoTranspose }

public class OvCtrl : MonoBehaviour
{
    // Start is called before the first frame update
    [Header("State")]
    public bool OV_UseFixedJsonStateFile;
    public string OV_FixedJsonStateFileName = "rundir";
    public string OV_JsonStateFile;
    public string OV_Rootname;
    public string OV_ActiveObjects;
    public string OV_FlattenObjects;
    public string OV_RunStateFolder;
    public float OV_StateListSimTime;
    public bool OV_StateListWritePending;
    public int OV_StateListIndex;
    public int OV_skipcount;
    public List<string> OV_StateList;
    public bool isFlattend = false;
    public OvTransCtrl ovTransformControl = OvTransCtrl.Correct;


    [Header("Actions")]
    public bool flatten;
    public bool restore;

    public void Init(string rootname)
    {
        this.OV_Rootname = rootname;
        this.OV_UseFixedJsonStateFile = true;
        this.OV_skipcount = 5;
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
            SetupStateFolderForRun();
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
            ostate.Init(pathname, this, ovp);
            var ostr = ostate.MakeJsonString();
            File.AppendAllText(OV_JsonStateFile, ostr+nl);
            AddStringToStateList(ostr);
            // Debug.Log($"OV Added {pathname}");
        }
    }


    public string GetNewStateFolderName()
    {

        var runnum = 0;
        string newname;
        while (true)
        {
            newname = $"JsonState/run_{runnum:D3}/";
            if (!System.IO.Directory.Exists(newname))
            {
                System.IO.Directory.CreateDirectory(newname);
                return newname;
            }
            runnum++;
            if (runnum>10000)
            {
                break;
            }
        }
        return "";
    }

    public void InitStateList()
    {
        this.OV_StateListWritePending = true;
        this.OV_StateList = new List<string>();
        var s = $"{{\"ftyp\":\"StateList\",\"index\":{this.OV_StateListIndex},\"now\":\"{System.DateTime.Now}\",\"simtime\":{Time.time}}}";
        OV_StateList.Add(s);
        OV_StateListSimTime = Time.time;
    }

    public void FlushStateList()
    {
        // note we are using "json lines" formatting
        if (this.OV_skipcount==0)
        {
            this.OV_skipcount = 1;
        }
        if (this.OV_StateListIndex % this.OV_skipcount == 0)
        {
            var fname = this.OV_RunStateFolder + $"timestep_{this.OV_StateListIndex:D6}.jsonl";
            File.WriteAllLines(fname, this.OV_StateList);
        }
        this.OV_StateList = new List<string>();
        this.OV_StateListIndex++;
        InitStateList();
    }

    public void EnsureExistingAndEmptyDirectory(string dirname)
    {
        if (!System.IO.Directory.Exists(dirname))
        {
            System.IO.Directory.CreateDirectory(dirname);
        }

        // Now empty the directory of any existing files or subdires
        var di = new DirectoryInfo(dirname);

        foreach (var file in di.GetFiles())
        {
            file.Delete();
        }
        foreach (var subdir in di.GetDirectories())
        {
            subdir.Delete(true);
        }
    }

    public void SetupStateFolderForRun()
    {
        if (OV_UseFixedJsonStateFile)
        {
            this.OV_RunStateFolder = $"JsonState/{this.OV_FixedJsonStateFileName}/";
            EnsureExistingAndEmptyDirectory(this.OV_RunStateFolder);
        }
        else
        {
            this.OV_RunStateFolder = GetNewStateFolderName();
        }
        this.OV_StateListIndex = 0;
        InitStateList();
    }

    public void AddStringToStateList(string line)
    {
        if (Time.time!=this.OV_StateListSimTime)
        {
            FlushStateList();
            this.OV_StateListSimTime = Time.time;
        }
        this.OV_StateList.Add(line);
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

    void FixedUpdate()
    {
    }

}
