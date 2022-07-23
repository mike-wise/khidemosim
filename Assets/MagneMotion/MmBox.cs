using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace KhiDemo
{
    public enum PoolStatus { notInPool, fakePool, realPool }
    public enum BoxStatus { free, onTray, onSled, onRobot }
    public class MmBox : MonoBehaviour
    {
        MmTable mmt;
        GameObject formgo;
        static GameObject fakePoolRoot;
        static GameObject realPoolRoot;
        public enum BoxForm { CubeBased, Prefab, PrefabWithMarkerCube }
        BoxForm boxform;
        public string boxid1;
        public string boxid2;
        public string boxclr="none";
        public int seqnum;
        public BoxStatus lastStatus;
        public BoxStatus boxStatus;
        public bool destroyedOnClear;
        public PoolStatus poolStatus;

        static MmBox[] boxes = null;


        static int clas_seqnum = 0;

        static MagneMotion magmo;

        static List<MmBox> fakePool;
        static List<MmBox> realPool;

        public static List<MmBox> GetCurrentPool()
        {
            switch (magmo.mmctrl.mmBoxMode)
            {
                default:
                case MmBoxMode.FakePooled:
                    return fakePool;
                case MmBoxMode.RealPooled:
                    return realPool;
            }
        }

        public static void AllocatePools(MagneMotion magmo)
        {
            fakePoolRoot = new GameObject("FakePoolRoot");
            fakePool = new List<MmBox>();
            var nfakePool = 12 + 1 + 10;
            for (int i = 0; i < nfakePool; i++)
            {
                var boxid = $"f{i}";
                var box = MmBox.ConstructBox(magmo, BoxForm.Prefab, boxid);
                box.poolStatus = PoolStatus.fakePool;
                box.transform.SetParent(fakePoolRoot.transform, worldPositionStays: false);
                fakePool.Add(box);
            }
            realPoolRoot = new GameObject("RealPoolRoot");
            realPool = new List<MmBox>();
            var nrealPool = 10;
            for (int i = 0; i < nrealPool; i++)
            {
                var boxid = $"r{i}";
                var box = MmBox.ConstructBox(magmo, BoxForm.PrefabWithMarkerCube, boxid);
                box.poolStatus = PoolStatus.realPool;
                box.transform.SetParent(realPoolRoot.transform, worldPositionStays: false);
                realPool.Add(box);
            }
        }

        public static void ReturnToPool(MmBox box)
        {
            if (box == null)
            {
                magmo.ErrMsg($"Trying to return null box pool");
                return;
            }
            switch (box.poolStatus)
            {
                case PoolStatus.realPool:
                    box.lastStatus = box.boxStatus;
                    box.boxStatus = BoxStatus.free;
                    box.transform.SetParent(realPoolRoot.transform, worldPositionStays: false);
                    break;
                case PoolStatus.fakePool:
                    box.lastStatus = box.boxStatus;
                    box.boxStatus = BoxStatus.free;
                    box.transform.SetParent(fakePoolRoot.transform, worldPositionStays: false);
                    break;
                default:
                    magmo.ErrMsg($"Trying to return non-pooled box to pool");
                    break;
            }
        }

        public static MmBox GetFreePooledBox(BoxStatus newBoxStatus)
        {
            var pool = GetCurrentPool();
            foreach (var bx in pool)
            {
                if (bx.boxStatus == BoxStatus.free)
                {
                    bx.lastStatus = bx.boxStatus;
                    bx.boxStatus = newBoxStatus;
                    return bx;
                }
            }
            return null;
        }

        public static MmBox ConstructBox(MagneMotion magmo, BoxForm boxform, string boxid1, BoxStatus stat=BoxStatus.free)
        {
            var mmt = magmo.mmt;
            clas_seqnum++;
            var boxname = $"Box-{clas_seqnum:D2}";
            var boxgeomgo = new GameObject(boxname);
            boxgeomgo.transform.position = Vector3.zero;
            boxgeomgo.transform.rotation = Quaternion.identity;
            var box = boxgeomgo.AddComponent<MmBox>();
            box.mmt = mmt;
            box.boxid1 = boxid1;
            box.poolStatus = PoolStatus.notInPool;

            box.seqnum = clas_seqnum;
            box.boxid2 = $"{box.seqnum:D2}";
            box.ConstructForm(boxform);
            box.lastStatus = BoxStatus.free;
            box.boxStatus = stat;
            boxes = null;
            //boxgeomgo.transform.SetParent(mmt.mmtgo.transform, worldPositionStays: true);
            return box;
            //Debug.Log($"makesled pathnum:{pathnum} dist:{pathdist:f1} pt:{sledgeomgo.transform.position:f1}");
        }

        public static void Clear()
        {
            // clean up boxes that may have been in a limbo state when changed the mode
            // this is easier than yielding until the robot is no longer busy
            // it won't happen often but it can happen
            // all boxes in both pools should be free
            boxes = FindObjectsOfType<MmBox>();
            var mode = magmo.mmctrl.mmBoxMode;
            foreach (var box in boxes)
            {
                if (box.boxStatus!= BoxStatus.free)
                {
                    Debug.LogWarning($"ClearBoxes - BoxMode:{mode} dropped box {box.boxid1} has boxStatus:{box.boxStatus} poolStatus:{box.poolStatus} resetting to free ");
                    box.boxStatus = BoxStatus.free;
                }
            }
            boxes = null;
        }

        public static (int nFreeReal, int nFreeFake,int nOnTray,int nOnRobot,int nOnSled) CountBoxStatus()
        {
            if (boxes == null)
            {
                boxes = FindObjectsOfType<MmBox>();
            }
            var nFreeReal = 0;
            var nFreeFake = 0;
            var nOnTray = 0;
            var nOnRobot = 0;
            var nOnSled = 0;
                foreach (var box in boxes)
                {
                    switch (box.boxStatus)
                    {
                        case BoxStatus.free:
                            if (box.poolStatus == PoolStatus.fakePool)
                            {
                                nFreeFake++;
                            }
                            else
                            {
                                nFreeReal++;
                            }
                            break;
                        case BoxStatus.onTray:
                            nOnTray++;
                            break;
                        case BoxStatus.onRobot:
                            nOnRobot++;
                            break;
                        case BoxStatus.onSled:
                            nOnSled++;
                            break;
                    }
                }
            return (nFreeFake,nFreeReal,nOnTray, nOnRobot, nOnSled);
        }
        // Start is called before the first frame update
        void Awake()
        {
            magmo = FindObjectOfType<MagneMotion>();
        }
        void Start()
        {

        }

        public void SetBoxStatus(BoxStatus newstat)
        {
            lastStatus = boxStatus;
            boxStatus = newstat;
        }

        public void ConstructForm(BoxForm boxform)
        {

            if (formgo != null)
            {
                Destroy(formgo);
                formgo = null;
            }


            this.boxform = boxform;
            formgo = new GameObject("boxform");
            switch (this.boxform)
            {
                case BoxForm.CubeBased:
                    {
                        var gobx = UnityUt.CreateCube(formgo, "yellow", size: 1);
                        gobx.name = $"box";
                        // 7x5.4x4.3.5
                        gobx.transform.position = new Vector3(0.0f, 0.0f, -0.16f)*1f/8;
                        gobx.transform.localScale = new Vector3(0.43f, 0.56f, 0.26f)*1f/8;
                        break;
                    }
                case BoxForm.PrefabWithMarkerCube:
                case BoxForm.Prefab:
                    {
                        var prefab1 = (GameObject)Resources.Load("Prefabs/Box1");
                        var go1 = Instantiate<GameObject>(prefab1);
                        go1.name = $"box";
                        // 7x5.4x4.3.5
                        go1.transform.parent = formgo.transform;
                        go1.transform.position = new Vector3(0.0f, 0.0f, -0.16f)*1f/8;
                        go1.transform.localRotation = Quaternion.Euler(180, 90, -90);

                        if (boxform == BoxForm.PrefabWithMarkerCube)
                        {
                            boxclr = UnityUt.GetSequentialColorString();
                            var gobx = UnityUt.CreateCube(null, boxclr, size: 0.02f);
                            gobx.name = "markercube";
                            gobx.transform.position = new Vector3(0, 0.0164f, 0);
                            gobx.transform.SetParent(go1.transform, worldPositionStays: false);
                        }
                        break;
                    }
            }

            AddBoxIdToBoxForm();

            formgo.transform.SetParent(transform, worldPositionStays: false);
        }


        void AddBoxIdToBoxFormOld()
        {
            if (boxid1 != "")
            {
                var ska = 1f/8;
                var rot1 = new Vector3(0, 90, -90);
                var rot2 = -rot1;
                var off1 = new Vector3(-0.22f, -0.23f, -0.25f);
                var off2 = new Vector3(+0.22f, +0.23f, -0.25f);
                var txt = $"{boxid1}";
                var meth = UnityUt.FltTextImpl.TextPro;
                UnityUt.AddFltTextMeshGameObject(formgo, Vector3.zero, txt, "black", rot1, off1, ska, meth);
                UnityUt.AddFltTextMeshGameObject(formgo, Vector3.zero, txt, "black", rot2, off2, ska, meth);
            }
        }
        void AddBoxIdToBoxForm()
        {
            if (boxid1 != "")
            {
                var rot1 = new Vector3(0, 90, -90);
                var rot2 = -rot1;
                var rot3 = new Vector3(0, 0, 0);
                var off1 = new Vector3(-0.0275f, -0.02875f, -0.03125f);
                var off2 = new Vector3(+0.0275f, +0.02875f, -0.03125f);
                var off3 = new Vector3(+0.0188f, +0.02875f, -0.0366f);
                var txt1 = $"{boxid1}";
                var txt2 = $"{boxid2}";
                var meth = UnityUt.FltTextImpl.TextPro;
                var ska1 = 0.075f;
                UnityUt.AddFltTextMeshGameObject(formgo, Vector3.zero, txt1, "black", rot1, off1, ska1, meth, goname: "BoxIdTxt1");
                UnityUt.AddFltTextMeshGameObject(formgo, Vector3.zero, txt1, "black", rot2, off2, ska1, meth, goname: "BoxIdTxt1");
                UnityUt.AddFltTextMeshGameObject(formgo, Vector3.zero, txt2, "black", rot3, off3, ska1, meth, goname: "BoxIdTxt2");
            }
        }

    }
}