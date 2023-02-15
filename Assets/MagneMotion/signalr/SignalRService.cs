// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;

public class SignalRService
{
    public HubConnection connection;

    public event Action<string> OnConnected;
    public event Action<TelemetryArmData> OnRobotArmTelemetryMessage;
    public event Action OnDisconnected;

    ~SignalRService()
    {
        if (connection != null)
        {
            connection.StopAsync();
            connection = null;
        }
    }

    public async Task StartAsync(string url)
    {
        connection = new HubConnectionBuilder()
           .WithUrl(url)
           .Build();
        connection.On<TelemetryArmData>("NewSensorData", (message) =>
        {
            OnRobotArmTelemetryMessage?.Invoke(message);
        });
        await connection.StartAsync();
        OnConnected?.Invoke(connection.State.ToString());
        connection.Closed += async (error) =>
        {
            OnDisconnected?.Invoke();
            await connection.StartAsync();
        };
    }
}