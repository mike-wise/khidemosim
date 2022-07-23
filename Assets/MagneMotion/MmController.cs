using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace KhiDemo
{
    public enum RobStatus { busy,idle }
    public class MmController : MonoBehaviour
    {

        [Header("Modes")]
        public MmMode mmMode = MmMode.None;
        public MmSubMode mmSubMode = MmSubMode.None;
        public MmBoxMode mmBoxMode = MmBoxMode.FakePooled;
        public bool enablePlanning = false;

        [Header("Scene Components")]
        public MmRobot mmRobot = null;
        public MmTray mmtray = null;
        MagneMotion magmo;
        MmTable mmt;

        [Header("Simulation")]
        public bool processStep;
        [Space(10)]
        public bool reverseTrayRail;
        public RobStatus robstatus;

        [Header("Speed")]
        public float shortRobMoveSec = 0.4f;
        public float longRobMoveSec = 1.0f;

        
        public void Init(MagneMotion magmo)
        {
            this.magmo = magmo;
            mmt = magmo.mmt;
            mmtray = magmo.mmtray;
            mmRobot = magmo.mmRobot;
            robstatus = RobStatus.idle;
        }
        // Start is called before the first frame update
        void Start()
        {
        }

        public void Clear()
        {
            mmRobot.Clear();
            mmt.Clear();
            mmtray.Clear();
            MmBox.Clear();
        }

        public MmBoxMode InferBoxMode(MmMode mode)
        {
            switch (mode)
            {
                case MmMode.None:
                    return MmBoxMode.FakePooled;
                case MmMode.Echo:
                    return MmBoxMode.FakePooled;
                case MmMode.SimuRailToRail:
                    return MmBoxMode.RealPooled;
                case MmMode.Planning:
                    return MmBoxMode.RealPooled;
                case MmMode.StartRailToTray:
                    return MmBoxMode.RealPooled;
                default:
                case MmMode.StartTrayToRail:
                    return MmBoxMode.RealPooled;
            }
        }

        List<MmMode> fastModes = new List<MmMode>() { MmMode.SimuRailToRail, MmMode.StartRailToTray, MmMode.StartTrayToRail };
        public void SetModeFast(MmMode newMode)
        {
            var okForFast = fastModes.Contains(magmo.mmMode) && fastModes.Contains(newMode);
            Debug.Log($"SetModeFast {newMode} okForFast:{okForFast}");
            if (okForFast)
            {
                mmMode = newMode;
                magmo.mmMode = newMode;
                switch (newMode)
                {
                    case MmMode.Planning:
                    case MmMode.SimuRailToRail:
                        mmSubMode = MmSubMode.None;
                        break;
                    case MmMode.StartRailToTray:
                        mmSubMode = MmSubMode.RailToTray;
                        break;
                    case MmMode.StartTrayToRail:
                        mmSubMode = MmSubMode.TrayToRail;
                        break;
                }
            }
            else
            {
                SetMode(newMode, clear: true);
            }
            magmo.CheckNetworkActivation();
            magmo.CheckPlanningActivation();
            CheckConsistency();
        }

        public void SetMode(MmMode newMode,bool clear=false)
        {
            Debug.Log($"SetMode {newMode} clear:{clear}");
            if (clear)
            {
                Clear();
            }
            mmMode = newMode;
            magmo.mmMode = newMode;
            mmBoxMode = InferBoxMode(newMode);
            switch (newMode)
            {
                default:
                case MmMode.Echo:
                    mmSubMode = MmSubMode.None;
                    magmo.boxForm = MmBox.BoxForm.Prefab;
                    magmo.echoMovementsRos = true;
                    magmo.enablePlanning = false;
                    magmo.publishMovementsRos = false;
                    magmo.publishMovementsZmq = false;
                    magmo.mmRobot.RealiseRobotPose(RobotPose.rest);
                    mmt.SetupSledSpeeds(SledSpeedDistrib.fixedValue, 0);

                    mmRobot.InitRobotBoxState(startLoadState:true);
                    mmt.SetupSledLoads(SledLoadDistrib.allLoaded);
                    mmtray.InitAllLoadstate(nbox: 12); 
                    break;
                case MmMode.Planning:
                    mmSubMode = MmSubMode.None;
                    magmo.boxForm = MmBox.BoxForm.PrefabWithMarkerCube;
                    magmo.echoMovementsRos = false;
                    magmo.enablePlanning = true;
                    magmo.publishMovementsRos = false;
                    magmo.publishMovementsZmq = false;
                    magmo.mmRobot.RealiseRobotPose(RobotPose.rest);
                    mmt.SetupSledSpeeds(SledSpeedDistrib.fixedValue, 0);

                    mmRobot.InitRobotBoxState(startLoadState: false);
                    mmt.SetupSledLoads(SledLoadDistrib.alternateLoadedUnloaded);
                    mmtray.InitAllLoadstate(nbox: 5);
                    break;
                case MmMode.SimuRailToRail:
                    mmSubMode = MmSubMode.None;
                    magmo.boxForm = MmBox.BoxForm.PrefabWithMarkerCube;
                    magmo.echoMovementsRos = false;
                    magmo.enablePlanning = false;
                    magmo.publishMovementsRos = false;// queue is always full
                    magmo.publishMovementsZmq = true;


                    magmo.mmRobot.RealiseRobotPose(RobotPose.rest);
                    mmt.SetupSledSpeeds(SledSpeedDistrib.alternateHiLo, 0.5f);

                    mmRobot.InitRobotBoxState(startLoadState: false);
                    mmtray.InitAllLoadstate(nbox: 5);
                    mmt.SetupSledLoads(SledLoadDistrib.alternateLoadedUnloaded);
                    break;
                case MmMode.StartRailToTray:
                    mmSubMode = MmSubMode.RailToTray;
                    magmo.echoMovementsRos = false;
                    magmo.enablePlanning = false;
                    magmo.publishMovementsRos = false;// queue is always full
                    magmo.publishMovementsZmq = true;

                    magmo.boxForm = MmBox.BoxForm.PrefabWithMarkerCube;
                    mmRobot.InitRobotBoxState(startLoadState: false);
                    mmt.SetupSledSpeeds( SledSpeedDistrib.alternateHiLo, 0.5f);
                    mmt.SetupSledLoads(SledLoadDistrib.allLoaded);
                    mmtray.InitAllLoadstate(nbox: 0);
                    magmo.mmRobot.RealiseRobotPose(RobotPose.rest);
                    break;
                case MmMode.StartTrayToRail:
                    mmSubMode = MmSubMode.TrayToRail;
                    magmo.boxForm = MmBox.BoxForm.PrefabWithMarkerCube;
                    magmo.echoMovementsRos = false;
                    magmo.enablePlanning = false;
                    magmo.publishMovementsRos = false;// queue is always full
                    magmo.publishMovementsZmq = true;

                    mmRobot.InitRobotBoxState(startLoadState: false);
                    mmt.SetupSledSpeeds( SledSpeedDistrib.alternateHiLo, 0.5f);
                    mmt.SetupSledLoads(SledLoadDistrib.allUnloaded);
                    mmtray.InitAllLoadstate(nbox: 10);
                    magmo.mmRobot.RealiseRobotPose(RobotPose.rest);
                    break;
            }
            magmo.CheckNetworkActivation();
            magmo.CheckPlanningActivation();
            CheckConsistency();
        }


        public void AdjustRobotSpeedFactor(float fak)
        {
            if (fak==0)
            {
                return;
            }
            shortRobMoveSec /= fak;
            longRobMoveSec /= fak;
        }




        public void DoReverseTrayRail()
        {
            if (mmSubMode == MmSubMode.RailToTray)
            {
                Debug.Log($"Reversing submode to {MmSubMode.TrayToRail}");
                mmSubMode = MmSubMode.TrayToRail;
            }
            else if (mmSubMode == MmSubMode.TrayToRail)
            {
                Debug.Log($"Reversing submode to {MmSubMode.RailToTray}");
                mmSubMode = MmSubMode.RailToTray;
            }
        }

        public void ReverseTrayRail()
        {
            if (!reverseTrayRail) return;
            reverseTrayRail = false;
            DoReverseTrayRail();
        }

        public void SetModeWhenIdle(MmMode newmode, bool clear = false)
        {
            StartCoroutine(CoSetModeWhenIdle(newmode,clear));
        }

        public IEnumerator CoSetModeWhenIdle(MmMode newmode,bool clear=false)
        {
            Debug.Log($"SetModeWhenIdle {newmode} stat:{robstatus}");
            yield return new WaitUntil(() => robstatus == RobStatus.idle);
            Debug.Log($"robstat now idle - stat:{robstatus}");
            robstatus = RobStatus.busy;
            try
            {
                if (clear)
                {
                    SetMode(newmode, clear);
                }
                else
                {
                    SetModeFast(newmode);
                }
            }
            finally
            {
                robstatus = RobStatus.idle;
            }
        }


        IEnumerator TransferBoxFromSledToRobot(MmMode launchMode,MmSled sled,MmRobot rob)
        {
            try
            {
                yield return new WaitUntil(() => robstatus == RobStatus.idle);
                if (Interrupt(launchMode)) yield break;

                robstatus = RobStatus.busy;
                mmRobot.RealiseRobotPose(RobotPose.fcartup);
                yield return new WaitForSeconds(shortRobMoveSec);
                if (Interrupt(launchMode)) yield break;

                mmRobot.RealiseRobotPose(RobotPose.fcartdn);
                yield return new WaitForSeconds(longRobMoveSec);
                if (Interrupt(launchMode)) yield break;

                var box = sled.DetachhBoxFromSled();
                if (box != null)
                {
                    if (Interrupt(launchMode)) yield break;
                    rob.AttachBoxToRobot(box);
                }
                mmRobot.RealiseRobotPose(RobotPose.fcartup);
                yield return new WaitForSeconds(shortRobMoveSec);
                if (Interrupt(launchMode)) yield break;

                if (mmMode == MmMode.SimuRailToRail)
                {
                    mmRobot.RealiseRobotPose(RobotPose.restr2r);
                }
                else
                {
                    mmRobot.RealiseRobotPose(RobotPose.rest);
                }
                yield return new WaitForSeconds(longRobMoveSec);
                if (Interrupt(launchMode)) yield break;

            }
            finally
            {
                coroutineCount--;
                robstatus = RobStatus.idle;
            }
        }

        IEnumerator TransferBoxFromRobotToSled(MmMode launchMode, MmRobot rob, MmSled sled)
        {
            try
            {
                yield return new WaitUntil(() => robstatus == RobStatus.idle);
                if (Interrupt(launchMode)) yield break;

                robstatus = RobStatus.busy;
                mmRobot.RealiseRobotPose(RobotPose.ecartup);
                yield return new WaitForSeconds(shortRobMoveSec);
                if (Interrupt(launchMode)) yield break;

                mmRobot.RealiseRobotPose(RobotPose.ecartdn);
                yield return new WaitForSeconds(longRobMoveSec);
                if (Interrupt(launchMode)) yield break;

                var box = rob.DetachhBoxFromRobot();
                if (box != null)
                {
                    sled.AttachBoxToSled(box);
                }
                mmRobot.RealiseRobotPose(RobotPose.ecartup);
                yield return new WaitForSeconds(shortRobMoveSec);
                if (Interrupt(launchMode)) yield break;

                if (mmMode == MmMode.SimuRailToRail)
                {
                    mmRobot.RealiseRobotPose(RobotPose.restr2r);
                }
                else
                {
                    mmRobot.RealiseRobotPose(RobotPose.rest);
                }
                yield return new WaitForSeconds(longRobMoveSec);
                if (Interrupt(launchMode)) yield break;

            }
            finally
            {
                coroutineCount--;
                robstatus = RobStatus.idle;
            }
        }

        IEnumerator TransferBoxFromTrayToRobot(MmMode launchMode, (int,int) key, MmRobot rob)
        {
            try
            {
                yield return new WaitUntil(() => robstatus == RobStatus.idle);
                if (Interrupt(launchMode)) yield break;

                robstatus = RobStatus.busy;
                var (poseup, posedn) = mmRobot.GetPoses(key);
                mmRobot.RealiseRobotPose(poseup);
                yield return new WaitForSeconds(shortRobMoveSec);
                if (Interrupt(launchMode)) yield break;

                mmRobot.RealiseRobotPose(posedn);
                yield return new WaitForSeconds(longRobMoveSec);
                if (Interrupt(launchMode)) yield break;

                var box = mmtray.DetachhBoxFromTraySlot(key);
                if (box != null)
                {
                    if (Interrupt(launchMode)) yield break;
                    rob.AttachBoxToRobot(box);
                }
                mmRobot.RealiseRobotPose(poseup);
                yield return new WaitForSeconds(shortRobMoveSec);
                if (Interrupt(launchMode)) yield break;

                mmRobot.RealiseRobotPose(RobotPose.rest);
                yield return new WaitForSeconds(longRobMoveSec);
                if (Interrupt(launchMode)) yield break;

            }
            finally
            {
                coroutineCount--;
                robstatus = RobStatus.idle;
            }
        }



        IEnumerator TransferBoxFromRobotToTray(MmMode launchMode, MmRobot rob, (int, int) key )
        {
            try
            {
                yield return new WaitUntil(() => robstatus == RobStatus.idle);
                if (Interrupt(launchMode)) yield break;
                robstatus = RobStatus.busy;
                var (poseup, posedn) = mmRobot.GetPoses(key);
                mmRobot.RealiseRobotPose(poseup);
                yield return new WaitForSeconds(shortRobMoveSec);
                if (Interrupt(launchMode)) yield break;

                mmRobot.RealiseRobotPose(posedn);
                yield return new WaitForSeconds(longRobMoveSec);
                if (Interrupt(launchMode)) yield break;

                var box = rob.DetachhBoxFromRobot();
                if (box != null)
                {
                    mmtray.AttachBoxToTraySlot(key, box);
                }
                mmRobot.RealiseRobotPose(poseup);
                yield return new WaitForSeconds(shortRobMoveSec);
                if (Interrupt(launchMode)) yield break;

                mmRobot.RealiseRobotPose(RobotPose.rest);
                yield return new WaitForSeconds(longRobMoveSec);
                if (Interrupt(launchMode)) yield break;
            }
            finally
            {
                coroutineCount--;
                robstatus = RobStatus.idle;
            }
        }



        bool Interrupt(MmMode launchMode)
        {
            if (launchMode == magmo.mmMode) return false;
            return true;
        }

        public enum TranferType { SledToRob, RobToSled, TrayToRob, RobToTray }

        public float coroutineStart;
        public int coroutineCount = 0;
        void CheckCount(string roo)
        {
            if (coroutineCount!=0)
            {
                Debug.LogWarning($"coroutineCount!=0 roo:{roo}");
            }
            coroutineCount++;
        }

        public void StartCoroutineSledTransferBox(TranferType tt, MmSled sled)
        {
            coroutineStart = Time.time;
            var rob = mmRobot;
            switch (tt)
            {
                case TranferType.SledToRob:
                    switch (mmBoxMode)
                    {
                        case MmBoxMode.FakePooled:
                            sled.SetLoadState(false);
                            rob.ActivateRobBox(true);
                            break;
                        case MmBoxMode.RealPooled:
                            CheckCount("TransferBoxFromSledToRobot");
                            StartCoroutine(TransferBoxFromSledToRobot(magmo.mmMode, sled,rob));
                            break;
                    }
                    break;
                case TranferType.RobToSled:
                    switch (mmBoxMode)
                    {
                        case MmBoxMode.FakePooled:
                            sled.SetLoadState(true);
                            rob.ActivateRobBox(false);
                            break;
                        case MmBoxMode.RealPooled:
                            CheckCount("TransferBoxFromRobotToSled");
                            StartCoroutine(TransferBoxFromRobotToSled(magmo.mmMode, rob,sled));
                            break;
                    }
                    break;
                default:
                    magmo.ErrMsg("SledTransferBox - Wrong Function");
                    break;
            }
        }

        public void StartCoroutineTrayTransferBox(TranferType tt, (int, int) key)
        {
            coroutineStart = Time.time;
            var rob = mmRobot;
            switch (tt)
            {
                case TranferType.TrayToRob:

                    switch (mmBoxMode)
                    {
                        case MmBoxMode.FakePooled:
                            mmtray.SetVal(key, false);
                            rob.ActivateRobBox(true);
                            break;
                        case MmBoxMode.RealPooled:
                            Debug.Log($"TransferBoxFromTrayToRobot {key}");
                            //yield return new WaitUntil(() => robstatus == RobStatus.idle);
                            CheckCount("TransferBoxFromTrayToRobot");
                            StartCoroutine(TransferBoxFromTrayToRobot(magmo.mmMode, key, rob));
                            break;
                    }
                    break;
                case TranferType.RobToTray:
                    switch (mmBoxMode)
                    {
                        case MmBoxMode.FakePooled:
                            mmtray.SetVal(key, true);
                            rob.ActivateRobBox(false);
                            break;
                        case MmBoxMode.RealPooled:
                            CheckCount("TransferBoxFromRobotToTray");
                            StartCoroutine(TransferBoxFromRobotToTray(magmo.mmMode,rob,key));
                            break;
                    }
                    break;
                default:
                    magmo.ErrMsg("TrayTransferBox - Wrong Function");
                    break;
            }
        }

        public bool CheckConsistency()
        {
            if (mmMode==MmMode.None || mmMode == MmMode.Echo)
            {
                if (mmBoxMode == MmBoxMode.RealPooled)
                {
                    var emsg2 = $"ChkCon error: unsupported mode mode:{mmMode} boxMode:{mmBoxMode}";
                    magmo.ErrMsg(emsg2);
                    return false;
                }
                return true;
            }
            if (mmBoxMode == MmBoxMode.FakePooled)
            {
                var emsg1 = $"ChkCon error: unsupported mode mode:{mmMode} boxMode:{mmBoxMode}";
                magmo.ErrMsg(emsg1);
                return false;
            }
            bool rv = false;
            var nTray = mmtray.CountLoaded();
            var nSled = mmt.CountLoadedSleds();
            var nRob = mmRobot.IsLoaded() ? 1 : 0;
            var emsg = $"ChkCon error: boxMode:{mmBoxMode} ntray:{nTray} nsled:{nSled} nrob:{nRob}";
            rv = (nTray + nSled + nRob)==10;
            if (!rv)
            {
                magmo.ErrMsg($"{emsg} does not sum to 10");
                //magmo.stopSimulation = true;
                
            }
            var (nFreeFake,nFreeReal, nTrayCbs, nRobCbs, nSledCbs) = MmBox.CountBoxStatus();
            if (mmBoxMode == MmBoxMode.FakePooled && nFreeFake != 0)
            {
                magmo.ErrMsg($"{emsg}  Fake Boxes got away - nFreeFake:{nFreeFake}");
                (nFreeFake, nFreeReal, nTrayCbs, nRobCbs, nSledCbs) = MmBox.CountBoxStatus();
            }
            if (mmBoxMode == MmBoxMode.RealPooled && nFreeReal != 0)
            {
                magmo.ErrMsg($"{emsg}  Real Boxes got away - nFreeReal:{nFreeReal}");
                (nFreeFake, nFreeReal, nTrayCbs, nRobCbs, nSledCbs) = MmBox.CountBoxStatus();
            }
            if (nTrayCbs!=nTray)
            {
                magmo.ErrMsg($"{emsg}  nTrayCbs:{nTrayCbs} not equal to nTray");
                (nFreeFake, nFreeReal, nTrayCbs, nRobCbs, nSledCbs) = MmBox.CountBoxStatus();
                //magmo.stopSimulation = true;
            }
            if (nRobCbs != nRob)
            {
                magmo.ErrMsg($"{emsg}  nRobCbs:{nRobCbs} not equal to nRob");
                (nFreeFake, nFreeReal, nTrayCbs, nRobCbs, nSledCbs) = MmBox.CountBoxStatus();
                //magmo.stopSimulation = true;
            }
            if (nSledCbs != nSled)
            {
                magmo.ErrMsg($"{emsg}  nSledCbs:{nSledCbs} not equal to nSled");
                (nFreeFake, nFreeReal, nTrayCbs, nRobCbs, nSledCbs) = MmBox.CountBoxStatus();
                //magmo.stopSimulation = true;
            }
            mmt.CheckConsistency();
            mmtray.CheckConsistency();
            return rv;
        }

        public bool CheckIfOkayForNextProcessStep()
        {
            var rv = false;
            var (nloadedstopped, nunloadedstopped) = mmt.CountStoppedSleds();
            var (_,_, nTray, nRob, nSled) = MmBox.CountBoxStatus();
            try
            {
                if (robstatus != RobStatus.idle)
                {
                    return rv;
                }
                //Debug.Log($"CountBoxStatus nTray:{nTray} nRob:{nRob} nSled:{nSled}");
                if (mmSubMode == MmSubMode.RailToTray && nSled == 0 && nRob == 0)
                {
                    mmSubMode = MmSubMode.TrayToRail;
                    //Debug.Log($"   Switched to {mmSubMode}");
                }
                else if (mmSubMode == MmSubMode.TrayToRail && nTray == 0 && nRob == 0)
                {
                    mmSubMode = MmSubMode.RailToTray;
                    //Debug.Log($"   Switched to {mmSubMode}");
                }
                switch (mmMode)
                {
                    case MmMode.Planning:
                        {
                            rv = false;
                            return rv;
                        }
                    //case MmMode.SimNew:
                    case MmMode.SimuRailToRail:
                        {
                            rv = (nloadedstopped > 0 && nunloadedstopped > 0);
                            return rv;
                        }
                    case MmMode.StartRailToTray:
                    case MmMode.StartTrayToRail:
                        {

                            switch (mmSubMode)
                            {
                                case MmSubMode.RailToTray:
                                    rv = nloadedstopped > 0 || nRob > 0;
                                    return rv;
                                case MmSubMode.TrayToRail:
                                    rv = nunloadedstopped > 0 && (nTray + nRob) > 0;
                                    return rv;
                            }
                        }
                        break;
                }
                return rv;
            }
            finally
            {
                //var msg = $"CIOFNP {mmMode} {mmSubMode} - nls:{nloadedstopped} nus:{nunloadedstopped} nTray:{nTray} nRob:{nRob} nSled:{nSled} rv:{rv}";
                //Debug.Log(msg);
            }
        }

        public void ProcessStep()
        {
            CheckConsistency();
            //if (!processStep) return;
            //processStep = false;
            if (!CheckIfOkayForNextProcessStep()) return;
            //Debug.Log($"ProcessStep mode:{mmMode}");
            if (Time.time - coroutineStart < 0.1f) return;

            var errhead = $"MagneMotion.demoStep - {mmMode}";
            var rob = mmRobot;
            switch (mmMode)
            {
                //case MmMode.SimNew:
                case MmMode.Planning:
                    {
                        break;
                    }
                case MmMode.SimuRailToRail:
                    {
                        if (!rob.loadState)
                        {
                            var s = mmt.FindStoppedSled(neededLoadState: true);
                            if (s == null)
                            {
                                magmo.WarnMsg($"{errhead} - cound not find stoppedsled with loadState {true} to load");
                                return;
                            }
                            StartCoroutineSledTransferBox(TranferType.SledToRob, s);
                        }
                        else
                        {
                            var s = mmt.FindStoppedSled(neededLoadState: false);
                            if (s == null)
                            {
                                magmo.WarnMsg($"{errhead} - cound not find stoppedsled with loadState {false} to unload");
                                return;
                            }
                            StartCoroutineSledTransferBox(TranferType.RobToSled, s);
                        }
                        break;
                    }
                case MmMode.StartRailToTray:
                case MmMode.StartTrayToRail:
                    {
                        switch (mmSubMode)
                        {
                            case MmSubMode.RailToTray:
                                {
                                    if (!rob.loadState)
                                    {
                                        var s = mmt.FindStoppedSled(neededLoadState: true);
                                        if (s == null)
                                        {
                                            magmo.WarnMsg($"{errhead}  - cound not find stoppedsled with loadState {true} to unload");
                                            return;
                                        }
                                        StartCoroutineSledTransferBox(TranferType.SledToRob, s);
                                    }
                                    else
                                    {
                                        var (found, key) = mmtray.FindFirst(seekLoadState: false);
                                        if (!found)
                                        {
                                            magmo.WarnMsg($"{errhead}  - cound not find empty tray slot");
                                            return;
                                        }
                                        if (found)
                                        {
                                            StartCoroutineTrayTransferBox(TranferType.RobToTray, key);
                                        }
                                    }
                                    break;
                                }
                            case MmSubMode.TrayToRail:
                                {
                                    if (!rob.loadState)
                                    {
                                        var (found, key) = mmtray.FindFirst(seekLoadState: true);
                                        if (!found)
                                        {
                                            magmo.WarnMsg($"{errhead}  - cound not find loaded tray slot to unload");
                                            return;
                                        }
                                        StartCoroutineTrayTransferBox(TranferType.TrayToRob, key);
                                    }
                                    else
                                    {
                                        var s = mmt.FindStoppedSled(neededLoadState: false);
                                        if (s == null)
                                        {
                                            magmo.WarnMsg($"{errhead}  - cound not find stoppedsled with loadState {false} to load");
                                            return;
                                        }
                                        StartCoroutineSledTransferBox(TranferType.RobToSled, s);
                                    }
                                    break;
                                }
                        }
                        break;
                    }
            }
        }


        public void PhysicsStep()
        {
            ReverseTrayRail();

        }

        // Update is called once per frame
        void Update()
        {
            if (!magmo.stopSimulation)
            {
                ProcessStep();
            }
        }
    }
}