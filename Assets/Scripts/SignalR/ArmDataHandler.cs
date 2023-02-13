using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Microsoft.Unity;

public class ArmDataHandler : MonoBehaviour
{
    private SignalRService rService;

    public string url = "";

    private void Start()
    {
        this.RunSafeVoid(CreateServiceAsync);
    }

    private void OnDestroy()
    {
        if (rService != null)
        {
            rService.OnConnected -= HandleConnected;
            rService.OnTelemetryArmData -= HandleTelemetryMessage;
            rService.OnDisconnected -= HandleDisconnected;
        }
    }

    /// <summary>
    /// Received a message from SignalR. Note, this message is received on a background thread.
    /// </summary>
    /// <param name="message">
    /// The message.
    /// </param>
    private void HandleTelemetryMessage(TelemetryArmData message)
    {
        //var angles = new float[] { 1.1f, 1.2f, 1.3f, 1.4f, 1.5f, 1.6f };

        // Finally update Unity GameObjects, but this must be done on the Unity Main thread.
        UnityDispatcher.InvokeOnAppThread(() =>
        {
            string deviceName = message.body.DeviceId.Replace("\"", "").ToLower();
            var go = GameObject.Find(deviceName);

            if (go != null) {
                //FindObjectOfType<KinUrdf>()
                //var kinurf = go.GetComponent<KinUrdf>();

                //kinurf.SetAngleRadians(0, message.body.Base);
                //kinurf.SetAngleRadians(1, message.body.Shoulder);
                //kinurf.SetAngleRadians(2, message.body.Elbow);
                //kinurf.SetAngleRadians(3, message.body.Wrist1);
                //kinurf.SetAngleRadians(4, message.body.Wrist2);
                //kinurf.SetAngleRadians(5, message.body.Wrist3);



                return;
            }
        });
    }

    /// <summary>
    /// Construct the WindTurbine Data received from SignalR
    /// </summary>
    /// <param name="message">Telemetry data</param>
    /// <returns>Data values of wind turbine</returns>
    private RobotArmData CreateNewWindTurbineData(TelemetryArmData message)
    {
        RobotArmData data = new RobotArmData
        {
            DeviceId = message.body.DeviceId,
            Base = message.body.Base,
            Shoulder = message.body.Shoulder,
            Elbow = message.body.Elbow,
            Wrist1 = message.body.Wrist1,
            Wrist2 = message.body.Wrist2,
            Wrist3 = message.body.Wrist3,
            Timestamp = message.body.timestamp,
        };

        return data;
    }

    private Task CreateServiceAsync()
    {
        rService = new SignalRService();
        rService.OnConnected += HandleConnected;
        rService.OnDisconnected += HandleDisconnected;
        rService.OnTelemetryArmData += HandleTelemetryMessage;

        return rService.StartAsync(url);
    }

    private void HandleConnected(string obj)
    {
        Debug.Log("Connected");
    }

    private void HandleDisconnected()
    {
        Debug.Log("Disconnected");
    }
}