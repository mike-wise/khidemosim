using System.Collections.Generic;
using UnityEngine;
using MmTray1Msg = RosMessageTypes.Rs007Control.MagneMotionTray1Msg;
using Unity.Robotics.ROSTCPConnector;

namespace KhiDemo
{

    public class MmTray : MonoBehaviour
    {
        MagneMotion magmo;
        MmTable mmt;

        public enum TrayBoxForm { Box, Prefab, BoxComp }

        public GameObject mmtrayrep;
        public GameObject mmtraygo;

        const int nrow = 3;
        const int ncol = 4;
        Dictionary<(int, int), bool> loadState = new Dictionary<(int, int), bool>();
        Dictionary<(int, int), MmBox> trayboxes = new Dictionary<(int, int), MmBox>();
        Dictionary<(int, int), GameObject> trayslots = new Dictionary<(int, int), GameObject>();

        public MmTray()
        {
            InitVals();
        }

        (bool ok, string errmsg) CheckIndexes(int i, int j, string rooname)
        {
            if (i < 0 || nrow <= i)
            {
                var msg = $"row out of rannge:{i} max:{nrow - 1} in {rooname}";
                return (false, msg);
            }
            if (j < 0 || ncol <= j)
            {
                var msg = $"col out of rannge:{j} max:{ncol - 1} in {rooname}";
                return (false, msg);
            }
            return (true, "");
        }


        void Start()
        {
        }

        public void Init(MagneMotion magmo)
        {
            this.magmo = magmo;
            this.mmt = magmo.mmt;
            //magmo.rosconnection.RegisterPublisher<MmTray1Msg>("Rs007Tray1");
            // size in cm - 31.4, 28.9, 1.7
            CreateTray(transform);
            CreateTraySlots();
        }

        public void SubscribeToRos()
        {
            magmo.rosconnection.Subscribe<MmTray1Msg>("Rs007Tray1", Tray1Change);
        }

        public void PublishTray()
        {
            if (magmo.publishMovementsRos)
            {
                for (var i = 0; i < nrow; i++)
                {
                    for (var j = 0; j < ncol; j++)
                    {
                        var key = (i, j);
                        var loaded = loadState[key] ? 1 : 0;
                        var t1msg = new MmTray1Msg(i,j,loaded);
                        //magmo.rosconnection.Publish("Rs007Tray1", t1msg);
                    }
                }
            }
        }

        public void PublishTrayZmq()
        {
            if (magmo.publishMovementsZmq)
            {
                var msg = "trayjson|[";
                var ntray = 1;
                for (var i = 0; i < nrow; i++)
                {
                    for (var j = 0; j < ncol; j++)
                    {
                        var key = (i, j);
                        var loaded = loadState[key] ? "true" : "false";
                        if (ntray<12)
                        {
                            msg += $"{loaded},";
                        }
                        else
                        {
                            msg += $"{loaded}]";
                        }
                        ntray++;
                    }
                }
                magmo.ZmqSendString(msg);
            }
        }


        public void Clear()
        {
            switch (magmo.mmctrl.mmBoxMode)
            {
                case MmBoxMode.RealPooled:
                case MmBoxMode.FakePooled:
                    ReturnBoxesToPool();
                    break;
            }
        }

        void CreateTray(Transform parent)
        {
            mmtrayrep = new GameObject("mmtrayrep");

            mmtrayrep.transform.SetParent(parent, worldPositionStays: false);

            mmtraygo = UnityUt.CreateCube(mmtrayrep, "gray", size: 1, wps: false);
            mmtraygo.name = "mmtraygo";
            mmtraygo.transform.localScale = new Vector3(0.289f, 0.017f, 0.314f);
        }
        float slotw;
        float sloth;
        void CreateTraySlots()
        {
            var rowdelt = 0.03f * 3;
            //var coldelt = 0.022f * 3;
            var coldelt = 0.022f * 3;
            var rowstar = rowdelt * (((float)nrow - 1) / 2f);
            //var colmid = 0.003f;
            var colmid = 0.014f;
            var colstar = coldelt * (((float)ncol - 1) / 2f);
            var rowpos = rowstar;
            slotw = coldelt * 0.95f;
            sloth = rowdelt * 0.95f;
            for (var i = 0; i < nrow; i++)
            {
                var colpos = colstar;
                for (var j = 0; j < ncol; j++)
                {
                    var key = (i, j);
                    // so the columns are not evenly distributed (sigh)
                    var colpos1 = colpos - colmid;
                    if (j < (ncol / 2))
                    {
                        colpos1 = colpos + colmid;
                    }
                    var pt = new Vector3(colpos1, 0.02f, rowpos);
                    CreateTraySlot(key, pt);


                    colpos -= coldelt;
                }
                rowpos -= rowdelt;
            }
        }

