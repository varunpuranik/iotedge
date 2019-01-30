// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.IO;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;

    public class LogsClient
    {
        LogsProvider logsProvider;
        Client.ModuleClient moduleClient;
        Client.DeviceClient deviceClient;

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

        public async Task InitLogsStreaming(string moduleId, Stream logsStream)
        {
            byte[] buffer = new byte[1024];

            try
            {
                Console.WriteLine($"Initializing stream for logs.. ");
                DeviceStreamRequest streamRequest = await this.deviceClient.WaitForDeviceStreamRequestAsync(CancellationToken.None).ConfigureAwait(false);
                if (streamRequest != null)
                {
                    Console.WriteLine($"Accepting stream request.. ");
                    await this.deviceClient.AcceptDeviceStreamRequestAsync(streamRequest, CancellationToken.None).ConfigureAwait(false);

                    using (ClientWebSocket webSocket = await GetStreamingClientAsync(streamRequest, CancellationToken.None).ConfigureAwait(false))
                    {                        
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
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
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
