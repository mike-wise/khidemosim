using System;
using System.Collections.Generic;
using UnityEngine;

namespace KhiDemo
{

    [Serializable]
    public class MmPathSeg
    {
        string name;
        public static bool inited = false;
        static float sphrad = 0.3f;
        static Dictionary<string, float> angleval;
        static Dictionary<(string, string), string> rot90;
        static Dictionary<string, Vector3> lenVek;
        static Dictionary<string, float> compassSigns;
        static List<string> compassPoints = new List<string>() { "s", "n", "e", "w" };
        static List<string> compassDirections = new List<string>() { "cw", "ccw" };

        public static void InitDicts()
        {
            //Debug.Log("InitDicts");
            rot90 = new Dictionary<(string, string), string>();
            rot90[("cw", "s")] = "w";
            rot90[("cw", "w")] = "n";
            rot90[("cw", "n")] = "e";
            rot90[("cw", "e")] = "s";
            rot90[("ccw", "s")] = "e";
            rot90[("ccw", "e")] = "n";
            rot90[("ccw", "n")] = "w";
            rot90[("ccw", "w")] = "s";
            lenVek = new Dictionary<string, Vector3>();
            lenVek["s"] = new Vector3(0, -1, 0);
            lenVek["n"] = new Vector3(0, +1, 0);
            lenVek["e"] = new Vector3(+1, 0, 0);
            lenVek["w"] = new Vector3(-1, 0, 0);
            compassSigns = new Dictionary<string, float>();
            compassSigns["cw"] = 1;
            compassSigns["ccw"] = -1;
            angleval = new Dictionary<string, float>();
            angleval["n"] = 0;
            angleval["e"] = 90;
            angleval["s"] = 180;
            angleval["w"] = 270;
            inited = true;
        }
        [NonSerialized]
        MmPath path;
        GameObject startgo;
        public MmSegForm mmSegForm = MmSegForm.None;
        public string direction = "";
        public float lengthUnits = 0;
        public string rotdir = "";
        public string startCompassPt = "";
        public Vector3 strpt = Vector3.zero;
        public Vector3 endpt = Vector3.zero;
        public Vector3 cenpt = Vector3.zero;
        public float sang = 0;
        public float eang = 0;
        public float spang = 0;
        public float epang = 0;
        public float rad = 0;
        public MmPathSeg(MmPath path, MmSegForm inform, string name, string direction, float lengthUnits)
        {
            if (inform != MmSegForm.Straight)
            {
                path.magmo.ErrMsg("Straight Form called incorrectly");
                return;
            }
            if (!direction.Contains(direction))
            {
                path.magmo.ErrMsg($"Bad MmPathSeg parameter - direction:{direction}");
                return;
            }
            this.name = name;
            this.path = path;
            mmSegForm = MmSegForm.Straight;
            this.direction = direction;
            this.lengthUnits = lengthUnits;
            this.strpt = path.End();
            this.endpt = this.strpt + lenVek[direction] * lengthUnits;
            spang = angleval[direction];
            epang = spang;
            path.AdjustEndPoint(this.endpt);
        }
        public MmPathSeg(MmPath path, MmSegForm inform, string name, string startCompassPt, string rotdir)
        {
            if (inform != MmSegForm.Curved)
            {
                path.magmo.ErrMsg("Straight Form called incorrectly");
                return;
            }

            if (!compassPoints.Contains(startCompassPt))
            {
                path.magmo.ErrMsg($"Bad MmPathSeg parameter - startCompassPt:{startCompassPt}");
                return;
            }
            if (!compassDirections.Contains(rotdir))
            {
                path.magmo.ErrMsg($"Bad MmPathSeg parameter - rotdir:{rotdir}");
                return;
            }
            this.name = name;
            this.path = path;
            mmSegForm = MmSegForm.Curved;
            this.startCompassPt = startCompassPt;
            this.rotdir = rotdir;
            strpt = path.End();
            //this.center = startpt + radVek[startCompassPt];
            cenpt = strpt - lenVek[startCompassPt];
            sang = angleval[startCompassPt];
            spang = sang + compassSigns[rotdir] * 90f;
            eang = sang + compassSigns[rotdir] * 90f;
            epang = eang + compassSigns[rotdir] * 90f; ;
            this.lengthUnits = Mathf.PI / 2f;// 1 quarter circle
            rad = 1f;
            var r9sp = rot90[(rotdir, startCompassPt)];
            //Debug.Log($"startCompasPt:{startCompassPt} rot90:{r9sp}spang:{spang} epang:{epang}");

            var ldir = rot90[(rotdir, startCompassPt)];
            this.endpt = this.strpt - lenVek[startCompassPt] + lenVek[ldir];
            //this.endpt = this.startpt + radVek[startCompassPt] + lenVek[ldir];
            path.AdjustEndPoint(this.endpt);
        }
        float angOfLmb(float lamb)
        {
            var ang = (float)Math.PI * (lamb * (eang - sang) + sang) / 180f;
            return ang;
        }
        public float angOfLmbDeg(float lamb)
        {
            var ang = (float)(lamb * (eang - sang) + sang);
            return ang;
        }
        public float pangOfLmbDeg(float lamb)
        {
            var pang = (float)(lamb * (epang - spang) + spang);
            return pang;
        }
        public Vector3 ptOfLmb(float lamb)
        {
            if (this.mmSegForm == MmSegForm.Curved)
            {
                var ang = angOfLmb(lamb);
                var y = Mathf.Cos(ang) * rad;
                var x = Mathf.Sin(ang) * rad;
                var pt = cenpt + new Vector3(x, y, 0);
                return pt;
            }
            else
            {
                var pt = lamb * (endpt - strpt) + strpt;
                return pt;
            }
        }
        public void MakeGos(GameObject parent)
        {
            startgo = UnityUt.CreateSphere(parent, "green", size: sphrad);
            startgo.name = name;
            startgo.transform.position = strpt;
            startgo = UnityUt.CreateSphere(parent, "blue", size: sphrad);
            startgo.name = name;
            startgo.transform.position = endpt;


            if (this.mmSegForm == MmSegForm.Curved)
            {
                startgo = UnityUt.CreateSphere(parent, "magenta", size: sphrad);
                startgo.name = name + "-center";
                startgo.transform.position = this.cenpt;
                var npts = 10;
                for (int i = 0; i < npts; i++)
                {
                    var frac = i * 1.0f / npts;
                    var ang = angOfLmbDeg(frac);
                    var pt = ptOfLmb(frac);

                    var go = UnityUt.CreateSphere(parent, "white", size: sphrad / 2);
                    go.name = $"{name} ang:{ang:f0} frac:{frac:f2}";
                    //go.transform.position = UnitsToMeters(pt);
                    go.transform.position = pt;
                }
            }
            else
            {
                var npts = 10;
                for (int i = 0; i < npts; i++)
                {
                    var frac = i * 1.0f / npts;
                    var pt = frac * (endpt - strpt) + strpt;
                    var go = UnityUt.CreateSphere(parent, "gray", size: sphrad / 2);
                    go.name = $"{name} line frac:{frac:f2}";
                    //go.transform.position = UnitsToMeters(pt);
                    go.transform.position = pt;
                }
            }
        }
    }