        void CreateTraySlot((int,int) key, Vector3 pt)
        {
            var slotgo = new GameObject($"slot:{key}");
            trayslots[key] = slotgo;
            slotgo.transform.position = pt;
            slotgo.transform.SetParent(mmtrayrep.transform, worldPositionStays: false);

            var slotformgo = UnityUt.CreateCube(null, "gray", size: 1);
            slotformgo.transform.localScale = new Vector3(slotw, 0.005f, sloth);
            slotformgo.transform.SetParent(slotgo.transform, worldPositionStays: false);

            var gobx = UnityUt.CreateCube(null, "red", size: 0.01f);
            gobx.name = "centercube";
            gobx.transform.position = new Vector3(0, 0.0164f, 0);
            gobx.transform.SetParent(slotformgo.transform, worldPositionStays: false);
            var sz = 0.01f;
            gobx.transform.localScale = new Vector3(sz/slotw, sz/0.005f, sz/sloth);

        }


        void ReturnBoxesToPool()
        {
            for (var i = 0; i < nrow; i++)
            {
                for (var j = 0; j < ncol; j++)
                {
                    var key = (i, j);
                    if (trayboxes.ContainsKey(key))
                    {
                        if (trayboxes[key] != null)
                        {
                            MmBox.ReturnToPool(trayboxes[key]);
                        }
                        trayboxes[key] = null;
                    }
                    else
                    {
                        trayboxes[key] = null;
                    }
                    loadState[key] = false;
                }
            }
        }



        void GetBoxesFromPool(int nbox)
        {
            
            var ncreated = 0;
            for (var i = 0; i < nrow; i++)
            {
                for (var j = 0; j < ncol; j++)
                {
                    var key = (i, j);
                    if (ncreated < nbox)
                    {
                        var boxid = $"{key}";
                        var box = MmBox.GetFreePooledBox( BoxStatus.onTray);
                        if (box != null)
                        {
                            AttachBoxToTraySlot(key, box);
                            trayboxes[key] = box;
                            loadState[key] = true;
                            ncreated++;
                        }
                    }
                    else
                    {
                        trayboxes[key] = null;
                        loadState[key] = false;
                    }
                }
            }
            if (ncreated != nbox)
            {
                magmo.WarnMsg($"MnTray.GetBoxesFromBool didn't create enough boxes requested:{nbox} created:{ncreated}");
            }
        }

        public void AttachBoxToTraySlot((int,int) slotkey, MmBox box)
        {
            var slot = trayslots[slotkey];
            //box.transform.localRotation = Quaternion.Euler(90, 0, 0);
            //box.transform.parent = null;
            box.transform.parent = null;
            box.transform.rotation = Quaternion.Euler(90, 0, 0);
            box.transform.position = Vector3.zero;
            box.transform.SetParent(slot.transform, worldPositionStays: false);
            trayboxes[slotkey] = box;
            loadState[slotkey] = true;
            box.SetBoxStatus(BoxStatus.onTray);
        }

        public MmBox DetachhBoxFromTraySlot((int, int) slotkey)
        {
            var box = trayboxes[slotkey];
            trayboxes[slotkey] = null;
            loadState[slotkey] = false;
            if (box == null)
            {
                magmo.ErrMsg($"Trying to detach null box from Trayslot:{slotkey}");
            }
            else
            {
                box.SetBoxStatus(BoxStatus.free);
            }
            return box;
        }

