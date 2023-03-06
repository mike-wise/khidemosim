// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.Unity;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class RobotArmSignalRDataHandler : MonoBehaviour
{
    private SignalRService rService;

    public string url = "";

    [SerializeField]
    private KinArm[] kinArms;

    private string targetDeviceId;
    private float targetAngle1;
    private float targetAngle2;
    private float targetAngle3;
    private float targetAngle4;
    private float targetAngle5;
    private float targetAngle6;

    private float prevAngle1;
    private float prevAngle2;
    private float prevAngle3;
    private float prevAngle4;
    private float prevAngle5;
    private float prevAngle6;

    private float prevTime = 0f;
    private float delta = 0f;
    private float interpolant = 0f;

    static Dictionary<string, KinUrdf> urdfDictionary = new Dictionary<string, KinUrdf>();

    private void Start()
    {
        this.RunSafeVoid(CreateServiceAsync);

        var adh = this.gameObject.GetComponent<RobotArmSignalRDataHandler>();
        if (adh != null)
        {
            //adh.SetDigestDataHandler(DataHandler);

            var kinudflist = FindObjectsOfType<KinUrdf>();
            foreach (var ku in kinudflist)
            {
                urdfDictionary[ku.name] = ku;
                Debug.Log("Found ku:" + ku.name);
            }
            
        }
        else
        {
            Debug.LogWarning("Can't find SignalR ArmDataHandler.");
        }
    }

    private void OnDestroy()
    {
        if (rService != null)
        {
            rService.OnConnected -= HandleConnected;
            rService.OnDisconnected -= HandleDisconnected;
            rService.OnRobotArmTelemetryMessage -= HandleRobotArmTelemetryMessage;
        }
    }

    private void Update()
    {
        interpolant += Time.deltaTime / 0.25f;

        interpolant = Mathf.Clamp(interpolant, 0.0f, 1.0f);

        float currentAngle1 = Mathf.Lerp(prevAngle1, targetAngle1, interpolant);
        float currentAngle2 = Mathf.Lerp(prevAngle2, targetAngle2, interpolant);
        float currentAngle3 = Mathf.Lerp(prevAngle3, targetAngle3, interpolant);
        float currentAngle4 = Mathf.Lerp(prevAngle4, targetAngle4, interpolant);
        float currentAngle5 = Mathf.Lerp(prevAngle5, targetAngle5, interpolant);
        float currentAngle6 = Mathf.Lerp(prevAngle6, targetAngle6, interpolant);

        for (int i = 0; i < kinArms.Length; i++)
        {
            kinArms[i].SetAngle(0, currentAngle1);
            kinArms[i].SetAngle(1, currentAngle2);
            kinArms[i].SetAngle(2, currentAngle3);
            kinArms[i].SetAngle(3, currentAngle4);
            kinArms[i].SetAngle(4, currentAngle5);
            kinArms[i].SetAngle(5, currentAngle6);
        }


    }

    private void HandleRobotArmTelemetryMessage(TelemetryArmData message)
    {
        UnityDispatcher.InvokeOnAppThread(() =>
        {
            Debug.Log($"id: {message.body.DeviceId} timestamp: {message.body.timestamp} ang1j: {message.body.Base} ang2j: {message.body.Shoulder} ang3j: {message.body.Elbow} ang4j: {message.body.Wrist1} ang5j: {message.body.Wrist2} ang6j: {message.body.Wrist3}");
            
            prevAngle1 = targetAngle1;
            prevAngle2 = targetAngle2;
            prevAngle3 = targetAngle3;
            prevAngle4 = targetAngle4;
            prevAngle5 = targetAngle5;
            prevAngle6 = targetAngle6;

            targetDeviceId = message.body.DeviceId;
            targetAngle1 = (float)message.body.Base;
            targetAngle2 = (float)message.body.Shoulder;
            targetAngle3 = (float)message.body.Elbow;
            targetAngle4 = (float)message.body.Wrist1;
            targetAngle5 = (float)message.body.Wrist2;
            targetAngle6 = (float)message.body.Wrist3;

            interpolant = 0f;

            //var id = "ur3e";
            var id = targetDeviceId.ToLower().Trim(new char[]{ '\"' });
            Debug.Log("Looking for urdfDictionary:" + id);

            //Find the Robot Arm by Name
            if (urdfDictionary.ContainsKey(id))
            {
                Debug.Log("Found urdfDictionary:" + id);

                var ku = urdfDictionary[id];

                if (ku.name == id)
                {
                    ku.SetAnglRadians(0, (float)message.body.Base);
                    ku.SetAnglRadians(1, (float)message.body.Shoulder);
                    ku.SetAnglRadians(2, (float)message.body.Elbow);
                    ku.SetAnglRadians(3, (float)message.body.Wrist1);
                    ku.SetAnglRadians(4, (float)message.body.Wrist2);
                    ku.SetAnglRadians(5, (float)message.body.Wrist3);
                }
            }
            else
            {
                Debug.LogWarning($"Can't find {id} for KinUrdf.");
            }
            // delta = Time.time - prevTime;
            // prevTime = Time.time;

            // Debug.Log("delta: " + delta);
        });
    }

    private Task CreateServiceAsync()
    {
        rService = new SignalRService();
        rService.OnConnected += HandleConnected;
        rService.OnDisconnected += HandleDisconnected;
        rService.OnRobotArmTelemetryMessage += HandleRobotArmTelemetryMessage;

        return rService.StartAsync(url);
    }

    private void HandleConnected(string obj)
    {
        UnityDispatcher.InvokeOnAppThread(() => Debug.Log("Connected to SignalR Service"));
    }

    private void HandleDisconnected()
    {
        UnityDispatcher.InvokeOnAppThread(() => Debug.Log("Disconnected from SignalR Service"));
    }
}