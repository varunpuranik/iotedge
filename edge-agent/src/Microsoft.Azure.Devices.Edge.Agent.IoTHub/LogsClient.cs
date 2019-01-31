// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Newtonsoft.Json;

    public class LogsClient
    {
        LogsProvider logsProvider;
        Client.ModuleClient moduleClient;
        Client.DeviceClient deviceClient;
        List<Task> streamTasks = new List<Task>();

        public LogsClient(LogsProvider logsProvier, Client.ModuleClient moduleClient)
        {
            this.logsProvider = logsProvier;
            this.moduleClient = moduleClient;
            string connectionString = Environment.GetEnvironmentVariable("DeviceConnectionString");
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                this.deviceClient = DeviceClient.CreateFromConnectionString(connectionString);
                Console.WriteLine("Using DeviceClient for streams");
            }
        }

        public async void Init()
        {
            try
            {
                await this.InitLogsStreaming();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        async Task InitLogsStreaming()
        {
            while (true)
            {
                try
                {
                    CancellationToken cts = CancellationToken.None;
                    
                    Console.WriteLine($"Waiting for stream request...");
                    DeviceStreamRequest streamRequest = await this.deviceClient.WaitForDeviceStreamRequestAsync(cts).ConfigureAwait(false);
                    Console.WriteLine("Received stream request.. ");
                    if (streamRequest != null)
                    {
                        Task streamTask = this.StartStream(streamRequest);
                        this.streamTasks.Add(streamTask);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }

        }

        async Task StartStream(DeviceStreamRequest streamRequest)
        {
            Console.WriteLine($"Accepting stream request.. ");
            await this.deviceClient.AcceptDeviceStreamRequestAsync(streamRequest, CancellationToken.None).ConfigureAwait(false);

            using (ClientWebSocket webSocket = await GetStreamingClientAsync(streamRequest, CancellationToken.None).ConfigureAwait(false))
            {
                byte[] buffer = new byte[1024];
                WebSocketReceiveResult receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), CancellationToken.None).ConfigureAwait(false);
                string receivedPayload = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
                Console.WriteLine($"Received stream data: {receivedPayload}");

                EdgeAgentConnection.LogsRequestData logsRequestData = JsonConvert.DeserializeObject<EdgeAgentConnection.LogsRequestData>(receivedPayload);
                Console.WriteLine($"Streaming for moduleId: {logsRequestData.ModuleId} - tail: {logsRequestData.Tail}");
                Stream logsStream = await this.logsProvider.GetLogsStream(logsRequestData.ModuleId, true, logsRequestData.Tail);
                Console.WriteLine($"Sending logs to server...");
                int bytesRead;
                while ((bytesRead = await logsStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    var arraySegment = new ArraySegment<byte>(buffer, 0, bytesRead);
                    await webSocket.SendAsync(
                        arraySegment,
                        WebSocketMessageType.Binary,
                        true,
                        CancellationToken.None);
                }
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, String.Empty, CancellationToken.None).ConfigureAwait(false);
            }
        }

        public static async Task<ClientWebSocket> GetStreamingClientAsync(DeviceStreamRequest streamRequest, CancellationToken cancellationToken)
        {
            ClientWebSocket wsClient = new ClientWebSocket();
            wsClient.Options.SetRequestHeader("Authorization", "Bearer " + streamRequest.AuthorizationToken);

            await wsClient.ConnectAsync(streamRequest.Url, cancellationToken).ConfigureAwait(false);

            return wsClient;
        }

    }
}