        public bool CheckTraySlotConsistency((int,int) key)
        {
            var mode = magmo.mmctrl.mmBoxMode;
            var ls = loadState[key];
            switch (mode)
            {
                case MmBoxMode.FakePooled:
                    if (trayboxes[key] != null)
                    {
                        magmo.ErrMsg($"Trayslot{key} inconsistent loadstate:{ls} but traybox is null - mode:{mode}");
                        return false;
                    }
                    break;
                case MmBoxMode.RealPooled:
                    if (ls)
                    {
                        if (trayboxes[key] == null)
                        {
                            magmo.ErrMsg($"Trayslot{key} inconsistent loadstate:{ls} but traybox is null - mode:{mode}");
                            return false;
                        }
                    }
                    else
                    {
                        if (trayboxes[key] != null)
                        {
                            magmo.ErrMsg($"Trayslot{key} inconsistent ltraybox is not null for fake mode - mode:{mode}");
                            return false;
                        }
                    }
                    break;
            }
            return true;
        }

        public bool CheckConsistency()
        {
            var rv = true;
            for (var i = 0; i < nrow; i++)
            {
                for (var j = 0; j < ncol; j++)
                {
                    var bkey = (i, j);
                    rv = rv && CheckTraySlotConsistency(bkey);
                }
            }
            return rv;
        }


        public void InitAllLoadstate(int nbox = 12)
        {
            //Debug.Log($"InitAllLoadStatenbox:{nbox}");
            switch (magmo.mmctrl.mmBoxMode)
            {
                case MmBoxMode.RealPooled:
                case MmBoxMode.FakePooled:
                    {
                        GetBoxesFromPool(nbox);
                        break;
                    }
            }
        }

        public int CountLoaded()
        {
            var rv = 0;
            for (var i = 0; i < nrow; i++)
            {
                for (var j = 0; j < ncol; j++)
                {
                    var bkey = (i, j);
                    if (loadState[bkey])
                    {
                        rv++;
                    }
                }
            }
            return rv;
        }

        void RealizeLoadStatusIj(int i, int j)
        {
            var key = (i, j);
            var oldstat = trayboxes[key].gameObject.activeSelf;
            var newstat = loadState[key];
            if (oldstat != newstat)
            {
                SetVal(key, newstat);
                trayboxes[key].gameObject.SetActive(newstat);
            }
        }

        void Tray1Change(MmTray1Msg traymsg)
        {
            if (magmo.echoMovementsRos)
            {
                //Debug.Log($"Received ROS message on topic Rs007Tray1:{traymsg.ToString()}");
                var (ok, msg) = CheckIndexes(traymsg.row, traymsg.col, "Tray1Change");
                if (ok)
                {
                    var oldstat = GetVal(traymsg.row, traymsg.col);
                    var newstat = traymsg.loaded != 0;
                    //Debug.Log($"   oldstat:{oldstat} newstat:{newstat}");
                    if (oldstat != newstat)
                    {
                        var key = (traymsg.row, traymsg.col);
                        loadState[key] = newstat;
                        RealizeLoadStatusIj(traymsg.row, traymsg.col);
                        magmo.mmRobot.ActivateRobBox(!newstat);
                        //RealizeLoadStatus();
                    }
                }
                else
                {
                    magmo.ErrMsg(msg);
                }
            }
        }

        bool GetVal(int i, int j)
        {
            var key = (i, j);
            if (!loadState.ContainsKey(key)) return false;
            var rv = loadState[key];
            return rv;
        }

        public void SetVal(int i, int j, bool newstat)
        {
            SetVal((i, j), newstat);
        }
        public void SetVal((int i, int j) key, bool newstat)
        {
            loadState[key] = newstat;
            if (trayboxes.ContainsKey(key))
            {
                trayboxes[key].gameObject.SetActive(newstat);
            }
        }
        void InitVals()
        {
            for (int i = 0; i < nrow; i++)
            {
                for (int j = 0; j < ncol; j++)
                {
                    loadState[(i, j)] = false;
                }
            }
        }

        public (bool found,(int i,int j)) FindFirst(bool seekLoadState)
        {
            for (int i = 0; i < nrow; i++)
            {
                for (int j = 0; j < ncol; j++)
                {
                    if (loadState[(i, j)] == seekLoadState)
                    {
                        return (true,(i, j));
                    }
                }
            }
            return (false, (-1,-1));
        }
    }
}