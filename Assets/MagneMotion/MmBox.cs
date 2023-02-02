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

        [Header("ID")]
        public string boxid1;
        public string boxid2;
        public string boxNativeColor="none";
        public string curColor = "none";
        public int seqnum;


        [Header("State")]
        public BoxStatus lastStatus;
        public BoxStatus boxStatus;
        public bool destroyedOnClear;
        public PoolStatus poolStatus;
        public Rigidbody rigbod;
        public GameObject markercube;

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

        public static void AllocateBoxPools(MagneMotion magmo)
        {
            fakePoolRoot = new GameObject("FakeBoxPool");
            fakePoolRoot.transform.position += new Vector3(0.5f, 0, 0);
            fakePoolRoot.transform.SetParent(magmo.simroot.transform, worldPositionStays: true);
            fakePool = new List<MmBox>();
            var nfakePool = 12 + 1 + 10;
            for (int i = 0; i < nfakePool; i++)
            {
                var boxid = $"f{i}";
                var box = MmBox.ConstructBox(magmo, BoxForm.Prefab, "FakeBox", boxid);
                box.poolStatus = PoolStatus.fakePool;
                box.transform.SetParent(fakePoolRoot.transform, worldPositionStays: false);
                SetPoolSidePosition(box);
                fakePool.Add(box);
            }
            realPoolRoot = new GameObject("RealBoxPool");
            realPoolRoot.transform.position += new Vector3(-0.5f, 0, 0);
            realPoolRoot.transform.SetParent(magmo.simroot.transform, worldPositionStays: true);
            realPool = new List<MmBox>();
            var nrealPool = 10;
            for (int i = 0; i < nrealPool; i++)
            {
                var boxid = $"r{i}";
                var box = MmBox.ConstructBox(magmo, BoxForm.PrefabWithMarkerCube, "RealBox", boxid);
                //Debug.Log($"Constructed box {box.name} rot:{box.transform.rotation.eulerAngles:f1}");
                box.poolStatus = PoolStatus.realPool;
                box.transform.SetParent(realPoolRoot.transform, worldPositionStays: false);
                SetPoolSidePosition(box);
                realPool.Add(box);
            }
        }

        static void SetPoolSidePosition(MmBox box)
        {
            var rowsize = 5;
            var rowmid = 3;
            var colmid = 1;
            var row = (box.seqnum % rowsize) - rowmid;
            var col = (box.seqnum / rowsize) - colmid;

            var pos = new Vector3(row * 0.1f, 0, col * 0.1f);
            box.transform.position += pos;
        }

        public static void ReturnToPoolSidePositions(bool fakeBoxes=true,bool realBoxes=true)
        {
            if (fakeBoxes)
            {
                foreach (var box in fakePool)
                {
                    SetPoolSidePosition(box);
                }
            }
            if (realBoxes)
            {
                foreach (var box in realPool)
                {
                    SetPoolSidePosition(box);
                }
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
                    SetPoolSidePosition(box);
                    break;
                case PoolStatus.fakePool:
                    box.lastStatus = box.boxStatus;
                    box.boxStatus = BoxStatus.free;
                    box.transform.SetParent(fakePoolRoot.transform, worldPositionStays: false);
                    SetPoolSidePosition(box);
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

        public static MmBox ConstructBox(MagneMotion magmo, BoxForm boxform, string boxname, string boxid1, BoxStatus stat=BoxStatus.free)
        {
            var mmt = magmo.mmt;
            clas_seqnum++;
            var boxwholename = $"{boxname}_{clas_seqnum:D2}";
            var boxgeomgo = new GameObject(boxwholename);
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
            var ovc = boxgeomgo.AddComponent<OvPrim>();
            ovc.Init("MmBox");
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
                        var gobx = UnityUt.CreateCube(formgo, "yellow", size: 1, collider:false);
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
                        go1.name = $"prefabbox";
                        // 7x5.4x4.3.5
                        go1.transform.parent = formgo.transform;
                        // boxrat
                        //go1.transform.position = new Vector3(0.0f, 0.0f, -0.16f)*1f/8;
                        //go1.transform.localRotation = Quaternion.Euler(0, 270, 90);
                        //go1.transform.position = new Vector3(0.0f, 0.0f, -0.16f)*1f/8;
                        go1.transform.position = new Vector3(0.0f, 0.16f, 0.0f) * 1f / 8;
                        go1.transform.localRotation = Quaternion.identity;

                        if (boxform == BoxForm.PrefabWithMarkerCube)
                        {
                            boxNativeColor = UnityUt.GetSequentialColorString();
                            markercube = UnityUt.CreateCube(null, boxNativeColor, size: 0.02f, collider:false);
                            markercube.name = "markercube";

                            markercube.transform.localScale = new Vector3(0.03f, 0.01f, 0.03f);
                            markercube.transform.position = new Vector3(0, 0.0164f, 0);
                            markercube.transform.SetParent(go1.transform, worldPositionStays: false);
                        }
                        if (magmo.mmBoxSimMode != MmBoxSimMode.Hierarchy)
                        {
                            rigbod = gameObject.AddComponent<Rigidbody>();
                            rigbod.isKinematic = true;
                            rigbod.centerOfMass = new Vector3(0, 0, -0.02f);
                            rigbod.mass = 1f;
                            var boxcol = gameObject.AddComponent<BoxCollider>();
                            //boxcol.size = new Vector3(0.054f, 0.065f, 0.033f);
                            boxcol.size = new Vector3(0.065f, 0.033f, 0.054f);
                            // boxrat
                            //boxcol.center = new Vector3(0, 0, -0.02f);
                            boxcol.center = go1.transform.localPosition;
                            boxcol.material = magmo.physMat;
                        }
                        break;
                    }
            }

            AddBoxIdToBoxForm();

            formgo.transform.SetParent(transform, worldPositionStays: false);
        }


        void AddBoxIdToBoxForm()
        {
            if (boxid1 != "")
            {
                var rotprt = new Vector3(0, 0, 0);
                var rotstb = new Vector3(0, -180, 0);
                var rottop = new Vector3(90, 270, 0);
                var offprt = new Vector3(-0.029f, +0.027f, -0.027f);
                var pffstb = new Vector3(-0.029f, +0.027f, +0.027f);
                var offtop = new Vector3(-0.029f, +0.0365f, 0.02f);
                var txtprt = $"{boxid1}";
                var txtstb = $"{boxid2}";
                var meth = UnityUt.FltTextImpl.TextPro;
                var ska = 0.075f;
                UnityUt.AddFltTextMeshGameObject(formgo, Vector3.zero, txtprt, "black", rotprt, offprt, ska, meth, goname: "BoxIdTxt1prt");
                UnityUt.AddFltTextMeshGameObject(formgo, Vector3.zero, txtprt, "black", rotstb, pffstb, ska, meth, goname: "BoxIdTxt1stb");
                UnityUt.AddFltTextMeshGameObject(formgo, Vector3.zero, txtstb, "black", rottop, offtop, ska, meth, goname: "BoxIdTxt2");
            }
        }

        MmboxColorMode lastColorMode= MmboxColorMode.Native;
        void ColorBox()
        {
            if (markercube==null)
            {
                return; // fake boxes don't have marker cubes
            }
            if (lastColorMode==magmo.boxColorMode)
            {
                return;
            }
            var clr = CalcBoxColor();
            var renderer = markercube.GetComponent<Renderer>();
            renderer.material.color = UnityUt.GetColorByName(clr);
        }


        string CalcBoxColor()
        {
            var clr = "blue";
            switch (magmo.boxColorMode)
            {
                case MmboxColorMode.Native:
                    clr = boxNativeColor;
                    break;
                case MmboxColorMode.Simmode:
                    if (rigbod==null)
                    {
                        clr = "black";
                    }
                    else
                    {
                        clr = rigbod.isKinematic? "purple" : "blue";
                    }
                    break;
            }
            return clr;
        }

        void Update()
        {
            ColorBox();
        }
    }
}