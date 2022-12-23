using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KhiDemo
{
    public class MmRail : MonoBehaviour
    {
        MmTable mmt;
        public enum RailForm { Cigar, CigarBox }
        static float sphrad = 0.2f;
        float speed;
        int pathnum;
        float pathdist;
        RailForm railform;
        GameObject geomgo;
        GameObject formgo;
        public string railid;

        public static MmRail ConstructRail(MagneMotion magmo, string rname, int pathnum, float pathdist)
        {
            var mmt = magmo.mmt;
            var railgo = new GameObject(rname);
            var (pt, ang) = mmt.GetPositionAndOrientation(pathnum, pathdist,0, magmo.trackSmoothness);
            railgo.transform.position = pt;
            //railgo.transform.rotation = Quaternion.Euler(0, 0, -ang);// Original
            //railgo.transform.rotation = Quaternion.Euler(-90, ang, 0 );// Without ReformPoint in ConstructForm
            railgo.transform.rotation = Quaternion.Euler(0, ang, 0);
            var rail = railgo.AddComponent<MmRail>();
            var railform = MmRail.RailForm.Cigar;
            rail.railid = "";
            rail.pathnum = pathnum;
            rail.pathdist = pathdist;
            rail.speed = 0;
            rail.mmt = mmt;
            rail.ConstructForm(railform);
            railgo.transform.parent = mmt.pathgos.transform;
            return rail;
        }

        public void ConstructForm(RailForm railform)
        {
            this.railform = railform;
            formgo = new GameObject("railform");
            switch (this.railform)
            {
                case RailForm.Cigar:
                    {
                        var go = UnityUt.CreateCube(formgo, "gray", size: sphrad / 3, collider:false);
                        go.name = $"cigar";
                        go.transform.localScale = MmPath.ReformPoint(new Vector3(0.1f, 0.3f, 0.1f));

                        var go1 = UnityUt.CreateCube(formgo, "red", size: sphrad / 3, collider: false);
                        go1.name = $"nose";
                        go1.transform.position = MmPath.ReformPoint(new Vector3(0.0f, 0.1f, 0));
                        go1.transform.localScale = MmPath.ReformPoint(new Vector3(0.11f, 0.11f, 0.11f));
                        break;
                    }
                case RailForm.CigarBox:
                    {
                        var go = UnityUt.CreateCube(formgo, "gray", size: sphrad / 3, collider: false);
                        go.name = $"cigarbox";
                        // 6.5x11.0x2cm
                        go.transform.localScale = MmPath.ReformPoint(new Vector3(0.88f, 0.52f, 0.16f));

                        var clr = UnityUt.GetRandomColorString();
                        var go2 = UnityUt.CreateCube(formgo, clr, size: sphrad / 3, collider:false);
                        go2.name = $"nose";
                        go2.transform.position = MmPath.ReformPoint(new Vector3(0.0f, 0.2f, -0.16f));
                        go2.transform.localScale = MmPath.ReformPoint(new Vector3(0.2f, 0.2f, 0.2f));
                        break;
                    }
            }
            if (mmt.useMeters)
            {
                var u2m = mmt.UnitsToMeters;
                formgo.transform.localScale = new Vector3(u2m, u2m, u2m);
            }
            formgo.transform.SetParent(gameObject.transform, worldPositionStays: false);
        }
    }
}