    [Serializable]
    public class MmPath
    {
        static float sphrad = 0.6f;
        [NonSerialized]
        public MagneMotion magmo;
        MmTable mmt;
        GameObject startgo;
        public string name;
        Vector3 startpt;
        Vector3 endpt;
        public int pidx;
        public float pathLength = 0;
        public List<MmPathSeg> segs = new List<MmPathSeg>();
        [NonSerialized]
        public List<MmPath> continuationPaths = new List<MmPath>();
        public List<int> continuationPathIdx = new List<int>();
        public MmPath preferedLoadedPath = null;
        public MmPath preferedUnloadedPath = null;
        public float loadedStopPoint;
        public float unloadedStopPoint;

        public MmPath(MagneMotion magmo, int idx, string name, Vector3 startpt)
        {
            this.magmo = magmo;
            this.mmt = magmo.mmt;
            this.name = name;
            this.pidx = idx;
            this.startpt = startpt;
            this.endpt = this.startpt;
            this.loadedStopPoint = -1;
            this.unloadedStopPoint = -1;
        }

        public MmRail MakePathRail(string sledid, int pathnum, float pathdist)
        {
            var rail = MmRail.ConstructRail(magmo, sledid, pathnum, pathdist);
            mmt.rails.Add(rail);
            return rail;
        }
        public void AddPathRails(GameObject parent, bool seggos, bool pathgos)
        {

            var sz = sphrad / 3;
            var pos = this.startpt;
            if (mmt.useMeters)
            {
                sz *= mmt.UnitsToMeters;
                pos *= mmt.UnitsToMeters;
            }
            startgo = UnityUt.CreateCube(parent, "blue", size: sz);
            startgo.name = $"Start-{name}";
            startgo.transform.position = pos;
            if (seggos)
            {
                foreach (var seg in segs)
                {
                    seg.MakeGos(startgo);
                }
            }
            if (pathgos)
            {
                var nrails = (int)this.pathLength / 0.4f;
                for (int i = 0; i < nrails; i++)
                {
                    var frac = i * 1.0f / nrails;
                    var pathdist = frac * this.pathLength;
                    var (pt, ang) = GetPositionAndOrientation(pathdist);
                    var rname = $"{name} rail - frac:{frac:f2} ang:{ang:f0}";
                    MakePathRail(rname, pidx, pathdist);
                }
            }
        }



