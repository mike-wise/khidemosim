using System;
using UnityEngine;

/// <summary>
/// Robot Arm Data information
/// </summary>
[Serializable]
public class RobotArmData
{
    /// <summary>
    /// Gets or sets Robot Arm Device Id
    /// </summary>
    [field: SerializeField]
    public string DeviceId { get; set; }

    /// <summary>
    /// Gets or sets Base Joint in Radians
    /// </summary>
    [field: SerializeField]
    public double Base { get; set; }

    /// <summary>
    /// Gets or sets Shoulder Joint in Radians
    /// </summary>
    [field: SerializeField]
    public double Shoulder { get; set; }

    /// <summary>
    /// Gets or sets Elbow Joint in Radians
    /// </summary>
    [field: SerializeField]
    public double Elbow { get; set; }

    /// <summary>
    /// Gets or sets Wrist 1 Joint in Radians
    /// </summary>
    [field: SerializeField]
    public double Wrist1 { get; set; }

    /// <summary>
    /// Gets or sets Wrist 2 Joint in Radians
    /// </summary>
    [field: SerializeField]
    public double Wrist2 { get; set; }

    /// <summary>
    /// Gets or sets Wrist 3 Joint in Radians
    /// </summary>
    [field: SerializeField]
    public double Wrist3 { get; set; }

    /// <summary>
    /// Gets or sets Timestamp
    /// </summary>
    [field: SerializeField]
    public string Timestamp { get; set; }
}