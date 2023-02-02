using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KinArmCtrl : MonoBehaviour
{
    // Start is called before the first frame update
    public enum MoveStyles {  random, rail2rail, ur90s }
    public MoveStyles moveStyle = MoveStyles.random;
    public bool startMovement = false;
    public bool stopMovement = false;
    public bool forceNoClip = false;
    public List<RobotPose> poses;
    public RobotPoses robpose;

    void InitMovement()
    {
        poses = new List<RobotPose>();
    }

    List<RobotPose> GetPoseList(MoveStyles movestyle)
    {
        List<RobotPose> rv = null;
        switch(moveStyle)
        {
            case MoveStyles.random:
                rv = new List<RobotPose>();
                break;
            case MoveStyles.rail2rail:
                rv = new List<RobotPose>() 
                {   
                    RobotPose.restr2r,
                    RobotPose.ecartup, RobotPose.ecartdn, RobotPose.ecartup,
                    RobotPose.restr2r,
                    RobotPose.fcartup,RobotPose.fcartdn, RobotPose.fcartup,
                    RobotPose.restr2r 
                };
                break;
            case MoveStyles.ur90s:
                rv = new List<RobotPose>()
                {
                    //     Pose000000, Pose099900, Pose090000, Pose099999, Pose099000, Pose099990, Pose999999,

                    RobotPose.Pose000000,
                    RobotPose.Pose000000,

                    RobotPose.Pose099900,
                    RobotPose.Pose099900,

                    RobotPose.Pose090000,
                    RobotPose.Pose090000,

                    RobotPose.Pose099999,
                    RobotPose.Pose099999,

                    RobotPose.Pose099000,
                    RobotPose.Pose099000,

                    RobotPose.Pose099990,
                    RobotPose.Pose099990,

                    RobotPose.Pose999999,
                    RobotPose.Pose999999
                };
                break;
        }
        return rv;
    }

    void StartMovement()
    {
        var kinarmlist = FindObjectsOfType<KinArm>();
        foreach( var ka in kinarmlist)
        {
            ka.continuousMove = true;
            ka.SetupRandomPointToMoveTowards();
            switch (moveStyle)
            {
                case MoveStyles.random:
                    ka.SetupRandomPointToMoveTowards();
                    break;
                case MoveStyles.ur90s:
                    {
                        var poses = GetPoseList(moveStyle);
                        ka.SetupMovePoseSequence(robpose, poses, 5);
                        break;
                    }
                case MoveStyles.rail2rail:
                    {
                        var poses = GetPoseList(moveStyle);
                        ka.SetupMovePoseSequence(robpose, poses, 1);
                        break;
                    }
            }
            ka.StartMovment();
        }
        var kinudflist = FindObjectsOfType<KinUrdf>();
        foreach (var ka in kinudflist)
        {
            ka.continuousMove = true;
            switch (moveStyle)
            {
                case MoveStyles.random:
                    ka.SetupRandomPointToMoveTowards();
                    break;
                case MoveStyles.ur90s:
                    {
                        var poses = GetPoseList(moveStyle);
                        ka.SetupMovePoseSequence(robpose, poses, 5);
                        break;
                    }
                case MoveStyles.rail2rail:
                    {
                        var poses = GetPoseList(moveStyle);
                        ka.SetupMovePoseSequence(robpose, poses, 1);
                        break;
                    }

            }
            ka.StartMovment();
            if (forceNoClip)
            {
                ka.clipJoints = false;
            }
        }
    }

    void StopMovement()
    {
        var kinarmlist = FindObjectsOfType<KinArm>();
        foreach (var ka in kinarmlist)
        {
            ka.StopMovement();
        }
        var kinudflist = FindObjectsOfType<KinUrdf>();
        foreach (var ku in kinudflist)
        {
            ku.StopMovement();
        }
    }


    void Start()
    {
        robpose = this.gameObject.AddComponent<RobotPoses>();
        robpose.InitializePoses();
    }

    // Update is called once per frame
    void Update()
    {
        if (startMovement)
        {
            InitMovement();
            StartMovement();
            startMovement = false;
            stopMovement = false;
        }
        if (stopMovement)
        {
            StopMovement();
            stopMovement = false;
            startMovement = false;
        }
    }
}
