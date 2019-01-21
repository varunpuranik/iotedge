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

        public LogsClient(LogsProvider logsProvier, Client.ModuleClient moduleClient)
        {
            this.logsProvider = logsProvier;
            this.moduleClient = moduleClient;
        }

        public async Task InitLogsStreaming(string moduleId)
        {
            byte[] buffer = new byte[1024];

            try
            {
                using (var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
                {
                    DeviceStreamRequest streamRequest = await this.moduleClient.WaitForDeviceStreamRequestAsync(cancellationTokenSource.Token).ConfigureAwait(false);

                    if (streamRequest != null)
                    {
                        await this.moduleClient.AcceptDeviceStreamRequestAsync(streamRequest, cancellationTokenSource.Token).ConfigureAwait(false);

                        using (ClientWebSocket webSocket = await GetStreamingClientAsync(streamRequest, cancellationTokenSource.Token).ConfigureAwait(false))
                        {
                            Stream stream = await this.logsProvider.GetLogsStream(moduleId);
                            Console.WriteLine($"Sending logs to server...");
                            int bytesRead = 0;
                            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                var arraySegment = new ArraySegment<byte>(buffer, 0, bytesRead);
                                await webSocket.SendAsync(
                                    arraySegment,
                                    WebSocketMessageType.Binary,
                                    true,
                                    CancellationToken.None);
                            }
                            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, String.Empty, cancellationTokenSource.Token).ConfigureAwait(false);
                        }
                    }

                    await this.moduleClient.CloseAsync();
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
