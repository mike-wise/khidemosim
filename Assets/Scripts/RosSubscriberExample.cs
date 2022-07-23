using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosColor = RosMessageTypes.Rs007Control.UnityColorMsg;
using RsJ1Msg  = RosMessageTypes.Rs007Control.Rs007Joints1Msg;
using RsJ6Msg = RosMessageTypes.Rs007Control.Rs007Joints6Msg;

public class RosSubscriberExample : MonoBehaviour
{
    public GameObject cube;

    void Start()
    {
        ROSConnection.GetOrCreateInstance().Subscribe<RosColor>("color", ColorChange);
        ROSConnection.GetOrCreateInstance().Subscribe<RsJ1Msg>("Rs007Joints1", Rs007J1Change);
        ROSConnection.GetOrCreateInstance().Subscribe<RsJ6Msg>("Rs007Joints6", Rs007J6Change);
    }

    void ColorChange(RosColor colorMessage)
    {
        Debug.Log($"RosColor:{colorMessage.ToString()}");
        cube.GetComponent<Renderer>().material.color = new Color32((byte)colorMessage.r, (byte)colorMessage.g, (byte)colorMessage.b, (byte)colorMessage.a);
    }

    void Rs007J1Change(RsJ1Msg j1msg)
    {
        Debug.Log($"RsJ1Msg:{j1msg.ToString()}");
    }

    void Rs007J6Change(RsJ6Msg j6msg)
    {
        Debug.Log($"RsJ6Msg:{j6msg.ToString()}");
    }
}