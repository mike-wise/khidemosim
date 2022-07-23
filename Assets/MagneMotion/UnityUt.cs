using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnityUt : MonoBehaviour
{
    static Dictionary<string, string> hexColorTable = null;
    static Dictionary<string, Color> colorTable = null;
    static Dictionary<string, string> colorOrigin = null;
    public static int colordups = 0;
    public static int colorconflicts = 0;

    public static bool IsColorName(string name)
    {
        if (colorTable == null)
        {
            InitColorTable();
        }
        return colorTable.ContainsKey(name);
    }

    static bool ClrEqual(Color c1, Color c2)
    {
        if (c1.r != c2.r) return false;
        if (c1.g != c2.g) return false;
        if (c1.b != c2.b) return false;
        return true;
    }

    public static void AddCoreColor(string cname, Color clr)
    {
        if (colorTable.ContainsKey(cname))
        {
            var oldclr = colorTable[cname];
            if (ClrEqual(clr, oldclr))
            {
                colordups++;
                return;
            }
            colorconflicts++;
            //Debug.LogWarning($"Duplicate color {cname}"); // to be expected
            return;
        }
        colorTable[cname] = clr;
        colorOrigin[cname] = "core";
    }


    public static void AddColor(string cname, Color clr)
    {
        if (colorTable.ContainsKey(cname))
        {
            //Debug.LogWarning($"Duplicate color {cname}"); // to be expected
            var oldclr = colorTable[cname];
            if (ClrEqual(clr, oldclr))
            {
                colordups++;
                return;
            }
            colorconflicts++;
            return;
        }
        colorTable[cname] = clr;
        colorOrigin[cname] = "adhoc";
    }



    public static void InitCoreColors()
    {
        AddCoreColor("red", Color.red);
        AddCoreColor("green", Color.green);
        AddCoreColor("blue", Color.blue);
        AddCoreColor("gray", Color.gray);
        AddCoreColor("grey", Color.grey);
        AddCoreColor("cyan", Color.cyan);
        AddCoreColor("magenta", Color.magenta);
        AddCoreColor("yellow", Color.yellow);
        AddCoreColor("white", Color.white);
        AddCoreColor("black", Color.black);
        AddCoreColor("clear", Color.clear);
    }
    public static Color RgbByte(int r, int g, int b, float alpha = 1)
    {
        return new Color(r / 255f, g / 255f, b / 255f, alpha);
    }
    public static void InitColorTable()
    {

        colorTable = new Dictionary<string, Color>();
        colorOrigin = new Dictionary<string, string>();
        InitCoreColors();
        // InitXkcdColors();
        // reds
        AddColor("r", new Color(1, 0, 0));
        AddColor("red", new Color(1, 0, 0));
        AddColor("dr", new Color(0.5f, 0, 0));
        AddColor("crimsom", RgbByte(220, 20, 60));
        AddColor("coral", RgbByte(255, 127, 80));
        AddColor("firebrick", RgbByte(178, 34, 34));
        AddColor("darkred", RgbByte(139, 0, 0));
        AddColor("dirtyred", RgbByte(117, 10, 10));
        AddColor("lightred", new Color(1, 0.412f, 0.71f));
        AddColor("pink", new Color(1, 0.412f, 0.71f));
        AddColor("scarlet", new Color(1, 0.14f, 0.0f));
        // yellows
        AddColor("y", new Color(1, 1, 0));
        AddColor("yellow", new Color(1, 1, 0));
        AddColor("dy", new Color(0.5f, 0.5f, 0));
        AddColor("lightyellow", new Color(1, 1, 0.5f));
        AddColor("goldenrod", RgbByte(218, 165, 32));
        // oranges
        AddColor("orange", new Color(1, 0.5f, 0));
        AddColor("lightorange", new Color(1, 0.75f, 0));
        AddColor("darkorange", new Color(0.75f, 0.25f, 0));
        AddColor("brown", new Color(0.647f, 0.164f, 0.164f));
        AddColor("saddlebrown", new Color(0.545f, 0.271f, 0.075f));
        AddColor("darkbrown", new Color(0.396f, 0.263f, 0.129f));
        // greens
        AddColor("g", new Color(0.5f, 1, 0.5f));
        AddColor("lightgreen", new Color(0.5f, 1, 0.5f));
        AddColor("green", new Color(0, 1, 0));
        AddColor("dg", new Color(0, 0.5f, 0));
        AddColor("olive", new Color(0.5f, 0.5f, 0f));
        AddColor("darkgreen", new Color(0, 0.5f, 0));
        AddColor("darkgreen1", new Color(0.004f, 0.196f, 0.125f));
        AddColor("forestgreen", new Color(0.132f, 0.543f, 0.132f));
        AddColor("limegreen", new Color(0.195f, 0.8f, 0.195f));
        AddColor("seagreen", new Color(0.33f, 1.0f, 0.62f));
        // cyans
        AddColor("c", new Color(0, 1, 1));
        AddColor("cyan", new Color(0, 1, 1));
        AddColor("dc", new Color(0, 0.5f, 0.5f));
        AddColor("turquoise", RgbByte(64, 224, 208));
        AddColor("turquis", RgbByte(64, 224, 208));
        AddColor("teal", RgbByte(0, 128, 128));
        AddColor("aquamarine", RgbByte(128, 255, 212));
        // blues
        AddColor("b", new Color(0, 0, 1));
        AddColor("blue", new Color(0, 0, 1));
        AddColor("db", new Color(0, 0, 0.5f));
        AddColor("steelblue", new Color(0.27f, 0.51f, 0.71f));
        AddColor("lightblue", RgbByte(173, 216, 230));
        AddColor("azure", RgbByte(0, 127, 255));
        AddColor("skyblue", RgbByte(135, 206, 235));
        AddColor("darkblue", new Color(0.0f, 0.0f, 0.500f));
        AddColor("navyblue", new Color(0.0f, 0.0f, 0.398f));
        // purples
        AddColor("m", new Color(1, 0, 1));
        AddColor("magenta", new Color(1, 0, 1));
        AddColor("dm", new Color(0.5f, 0, 0.5f));
        AddColor("purple", new Color(0.5f, 0, 0.5f));
        AddColor("violet", new Color(0.75f, 0, 0.75f));
        AddColor("indigo", RgbByte(43, 34, 170));
        AddColor("deeppurple", new Color(0.4f, 0, 0.4f));
        AddColor("darkpurple", RgbByte(48, 25, 52));
        AddColor("phlox", RgbByte(223, 0, 255));
        AddColor("mauve", RgbByte(224, 176, 255));
        AddColor("fuchsia", RgbByte(255, 0, 255));
        AddColor("lilac", RgbByte(200, 162, 200));
        // whites and grays
        AddColor("w", new Color(1, 1, 1));
        AddColor("white", new Color(1, 1, 1));
        AddColor("chinawhite", new Color(0.937f, 0.910f, 0.878f));
        AddColor("clear", Color.clear);
        AddColor("silver", RgbByte(192, 192, 192));
        AddColor("lightgrey", RgbByte(211, 211, 211));
        AddColor("lightgray", RgbByte(211, 211, 211));
        AddColor("slategray", RgbByte(112, 128, 144));
        AddColor("slategrey", RgbByte(112, 128, 144));
        AddColor("darkslategray", RgbByte(74, 85, 83));
        AddColor("darkslategrey", RgbByte(74, 85, 83));
        AddColor("darkgray", RgbByte(105, 105, 105));
        AddColor("darkgrey", RgbByte(105, 105, 105));
        AddColor("dimgray", RgbByte(105, 105, 105));
        AddColor("dimgrey", RgbByte(105, 105, 105));
        AddColor("grey", RgbByte(128, 128, 128));
        AddColor("gray", RgbByte(128, 128, 128));
        AddColor("blk", new Color(0, 0, 0));
        AddColor("black", new Color(0, 0, 0));
        var ncnt = colorTable.Count;
        var msg = $"Inited color table count:{ncnt} dups:{colordups} conflicts:{colorconflicts}";
    }
    static string[] dcolorseq = { "dr", "dg", "db", "dm", "dy", "dc" };
    static string[] colorseq = { "r", "g", "b", "m", "y", "c" };
    public static string GetColorBySeq(int idx, bool dark = true)
    {
        string name;
        if (dark)
        {
            name = dcolorseq[idx % dcolorseq.Length];
        }
        else
        {
            name = colorseq[idx % colorseq.Length];
        }
        return name;
    }
    public static Color GetColorByName(string name)
    {
        if (!IsColorName(name))
        {
            Debug.LogError($"color {name} not defined in colortable");
            return Color.gray;
        }
        return colorTable[name];
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }
    public static void AddFltTextMeshGameObject(GameObject go, Vector3 pt, string text, string colorname, Vector3 rot, Vector3 off, float ska=1,FltTextImpl lfltTextImpl = FltTextImpl.TextPro, FltTextRotate rotmeth = FltTextRotate.Specified, bool wps =false, string goname="FltText")
    {
        var ngo = new GameObject(goname);
        switch (rotmeth)
        {
            case FltTextRotate.Specified:
                ngo.transform.rotation = Quaternion.Euler(rot);
                break;
            case FltTextRotate.MainCam:
            case FltTextRotate.SceneCam:
                ngo.transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward);
                break;
        }
        ngo.transform.position = off;
        ngo.transform.localScale = new Vector3(ska,ska,ska);
        ngo.transform.SetParent(go.transform, worldPositionStays:wps);
        var txgo = new GameObject("TextMesh");
        txgo.transform.SetParent(ngo.transform,worldPositionStays:false);
        AddFltTextMeshComponent(txgo, Vector3.zero, text, colorname, lfltTextImpl, wps:false);
        //var cgo = CreateSphere(ngo, "limegreen", size: 0.2f, wps: false);
        //cgo.name = "sphere";
    }

    public enum FltTextImpl { TextMesh, TextPro };
    public enum FltTextRotate { Specified, MainCam, SceneCam }
    public static void AddFltTextMeshComponent(GameObject go, Vector3 pt, string text, string colorname, FltTextImpl lfltTextImpl, bool wps=false)
    {
        // Turns out this is easier to debug
        switch (lfltTextImpl)
        {
            default:
            case FltTextImpl.TextMesh:
                {
                    var tm = go.AddComponent<TextMesh>();
                    int linecountMinus1 = text.Split('\n').Length-1;
                    tm.text = text;
                    tm.fontSize = 12;
                    if (linecountMinus1==0)
                    {
                        tm.anchor = TextAnchor.MiddleCenter;
                    }
                    else
                    {
                        tm.anchor = TextAnchor.UpperCenter;
                    }
                    float sfak = 0.1f;
                    tm.transform.localScale = new Vector3(sfak, sfak, sfak);
                    tm.transform.localPosition = pt + new Vector3(0, linecountMinus1 * 0.125f, 0);
                    tm.transform.SetParent(go.transform,worldPositionStays:wps);
                    tm.color = GetColorByName(colorname);
                    break;
                }
            case FltTextImpl.TextPro:
                {
                    var tm = go.AddComponent<TMPro.TextMeshPro>();
                    int linecountMinus1 = text.Split('\n').Length-1;
                    //tm.text = "<mark=#000000>"+text+"</mark>"; // this only works with an alpha of less than 1
                    tm.text = text;
                    tm.fontSize = 12;
                    if (linecountMinus1 == 0)
                    {
                        tm.alignment = TMPro.TextAlignmentOptions.Center;
                    }
                    else
                    {
                        tm.alignment = TMPro.TextAlignmentOptions.Center;
                    }
                    float sfak = 0.1f;
                    tm.transform.localScale = new Vector3(sfak, sfak, sfak);
                    tm.transform.localPosition = pt + new Vector3(0, linecountMinus1 * 0, 0);
                    tm.transform.SetParent(go.transform, worldPositionStays: wps);
                    tm.color = GetColorByName(colorname);
                    tm.alpha = 1.0f; // this has to stay at the end or it gets overwritten !!
                    break;
                }
        }
    }

    //public static void AddFltTextMeshComponentOld(GameObject go, Vector3 pt, string text, string colorname,FltTextImpl lfltTextImpl, FltTextRotate rotmeth = FltTextRotate.Specified, float xrot = 0, float yrot = 0, float zrot = 0, float xoff = 0, float yoff = 0, float zoff = 0, bool wps = true)
    //{
    //    // Turns out this is easier to debug
    //    switch (lfltTextImpl)
    //    {
    //        default:
    //        case FltTextImpl.TextMesh:
    //            {
    //                var tm = go.AddComponent<TextMesh>();
    //                int linecount = text.Split('\n').Length - 1;
    //                tm.text = text;
    //                tm.fontSize = 12;
    //                tm.anchor = TextAnchor.UpperCenter;
    //                float sfak = 0.1f;
    //                tm.transform.localScale = new Vector3(sfak, sfak, sfak);
    //                switch (rotmeth)
    //                {
    //                    case FltTextRotate.Specified:
    //                        tm.transform.Rotate(xrot, yrot, zrot);
    //                        break;
    //                    case FltTextRotate.MainCam:
    //                    case FltTextRotate.SceneCam:
    //                        tm.transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward);
    //                        break;
    //                }
    //                tm.transform.localPosition = pt + new Vector3(xoff, sfak * yoff + linecount * 0.25f, zoff);
    //                tm.transform.SetParent(go.transform, worldPositionStays: wps);
    //                tm.color = GetColorByName(colorname);
    //                break;
    //            }
    //        case FltTextImpl.GUIText:
    //            {
    //                Vector2 worldPoint = Camera.main.WorldToScreenPoint(go.transform.position);
    //                GUI.Label(new Rect(worldPoint.x - 100, (Screen.height - worldPoint.y) - 50, 200, 100), text);
    //                break;
    //            }
    //        case FltTextImpl.TextPro:
    //            {
    //                var tm = go.AddComponent<TMPro.TextMeshPro>();
    //                int linecount = text.Split('\n').Length - 1;
    //                //tm.text = "<mark=#000000>"+text+"</mark>"; // this only works with an alpha of less than 1
    //                tm.text = text;
    //                tm.fontSize = 12;
    //                tm.alignment = TMPro.TextAlignmentOptions.Center;
    //                float sfak = 0.1f;
    //                tm.transform.localScale = new Vector3(sfak, sfak, sfak);
    //                switch (rotmeth)
    //                {
    //                    case FltTextRotate.Specified:
    //                        tm.transform.Rotate(xrot, yrot, zrot);
    //                        break;
    //                    case FltTextRotate.MainCam:
    //                    case FltTextRotate.SceneCam:
    //                        tm.transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward);
    //                        break;
    //                }
    //                tm.transform.localPosition = pt + new Vector3(xoff, sfak * yoff + linecount * 0.25f, zoff);
    //                tm.transform.SetParent(go.transform, worldPositionStays: wps);
    //                tm.color = GetColorByName(colorname);
    //                tm.alpha = 1.0f; // this has to stay at the end or it gets overwritten !!
    //                break;
    //            }
    //    }
    //}

    //public static Color mmcolor = Color.white;
    public static GameObject CreateSphere(GameObject parent, string cname="white", float size = 0.5f, bool wps = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.transform.localScale = new Vector3(size, size, size);
        if (parent != null)
        {
            go.transform.SetParent(parent.transform, worldPositionStays: wps);
        }
        var material = go.GetComponent<Renderer>().material;
        material.color = GetColorByName(cname);
        return go;
    }
    public static GameObject CreateCube(GameObject parent, string cname = "white", float size = 0.5f, bool wps = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.localScale = new Vector3(size, size, size);
        if (parent != null)
        {
            go.transform.SetParent(parent.transform, worldPositionStays: wps);
        }
        var material = go.GetComponent<Renderer>().material;
        material.color = GetColorByName(cname);
        return go;
    }


    static System.Random random = new System.Random(12345);

    public static string GetRandomColorString()
    {
        string[] rancolors = { "red", "green", "blue", "yellow", "purple", "cyan", "pink", "black", "orange" };
        var i = random.Next(rancolors.Length);
        return rancolors[i];
    }

    static int seqnum = 0;
    public static string GetSequentialColorString()
    {
        string[] seqcolors = { "red", "green", "blue", "yellow", "purple", "cyan", "pink", "indigo", "orange", "lilac", "black", "darkgreen" };
        var i = seqnum++ % seqcolors.Length;
        return seqcolors[i];
    }

    #region args
    static List<string> cookedArgs = null;

    public static void InitArgs()
    {
        if (cookedArgs == null)
        {
            cookedArgs = new List<string>(System.Environment.GetCommandLineArgs());
        }
    }
    public static void AddArgs(string parmselector = "")
    {
        InitArgs();
        switch (parmselector)
        {
            case "test":
                {
                    AddArgs(new string[] { "-testb", "newarg2", "-tests", "hiya", "-testi", "3", "-testf", "3.14", "-testd", "1.234567890123", "-scene", "msftb19focused", "riggins", "-fly", "-run", "-nopipes" });
                    break;
                }
            case "testjnykickoff":
                {
                    AddArgs(new string[] { "-jny", "BlueTina" });
                    break;
                }
        }
    }
    public static void AddArgs(string[] parms)
    {
        InitArgs();
        cookedArgs.AddRange(parms);
    }

    public static List<string> GetArgs()
    {
        InitArgs();
        return cookedArgs;
    }

    public static bool ParmBool(string parm)
    {
        var args = GetArgs();
        foreach (var s in args)
        {
            if (s == parm) return true;
        }
        return false;
    }
    public static (bool, string) ParmString(string parm, string def = "")
    {
        var args = GetArgs();
        var stringrv = def;
        var hit = false;
        foreach (var s in args)
        {
            if (hit)
            {
                stringrv = s;
                break;
            }
            if (s == parm) hit = true;
        }
        return (hit, stringrv);
    }
    public static double ParseDouble(string s, double defval)
    {
        var ok = double.TryParse(s, out double res);
        if (!ok)
        {
            return defval;
        }
        return res;
    }
    public static int ParseInt(string s, int defval)
    {
        var ok = int.TryParse(s, out int res);
        if (!ok)
        {
            return defval;
        }
        return res;
    }
    public static (bool, int) ParmInt(string parm, int def = 0)
    {
        var args = GetArgs();
        var numrv = def;
        var stringrv = "";
        var hit = false;
        foreach (var s in args)
        {
            if (hit)
            {
                stringrv = s;
                break;
            }
            if (s == parm) hit = true;
        }
        if (hit && stringrv != "")
        {
            numrv = ParseInt(stringrv, def);
        }
        return (hit, numrv);
    }
    public static (bool, float) ParmFloat(string parm, float def = 0)
    {
        var args = GetArgs();
        var numrv = def;
        var stringrv = "";
        var hit = false;
        foreach (var s in args)
        {
            if (hit)
            {
                stringrv = s;
                break;
            }
            if (s == parm) hit = true;
        }
        if (hit && stringrv != "")
        {
            numrv = (float)ParseDouble(stringrv, def);
        }
        return (hit, numrv);
    }
    public static (bool, double) ParmDouble(string parm, double def = 0)
    {
        var args = GetArgs();
        var numrv = def;
        var stringrv = "";
        var hit = false;
        foreach (var s in args)
        {
            if (hit)
            {
                stringrv = s;
                break;
            }
            if (s == parm) hit = true;
        }
        if (hit && stringrv != "")
        {
            numrv = ParseDouble(stringrv, def);
        }
        return (hit, numrv);
    }

    public void TestParms()
    {
        var testb = ParmBool("-testb");
        UnityEngine.Assertions.Assert.IsTrue(testb);

        var testb1 = ParmBool("testb");
        UnityEngine.Assertions.Assert.IsFalse(testb1);
        var (tests, testsv) = ParmString("-tests");
        UnityEngine.Assertions.Assert.IsTrue(tests);

        var (tests1, testsv1) = ParmString("-tests1");
        var (testi, testiv) = ParmInt("-testi");
        var (testi1, testiv1) = ParmInt("-testi1", -1);
        var (testf, testfv) = ParmFloat("-testf");
        var (testf1, testfv1) = ParmFloat("-testf1", -2);
        var (testd, testdv) = ParmDouble("-testd");
        var (testd1, testdv1) = ParmDouble("-testd1", -3);
    }
    #endregion



}
