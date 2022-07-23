using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KhiDemo
{

    public class MmSled : MonoBehaviour
    {
        MagneMotion magmo;
        MmTable mmt;
        public enum SledForm { BoxCubeBased, Prefab }
        public int pathnum;
        public int nextpathnum;
        public float pathUnitDist;
        bool markedForDeletion = false;
        public float sledUpsSpeed;
        public float reqestedSledUpsSpeed;
        public bool visible;
        SledForm sledform;
        GameObject formgo;
        GameObject boxgo;
        public MmBox box;
        public bool loadState;
        public int sledidx;
        public string sledid;
        public string sledInFront;
        public float sledInFrontDist;
        public bool stopped;

        public static MmSled ConstructSled(MagneMotion magmo, int sledidx, string sledid, int pathnum, float pathdist, bool loaded, bool addbox=false)
        {

            var mmt = magmo.mmt;
            var sname1 = $"sledid:{sledid}";
            var sledgo = new GameObject(sname1);
            //var (pt, ang) = mmt.GetPositionAndOrientation(pathnum, pathdist);
            //sledgo.transform.position = pt;
            //sledgo.transform.rotation = Quaternion.Euler(0, 0, -ang);
            var sledform = magmo.sledForm;

            var sled = sledgo.AddComponent<MmSled>();
            sled.sledidx = sledidx;
            sled.sledid = sledid;
            sled.magmo = magmo;
            sled.mmt = mmt;
            // Set default state
            sled.sledUpsSpeed = 0;
            sled.reqestedSledUpsSpeed = 0;
            sled.pathnum = 0;
            sled.nextpathnum = -1;
            sled.pathUnitDist = 0;
            sled.loadState = true;
            sled.visible = true;
            sled.stopped = false;

            sled.ConstructSledForm( sledform, addbox);
            sled.AdjustSledOnPathDist(pathnum, pathdist);
            sled.SetLoadState(loaded);
            sledgo.transform.SetParent(mmt.mmtgo.transform, worldPositionStays: false);
            //Debug.Log($"ConstructSled {sledid} pathnum:{pathnum} nextpathnum:{sled.nextpathnum} dist:{pathdist:f1}");
            return sled;
        }

        // Start is called before the first frame update
        void Start()
        {
        }
        public bool ShouldDeleted()
        {
            return markedForDeletion;
        }
        public void MarkForDeletion()
        {
            markedForDeletion = true;
        }
        public bool GetLoadState()
        {
            return loadState;
        }

        public void SetNextPath()
        {
            var p = mmt.GetPath(pathnum);
            nextpathnum = p.FindContinuationPathIdx(loadState, alternateIfMultipleChoicesAvaliable: false);
        }

        public void SetSpeed(float newspeed)
        {
            reqestedSledUpsSpeed = newspeed;
            sledUpsSpeed = newspeed;
        }
        public void SetLoadState(bool newLoadState, bool cascadeToRobot=false)
        {
            if (newLoadState == loadState) return;
            loadState = newLoadState;
            //if (magmo.mmctrl.mmBoxMode == MmBoxMode.Real && newLoadState && boxgo == null)
            //{
            //    box = MmBox.ConstructBox(magmo, sledid, stat: BoxStatus.onSled);
            //    boxgo = box.gameObject;
            //}
            if (boxgo != null)
            {
                boxgo.SetActive(loadState);
                if (cascadeToRobot)
                {
                    magmo.mmRobot.ActivateRobBox(!loadState);
                }
            }
        }


        public void AssignedPooledBox(bool newLoadState)
        {
            if (newLoadState)
            {
                box = MmBox.GetFreePooledBox(BoxStatus.onSled);
                if (box != null)
                {
                    AttachBoxToSled(box);
                }
                else
                {
                    DetachhBoxFromSled();
                    magmo.WarnMsg($"Out of boxes in AssignedPooledBox");
                }
            }
            else
            {
                DetachhBoxFromSled();
            }
        }


        public void DeleteStuff()
        {
            var parentgo = formgo.transform.parent.gameObject;
            Destroy(gameObject);
        }

        public void ConstructSledForm(SledForm sledform,bool addBox)
        {
            // This should have no parameters with changeable state except for the form
            // This ensures we can update the form without disturbing the other logic and state that the sled has, like position and loadstate
            // Coming out of this the 

            if (formgo!=null)
            {
                Destroy(formgo);
                formgo = null;
            }

            this.sledform = sledform;

            formgo = new GameObject("sledform");
            var ska8 = 1f/8;

            switch (this.sledform)
            {
                case SledForm.BoxCubeBased:
                    {
                        //var go = UnityUt.CreateCube(formgo, "gray", size: sphrad / 3);
                        //go.transform.localScale = new Vector3(0.88f, 0.52f, 0.16f);
                        // 6.5x11.0x2cm
                        var go = UnityUt.CreateCube(formgo, "gray", size: 1 );
                        go.transform.position = new Vector3(0.0f, 0.0f, 0.09f) * ska8;
                        go.transform.localScale = new Vector3(0.9f, 0.53f, 0.224f) * ska8;
                        go.name = $"tray";

                        if (addBox)
                        {
                            //var box = UnityUt.CreateCube(formgo, "yellow", size: 1);
                            //box.name = $"box";
                            //// 7x5.4x4.3.5
                            //box.transform.position = new Vector3(0.0f, 0.0f, -0.16f) * ska8;
                            //box.transform.localScale = new Vector3(0.43f, 0.56f, 0.27f) * ska8;
                            //boxgo = box;
                            var box = MmBox.ConstructBox(mmt.magmo, mmt.magmo.boxForm, sledid, BoxStatus.onSled);
                            AttachBoxToSled(box);
                        }

                        break;
                    }
                case SledForm.Prefab:
                    {
                        var prefab = Resources.Load<GameObject>("Prefabs/Sled");
                        var go = Instantiate(prefab);
                        go.name = $"tray";
                        // 6.5x11.0x2cm
                        go.transform.parent = formgo.transform;
                        go.transform.position = new Vector3(0.0f, 0.0f, 0.011f);
                        go.transform.localRotation = Quaternion.Euler(180, 90, -90);

                        if (addBox)
                        {
                            var box = MmBox.ConstructBox(mmt.magmo, mmt.magmo.boxForm, sledid, BoxStatus.onSled );
                            AttachBoxToSled(box);
                        }

                        break;
                    }
            }

            AddSledIdToSledForm();

            formgo.transform.SetParent(transform, worldPositionStays: false);

            //Debug.Log($"ConstructSledForm sledForm:{sledform} id:{sledid}");
        }

        public void AttachBoxToSled(MmBox box)
        {
            this.box = box;
            if (box==null)
            {
                magmo.ErrMsg("AttachBoxToSled - tryied to attach null box");
                return;
            }
            boxgo = box.gameObject;
            box.transform.parent = null;
            box.transform.rotation = Quaternion.Euler(0, 0, 0);
            box.transform.position = Vector3.zero;
            box.transform.SetParent(formgo.transform, worldPositionStays:false );
            box.SetBoxStatus(BoxStatus.onSled);
            loadState = true;
        }

        public MmBox DetachhBoxFromSled()
        {
            var oldbox = box;
            if (oldbox != null)
            {
                oldbox.SetBoxStatus(BoxStatus.free);
            }
            box = null;
            boxgo = null;
            loadState = false;
            return oldbox;
        }

        void AddSledIdToSledForm()
        {
            var ska = 1f/8;
            var rot1 = new Vector3(0, 90, -90);
            var rot2 = -rot1;
            var off1 = new Vector3(-0.27f, 0, -0.12f)*ska;
            var off2 = new Vector3(+0.27f, 0, -0.12f)*ska;
            var txt = $"{sledid}";
            var meth = UnityUt.FltTextImpl.TextPro;
            UnityUt.AddFltTextMeshGameObject(formgo, Vector3.zero, txt, "yellow", rot1, off1, ska, meth, goname:"SledidTxt");
            UnityUt.AddFltTextMeshGameObject(formgo, Vector3.zero, txt, "yellow", rot2, off2, ska, meth, goname: "SledidTxt");
        }

        void AdjustSledOnPathDist(int pathnum, float pathdist)
        {
            this.pathnum = pathnum;
            this.pathUnitDist = pathdist;

            var (pt, ang) = mmt.GetPositionAndOrientation(pathnum, pathdist);
            AdjustSledPositionAndOrientation(pt, ang);

            visible = pathnum >= 0;
            formgo.SetActive(visible);
        }

        void AdjustSledPositionAndOrientation(Vector3 pt, float ang)
        {
            var geomparenttrans = transform.parent;
            transform.parent = null;

            transform.position = pt;
            transform.rotation = Quaternion.Euler(0, 0, -ang);
            transform.localScale = Vector3.one;
            transform.SetParent(geomparenttrans, worldPositionStays: false);
            transform.SetAsFirstSibling();
        }

        public void EchoUpdateSled(int new_pathnum, float new_pathdist, bool new_loaded)
        {
            //var msg = $"Updating {sledid} to path:{new_pathnum} pos:{new_pathdist:f2} loaded:{new_loaded}";
            //Debug.Log(msg);
            this.pathnum = new_pathnum;
            this.pathUnitDist = new_pathdist;
            this.visible = new_pathnum >= 0;
            this.formgo.SetActive(visible);
            if (new_pathnum < 0) return;
            SetLoadState(new_loaded,cascadeToRobot:true);
            AdjustSledOnPathDist(new_pathnum, new_pathdist);
        }

        const float sledMinGap = 8*0.10f;// 10 cm
        public float deltDistToMove;
        public float maxDistToMove;
        public void AdvanceSledBySpeed()
        {
            if (pathnum >= 0)
            {
                deltDistToMove = 8*this.sledUpsSpeed * Time.deltaTime;
                if (sledInFront!="")
                {
                    maxDistToMove = sledInFrontDist - sledMinGap;
                    deltDistToMove = Mathf.Min(maxDistToMove, deltDistToMove);
                    if (deltDistToMove<0)
                    {
                        deltDistToMove = 0;
                    }
                }
                var path = mmt.GetPath(pathnum);
                bool atEndOfPath;
                int oldpath = pathnum;
                (pathnum, pathUnitDist, atEndOfPath, stopped) = path.AdvancePathdistInUnits(pathUnitDist, deltDistToMove, loadState );
                if (oldpath!= pathnum)
                {
                    // pathchanged
                    var newpath = mmt.GetPath(pathnum);
                    nextpathnum = newpath.FindContinuationPathIdx(loadState, alternateIfMultipleChoicesAvaliable: false);
                    if (nextpathnum==pathnum)
                    {
                        magmo.WarnMsg($"AdvancePathBySpped sledid:{sledid} nextpathnum {nextpathnum} cannot be equal to pathnum:{pathnum}");
                    }
                }
                if (atEndOfPath)
                {
                    this.MarkForDeletion();
                }
                else
                {
                    AdjustSledOnPathDist(pathnum, pathUnitDist);
                }
            }
        }
        public bool CheckConsistency()
        {
            if (magmo.mmctrl.mmBoxMode == MmBoxMode.RealPooled)
            {
                if (loadState && box == null)
                {
                    magmo.ErrMsg($"Sled:{sledid} has loadstate:{loadState} and box == null");
                    return false;
                }
                if (!loadState && box != null)
                {
                    magmo.ErrMsg($"Sled:{sledid} has loadstate:{loadState} and box is not null");
                    return false;
                }
            }
            if (magmo.mmctrl.mmBoxMode == MmBoxMode.FakePooled)
            {
                if (box == null)
                {
                    magmo.ErrMsg($"Sled:{sledid} has null box in FakeMode");
                    return false;
                }
            }
            return true;
        }
        public void FindSledInFront()
        {
            sledInFront = "";
            sledInFrontDist = float.MaxValue;
            if (pathnum >= 0)
            {
                var p = mmt.GetPath(pathnum);
                var pathTotalUnitDist = p.pathLength;
                var lookForwardOnePath = nextpathnum>=0 && pathnum != nextpathnum;
                if (!lookForwardOnePath)
                {
                    magmo.WarnMsg($"Don't look foward sledid:{sledid} pidx:{pathnum}  time:{Time.time}");
                }
                foreach (var s in mmt.sleds)
                {
                    //var db = sledid == "6" && s.sledid == "5";
                    var db = false;
                    if (s.pathnum==pathnum)
                    {
                        if (s.pathUnitDist>pathUnitDist)
                        {
                            var newdist = s.pathUnitDist - pathUnitDist;
                            if (newdist<sledInFrontDist)
                            {
                                sledInFront = s.sledid;
                                sledInFrontDist = newdist;
                            }
                        }
                    }
                    if (lookForwardOnePath)
                    {
                        //if (s.pathnum == nextpathnum)
                        if (p.continuationPathIdx.Contains(s.pathnum))
                        {
                            var newdist = s.pathUnitDist + pathTotalUnitDist - pathUnitDist;
                            if (db)
                            {
                                Debug.Log($"sid:{sledid:f1} pidx:{pathnum} pud:{pathUnitDist}   s.sid:{s.sledid} pidx:{s.pathnum} s.pud:{s.pathUnitDist:f1}  pathTotalUnitDist:{pathTotalUnitDist:f1} newdist:{newdist:f1}");
                            }
                            if (newdist < sledInFrontDist)
                            {
                                sledInFront = s.sledid;
                                sledInFrontDist = newdist;
                            }
                        }
                    }
                }
            }
        }

        //void SyncLoadState()
        //{
        //    if (boxgo!=null)
        //    {
        //        boxgo.SetActive(loadState);
        //    }
        //}
        //int updatecount = 0;
        // Update is called once per frame

        // void Update()
        //{
        //    SyncLoadState();
        //    updatecount++;
        //}
    }
}