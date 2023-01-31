using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KinArmCtrl : MonoBehaviour
{
    // Start is called before the first frame update
    public bool startRanMovment = false;
    public bool forceNoClip = false;

    void StartRanMovment()
    {
        var kinarmlist = FindObjectsOfType<KinArm>();
        foreach( var ka in kinarmlist)
        {
            ka.continuousMove = true;
            ka.newRanPoint = true;
        }
        var kinudflist = FindObjectsOfType<KinUrdf>();
        foreach (var ka in kinudflist)
        {
            ka.continuousMove = true;
            ka.newRanPoint = true;
            if (forceNoClip)
            {
                ka.clipJoints = false;
            }
        }
    }

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (startRanMovment)
        {
            StartRanMovment();
            startRanMovment = false;
        }
    }
}
