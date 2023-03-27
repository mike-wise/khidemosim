using System;


//  {
//  "body": {
//    "Base": -3.580590311680929,
//    "Shoulder": -0.5092846912196656,
//    "Elbow": 2.2845593134509485,
//    "Wrist1": -3.3458086452879847,
//    "Wrist2": 4.712616443634033,
//    "Wrist3": 4.26494026184082,
//    "timestamp": "2023-01-30T10:09:09.212087-06:00"
//  },
//  "enqueuedTime": "Mon Jan 30 2023 10:09:09 GMT-0600 (Central Standard Time)"
//}

[Serializable]
public class TelemetryArmData
{
    public string enqueuedTime { get; set; }

    public Body body { get; set; }
}

[Serializable]
public class Body
{
    public string DeviceId { get; set; }
    public double Base { get; set; }
    public double Shoulder { get; set; }
    public double Elbow { get; set; }
    public double Wrist1 { get; set; }
    public double Wrist2 { get; set; }
    public double Wrist3 { get; set; }
    public string timestamp { get; set; }
}
