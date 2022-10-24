using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace KhiDemo
{

    public class MmEffector : MonoBehaviour
    {
        GameObject formbase;
        MmRobot mmrobot;

        // Start is called before the first frame update
        void Start()
        {
        }

        public void Init(MmRobot robot)
        {
            mmrobot = robot;
            var sz = 0.05f;
            var sz2 = sz / 2;
            formbase = UnityUt.CreateSphere(formbase, "lilac",sz, collider:false);
            formbase.name = "formbase";
            formbase.transform.parent = this.transform;
            var xax = UnityUt.CreateSphere(formbase, "red", sz/10, collider: false);
            xax.transform.position += new Vector3(sz2, 0, 0);
            var yax = UnityUt.CreateSphere(formbase, "green", sz/10, collider: false);
            yax.transform.position += new Vector3(0, sz2, 0);
            var zax = UnityUt.CreateSphere(formbase, "blue", sz/10, collider: false);
            zax.transform.position += new Vector3(0, 0, sz2);
        }

        // Update is called once per frame
        void FixedUpdate()
        {
            if (mmrobot!=null)
            {
                var (ok, p, q) = mmrobot.GetEffectorPose();
                if (ok)
                {
                    this.transform.position = p;
                    this.transform.rotation = q;
                }
            }
        }
    }
}