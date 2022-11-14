using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KhiDemo
{
    public enum SledMoveStatus { Unset, Stopped, Moving };

    public class MmSled : MonoBehaviour
    {
        MagneMotion magmo;
        MmTable mmt;
        public enum SledForm { BoxCubeBased, Prefab }
        public bool useNewAccelerationModel = false;




        public int pathnum;
        public int nextpathnum;
        public float pathUnitDist;
        bool markedForDeletion = false;
        public float sledUnitsPerSecSpeed;
        public float reqestedSledUpsSpeed;
        public bool visible;
        SledForm sledform;
        GameObject formgo;
        GameObject traygo;
        public MmBox box;
        public bool loadState;
        public int sledidx;
        public string sledid;
        public string sledInFront;
        public float sledInFrontDist;
        public SledMoveStatus sledMoveStatus = SledMoveStatus.Unset;
        public Rigidbody rigbod;
        public Matrix4x4 localToWorld;

        public static MmSled ConstructSled(MagneMotion magmo, int sledidx, string sledid, int pathnum, float pathdist, bool loaded, bool addbox = false)
        {

            var mmt = magmo.mmt;
            var sname1 = $"sledid_{sledid}";
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
            sled.sledUnitsPerSecSpeed = 0;
            sled.reqestedSledUpsSpeed = 0;
            sled.pathnum = 0;
            sled.nextpathnum = -1;
            sled.pathUnitDist = 0;
            sled.loadState = true;
            sled.visible = true;
            sled.sledMoveStatus = SledMoveStatus.Moving;
            sled.useNewAccelerationModel = true;

            sled.ConstructSledForm(sledform, addbox);
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
        public void SetRequestedSpeedNew(float newspeed)
        {
            reqestedSledUpsSpeed = newspeed;
        }
        public void SetRequestedSpeedOld(float newspeed)
        {
            reqestedSledUpsSpeed = newspeed;
            sledUnitsPerSecSpeed = newspeed;
        }
        public void SetRequestedSpeed(float newspeed)
        {
            if (useNewAccelerationModel)
            {
                SetRequestedSpeedNew(newspeed);
            }
            else
            {
                SetRequestedSpeedOld(newspeed);
            }
        }
        public void SetLoadState(bool newLoadState, bool cascadeToRobot = false)
        {
            if (newLoadState == loadState) return;
            loadState = newLoadState;
            if (box?.gameObject != null)
            {
                box.gameObject.SetActive(loadState);
                if (cascadeToRobot)
                {
                    magmo.mmRobot.ActivateRobBox(!loadState);
                }
            }
        }


        public void AssignedPooledBox(bool newLoadState,bool firstTime=false)
        {
            if (newLoadState)
            {
                box = MmBox.GetFreePooledBox(BoxStatus.onSled);
                if (box != null)
                {
                    AttachBoxToSled(box,firstTime:firstTime);
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

        public void ConstructSledForm(SledForm sledform, bool addBox)
        {
            // This should have no parameters with changeable state except for the form
            // This ensures we can update the form without disturbing the other logic and state that the sled has, like position and loadstate
            // Coming out of this the 

            if (formgo != null)
            {
                Destroy(formgo);
                formgo = null;
            }

            this.sledform = sledform;

            formgo = new GameObject("sledform");
            var unitsToMetersFak = 1f / 8;

            switch (this.sledform)
            {
                case SledForm.BoxCubeBased:
                    {
                        //var go = UnityUt.CreateCube(formgo, "gray", size: sphrad / 3);
                        //go.transform.localScale = new Vector3(0.88f, 0.52f, 0.16f);
                        // 6.5x11.0x2cm
                        var go = UnityUt.CreateCube(formgo, "gray", size: 1, collider:false);
                        go.transform.position = new Vector3(0.0f, 0.0f, 0.09f) * unitsToMetersFak;
                        go.transform.localScale = new Vector3(0.9f, 0.53f, 0.224f) * unitsToMetersFak;
                        go.name = $"tray";

                        if (addBox)
                        {
                            //var box = UnityUt.CreateCube(formgo, "yellow", size: 1);
                            //box.name = $"box";
                            //// 7x5.4x4.3.5
                            //box.transform.position = new Vector3(0.0f, 0.0f, -0.16f) * unitsToMetersFak;
                            //box.transform.localScale = new Vector3(0.43f, 0.56f, 0.27f) * unitsToMetersFak;
                            //boxgo = box;
                            var box = MmBox.ConstructBox(mmt.magmo, mmt.magmo.boxForm, "Hmm", sledid, BoxStatus.onSled);
                            AttachBoxToSled(box);
                        }

                        break;
                    }
                case SledForm.Prefab:
                    {
                        var prefab = Resources.Load<GameObject>("Prefabs/Sled");
                        traygo = Instantiate(prefab);
                        traygo.name = $"tray";
                        // 6.5x11.0x2cm
                        traygo.transform.parent = formgo.transform;
                        traygo.transform.position = new Vector3(0.0f, 0.0f, 0.011f);
                        if (magmo.mmSledMoveMethod == MmSledMoveMethod.SetPosition)
                        {
                            traygo.transform.localRotation = Quaternion.Euler(180, 90, -90);
                        }

                        if (addBox)
                        {
                            var box = MmBox.ConstructBox(mmt.magmo, mmt.magmo.boxForm, "Hmm", sledid, BoxStatus.onSled);
                            AttachBoxToSled(box);
                        }
                        var rgo = traygo;
                        rigbod = rgo.AddComponent<Rigidbody>();
                        rigbod.isKinematic = true;
                        rigbod.mass = 1f;
                        var boxcol_base = rgo.AddComponent<BoxCollider>();
                        boxcol_base.size = new Vector3( 0.08f, 0.018f, 0.09f );
                        boxcol_base.material = magmo.physMat;
                        var boxcol_back = rgo.AddComponent<BoxCollider>();
                        boxcol_back.size = new Vector3(0.002f, 0.08f, 0.06f);
                        boxcol_back.center = new Vector3(-0.04f, 0.02f, 0.00f);
                        boxcol_back.material = magmo.physMat;
                        var boxcol_frnt = rgo.AddComponent<BoxCollider>();
                        boxcol_frnt.size = new Vector3(0.002f, 0.08f, 0.06f);
                        boxcol_frnt.center = new Vector3(+0.04f, 0.02f, 0.00f);
                        boxcol_frnt.material = magmo.physMat;
                        var boxcol_lside = rgo.AddComponent<BoxCollider>();
                        boxcol_lside.size = new Vector3(0.06f, 0.08f, 0.002f);
                        boxcol_lside.center = new Vector3(0.00f, 0.02f, +0.04f);
                        boxcol_lside.material = magmo.physMat;
                        var boxcol_rside = rgo.AddComponent<BoxCollider>();
                        boxcol_rside.size = new Vector3(0.06f, 0.08f, 0.002f);
                        boxcol_rside.center = new Vector3(0.00f, 0.02f, -0.04f);
                        boxcol_rside.material = magmo.physMat;
                        break;
                    }
            }

            AddTextIdToSledForm();

            formgo.transform.SetParent(transform, worldPositionStays: false);

            //Debug.Log($"ConstructSledForm sledForm:{sledform} id:{sledid}");
        }

        public void AttachBoxToSled(MmBox box,bool firstTime=false)
        {
            if (box == null)
            {
                magmo.ErrMsg("AttachBoxToSled - tryied to attach null box");
                return;
            }
            Debug.Log($"Attaching/associating Box to Sled - {box.boxid1} {box.boxid2} {box.boxclr} {magmo.GetHoldMethod()}");
            this.box = box;
            switch (magmo.GetHoldMethod())
            {
                case MmHoldMethod.Hierarchy:
                    // Debug.Log($"Attaching Box to Sled - Hheirarchy");
                    box.rigbod.isKinematic = true;
                    box.transform.parent = null;
                    box.transform.rotation = Quaternion.Euler(0, 0, 0);
                    box.transform.position = Vector3.zero;
                    box.transform.SetParent(formgo.transform, worldPositionStays: false);
                    break;
                case MmHoldMethod.Physics:
                    // Debug.Log($"Associating Box to Sled - Physics");
                    if (firstTime)
                    {
                        box.rigbod.isKinematic = true;
                        box.transform.rotation = Quaternion.Euler(90, 0, 0);
                        box.transform.position = formgo.transform.position;
                        box.rigbod.isKinematic = false;
                    }
                    else
                    {
                        //box.rigbod.isKinematic = true; // should be false 
                        //box.transform.rotation = Quaternion.Euler(90, 0, 0);
                        //box.transform.position = formgo.transform.position;
                        box.rigbod.isKinematic = false;
                    }
                    box.rigbod.useGravity = true;
                    break;
                case MmHoldMethod.Dragged:
                default:
                    // Debug.Log($"Associating Box to Sled - Dragged");
                    box.rigbod.isKinematic = true;
                    box.transform.rotation = Quaternion.Euler(90, 0, 0);
                    box.transform.position = formgo.transform.position;
                    break;
            }
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
            loadState = false;
            return oldbox;
        }

        void AddTextIdToSledForm()
        {
            var unitsToMetersFak = 1f / 8;
            // These commented out values are what were used when we made the text subordinate to formgo, we now use traygo
            //var rot1 = new Vector3(0, 90, -90);
            // var rot2 = -rot1;
            //var off1 = new Vector3(-0.27f, 0, -0.12f) * unitsToMetersFak;
            //var off2 = new Vector3(+0.27f, 0, -0.12f) * unitsToMetersFak;

            var rot1 = new Vector3(0, 180, 0);
            var rot2 = new Vector3(0, 0, 0);
            var off1 = new Vector3(0, 0.20f, +0.27f) * unitsToMetersFak;
            var off2 = new Vector3(0, 0.20f, -0.27f) * unitsToMetersFak;
            var txt = $"{sledid}";
            var meth = UnityUt.FltTextImpl.TextPro;
            UnityUt.AddFltTextMeshGameObject(traygo, Vector3.zero, txt, "yellow", rot1, off1, unitsToMetersFak, meth, goname: "SledidTxt-Port");
            UnityUt.AddFltTextMeshGameObject(traygo, Vector3.zero, txt, "yellow", rot2, off2, unitsToMetersFak, meth, goname: "SledidTxt-Strb");
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
            if (transform.parent == null) return;
            if (magmo.mmSledMoveMethod == MmSledMoveMethod.SetPosition)
            {
                var ptrans = transform.parent;
                transform.parent = null;
                transform.position = pt;
                transform.rotation = Quaternion.Euler(0, 0, -ang);
                transform.SetParent(ptrans, worldPositionStays: false);
            }
            else
            {
                var npt = transform.parent.TransformPoint(pt);
                rigbod.MovePosition(npt);
                var rot = Quaternion.Euler(0, ang - 90, 0);
                rigbod.MoveRotation(rot);
            }
            transform.SetAsFirstSibling();
        }

        void AdjustSledPositionAndOrientationX(Vector3 pt, float ang)
        {
            if (transform.parent == null) return;
            if (magmo.mmSledMoveMethod == MmSledMoveMethod.SetPosition)
            {
                var ptrans = transform.parent;
                var npt = ptrans.TransformPoint(pt);
                transform.position = npt;
                var rot = Quaternion.Euler(0, ang - 90, 0);
                var roti = Quaternion.Inverse(rot);
                var prot = ptrans.rotation;
                var proti = Quaternion.Inverse(prot);
                //var frot = prot * rot * proti;
                //var frot = proti * rot * prot;
                //var frot = rot *prot * roti;
                var frot = roti * prot * rot;
                transform.rotation = frot;
            }
            else
            {
                var npt = transform.parent.TransformPoint(pt);
                rigbod.MovePosition(npt);
                var rot = Quaternion.Euler(0, ang - 90, 0);
                rigbod.MoveRotation(rot);
            }
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
            SetLoadState(new_loaded, cascadeToRobot: true);
            AdjustSledOnPathDist(new_pathnum, new_pathdist);
        }
        float speedLastCaled=0;
        public void AdjustSpeed()
        {
            var oldspeed = sledUnitsPerSecSpeed;
            if (sledMoveStatus== SledMoveStatus.Stopped)
            {
                return;
            }
            var delttime = Time.time - speedLastCaled;
            if (delttime<=0)
            {
                Debug.LogError($"Sled.AdjustSpeed - delttime less than or equal to zero:{delttime}");
            }
            speedLastCaled = Time.time;
            var maxaccel = 0.1f; // Ups
            var maxdecel = 2f;
            if (reqestedSledUpsSpeed > sledUnitsPerSecSpeed)
            {
                var deltspeed = maxaccel * delttime;
                sledUnitsPerSecSpeed += deltspeed;
                if (sledUnitsPerSecSpeed > reqestedSledUpsSpeed)
                {
                    sledUnitsPerSecSpeed = reqestedSledUpsSpeed;
                }
            }
            else if (reqestedSledUpsSpeed < sledUnitsPerSecSpeed)
            {
                var deltspeed = maxdecel * delttime;
                sledUnitsPerSecSpeed -= deltspeed;
                if (sledUnitsPerSecSpeed < reqestedSledUpsSpeed)
                {
                    sledUnitsPerSecSpeed = reqestedSledUpsSpeed;
                }
            }
            var delt = sledUnitsPerSecSpeed - oldspeed;
            //if (delt != 0)
            //{
            //    Debug.Log($"Adjust speed time:{Time.time:f3} sld:{sledid} old:{oldspeed:f2}  new:{sledUnitsPerSecSpeed:f2}   delt:{delt:f3}");
            //}
        }
        const float UnitsPerMeter = 8;
        const float sledMinGap = UnitsPerMeter * 0.10f;// 10 cm
        public float deltUnitsDistToMove;
        public float maxDistToMove;
        public void AdvanceSledBySpeedNew()
        {
            if (!rigbod.isKinematic)
            {
                return;
            }
            AdjustSpeed();
            if (pathnum >= 0)
            {
                deltUnitsDistToMove = UnitsPerMeter * this.sledUnitsPerSecSpeed * Time.deltaTime;
                if (sledInFront != "")
                {
                    maxDistToMove = sledInFrontDist - sledMinGap;
                    deltUnitsDistToMove = Mathf.Min(maxDistToMove, deltUnitsDistToMove);
                    if (deltUnitsDistToMove < 0)
                    {
                        deltUnitsDistToMove = 0;
                        //sledUnitsPerSecSpeed = 0;
                    }
                }
                var path = mmt.GetPath(pathnum);
                bool atEndOfPath;
                int oldpath = pathnum;
                float distInUnitsToStop = 0;
                (pathnum, pathUnitDist, atEndOfPath, sledMoveStatus, distInUnitsToStop) = path.AdvancePathdistInUnits(pathUnitDist, deltUnitsDistToMove, loadState);
                if ((sledMoveStatus==SledMoveStatus.Stopped) && useNewAccelerationModel)
                {
                    sledUnitsPerSecSpeed = 0;
                }
                if (oldpath != pathnum)
                {
                    // pathchanged
                    var newpath = mmt.GetPath(pathnum);
                    nextpathnum = newpath.FindContinuationPathIdx(loadState, alternateIfMultipleChoicesAvaliable: false);
                    if (nextpathnum == pathnum)
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
        public void AdvanceSledBySpeedOld()
        {
            if (pathnum >= 0)
            {
                deltUnitsDistToMove = UnitsPerMeter * this.sledUnitsPerSecSpeed * Time.deltaTime;
                if (sledInFront != "")
                {
                    maxDistToMove = sledInFrontDist - sledMinGap;
                    deltUnitsDistToMove = Mathf.Min(maxDistToMove, deltUnitsDistToMove);
                    if (deltUnitsDistToMove < 0)
                    {
                        deltUnitsDistToMove = 0;
                    }
                }
                var path = mmt.GetPath(pathnum);
                bool atEndOfPath;
                int oldpath = pathnum;
                float distInUnitsToStop = 0;
                (pathnum, pathUnitDist, atEndOfPath, sledMoveStatus, distInUnitsToStop) = path.AdvancePathdistInUnits(pathUnitDist, deltUnitsDistToMove, loadState);
                if (oldpath != pathnum)
                {
                    // pathchanged
                    var newpath = mmt.GetPath(pathnum);
                    nextpathnum = newpath.FindContinuationPathIdx(loadState, alternateIfMultipleChoicesAvaliable: false);
                    if (nextpathnum == pathnum)
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
        public void AdvanceSledBySpeed()
        {
            if (useNewAccelerationModel)
            {
                AdvanceSledBySpeedNew();
            }
            else
            {
                AdvanceSledBySpeedOld();
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
                var lookForwardOnePath = nextpathnum >= 0 && pathnum != nextpathnum;
                if (!lookForwardOnePath)
                {
                    magmo.WarnMsg($"Don't look foward sledid:{sledid} pidx:{pathnum}  time:{Time.time}");
                }
                foreach (var s in mmt.sleds)
                {
                    //var db = sledid == "6" && s.sledid == "5";
                    var db = false;
                    if (s.pathnum == pathnum)
                    {
                        if (s.pathUnitDist > pathUnitDist)
                        {
                            var newdist = s.pathUnitDist - pathUnitDist;
                            if (newdist < sledInFrontDist)
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

        void FixedUpdate()
        {
            localToWorld = transform.localToWorldMatrix;
            if (box != null)
            {
                var meth = magmo.GetHoldMethod();
                switch (meth)
                {
                    case MmHoldMethod.Physics: // FixedUpdate
                        //box.rigbod.isKinematic = false;
                         break;
                    case MmHoldMethod.Dragged: // FixedUpdate
                        if (box?.rigbod != null)
                        {
                            //if (meth==MmHoldMethod.Physics)
                            //{
                            //    box.rigbod.isKinematic = false;
                            //}
                            ////box.transform.position = formgo.transform.position;
                            //box.transform.rotation = formgo.transform.rotation;
                            box.rigbod.MovePosition(formgo.transform.position);
                            box.rigbod.MoveRotation(formgo.transform.rotation);
                        }
                        break;
                }
            }
        }
    }
}