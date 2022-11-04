using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Forces : MonoBehaviour
{
    // Start is called before the first frame update
    public bool doXforce = false;
    public float xForce = 100.0f;
    public bool doYforce = false;
    public float yForce = 100.0f;
    public bool doZforce = false;
    public float zForce = 100.0f;
    Rigidbody rigbod = null;

    void Start()
    {
        rigbod = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (rigbod!=null)
        {
            if (doXforce)
            {
                rigbod.AddForce(xForce * new Vector3(1, 0, 0));
                doXforce = false;
            }
            if (doYforce)
            {
                rigbod.AddForce(yForce * new Vector3(0, 1, 0));
                doYforce = false;
            }
            if (doZforce)
            {
                rigbod.AddForce(yForce * new Vector3(0, 0, 1));
                doZforce = false;
            }
        }
    }
}