        public (int newpathidx, float newpathdist, bool atEndOfPath, bool stopped) AdvancePathdistInUnits(float curpathdist, float deltadist, bool loaded)
        {
            var newdist = curpathdist + deltadist;
            if (newdist < this.pathLength)
            {
                if (loaded && loadedStopPoint>0)
                {
                    if (curpathdist<=loadedStopPoint && loadedStopPoint<newdist)
                    {
                        return (pidx, loadedStopPoint, atEndOfPath: false, stopped: true);
                    }
                }
                if (!loaded && unloadedStopPoint > 0)
                {
                    if (curpathdist <= unloadedStopPoint && unloadedStopPoint < newdist)
                    {
                        return (pidx, unloadedStopPoint, atEndOfPath: false, stopped: true);
                    }
                }
                return (pidx, newdist, atEndOfPath:false, stopped:false);
            }
            var restdist = newdist - this.pathLength;

            if (continuationPaths.Count == 0)
            {
                return (pidx, this.pathLength, atEndOfPath: true, stopped: false);
            }
            var nxpidx = FindContinuationPathIdx(loaded, alternateIfMultipleChoicesAvaliable: true);

            return (nxpidx, restdist, atEndOfPath: false, stopped: false);
        }

        int selcount = 0;
        public int FindContinuationPathIdx(bool loaded,bool alternateIfMultipleChoicesAvaliable=true)
        {
            if (continuationPaths.Count == 0)
            {
                return -1;
            }
            var newpath = continuationPaths[0];
            if (alternateIfMultipleChoicesAvaliable)
            {
                newpath = continuationPaths[selcount % continuationPaths.Count];
                selcount++;
            }
            if (loaded && preferedLoadedPath != null)
            {
                newpath = preferedLoadedPath;
            }
            else if (!loaded && preferedUnloadedPath != null)
            {
                newpath = preferedUnloadedPath;
            }
            return newpath.pidx;          
        }

        public void LinkToContinuationPath(MmPath contpath)
        {
            continuationPaths.Add(contpath);
            continuationPathIdx.Add(contpath.pidx);
        }
        public void SetPreferedLoadedPath(MmPath path)
        {
            preferedLoadedPath = path;
        }
        public void SetPreferedUnloadedPath(MmPath path)
        {
            preferedUnloadedPath = path;
        }
        public void SetLoadedStopPoint(float unitDist)
        {
            loadedStopPoint = unitDist;
        }
        public void SetUnloadedStopPoint(float unitDist)
        {
            unloadedStopPoint = unitDist;
        }
        public void AdjustEndPoint(Vector3 newendpoint)
        {
            this.endpt = newendpoint;
        }
        public Vector3 End()
        {
            return endpt;
        }
        public void MakeLineSeg(string direction, float lengthUnits)
        {
            var name = $"line-seg {segs.Count}";
            var seg = new MmPathSeg(this, MmSegForm.Straight, name, direction, lengthUnits);
            pathLength += seg.lengthUnits;
            segs.Add(seg);

        }
        public void MakeCircSeg(string startCompassPt, string rotdir)
        {
            var name = $"circ-seg {segs.Count}";
            var seg = new MmPathSeg(this, MmSegForm.Curved, name, startCompassPt, rotdir);
            pathLength += seg.lengthUnits;
            segs.Add(seg);
        }
        public (Vector3 pt, float ang) GetPositionAndOrientation(float pathdist)
        {
            //Debug.Log($"Pathidx:{pidx} pathdist:{pathdist:f2}");
            if (segs.Count <= 0)
            {
                magmo.ErrMsg($"no segs defined for path {name}");
                return (Vector3.zero, 0);
            }
            if (this.pathLength < pathdist)
            {
                magmo.WarnMsg($"GetPositionAndOrientation - path {name} - pathdist requested ({pathdist:f4}) is bigger than pathlength {pathLength:f4}");
                var eang = segs[segs.Count - 1].eang;
                return (endpt, eang);
            }
            //Debug.Log($"{name} unitLength:{unitLength}");
            var i = 0;
            var sg = segs[i];
            var lo = 0f;
            var hi = sg.lengthUnits;
            while (hi < pathdist)
            {
                //var msg = $"   i:{i} hi:{hi} {lo}";
                //Debug.Log(msg);
                i++;
                sg = segs[i];
                lo = hi;
                hi = hi + sg.lengthUnits;
            }
            var lamb = (pathdist - lo) / sg.lengthUnits;
            //var msg1 = $"   fin-i:{i} hi:{hi} {lo}  lamb:{lamb}";
            //Debug.Log(msg1);
            var pt = sg.ptOfLmb(lamb);
            var ang = sg.pangOfLmbDeg(lamb);
            //Debug.Log($"rv:{rv}");
            if (mmt.useMeters)
            {
                var u2m = mmt.UnitsToMeters;
                pt = new Vector3(u2m * pt.x, u2m * pt.y, u2m * pt.z);
            }
            //Debug.Log($"Pathidx:{pidx} pathdist:{pathdist:f2} returns - pt:{pt:f1} ang:{ang:f1}");
            return (pt, ang);
        }
    }
}