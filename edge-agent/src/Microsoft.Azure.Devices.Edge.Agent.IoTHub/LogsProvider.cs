// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    public class LogsProvider
    {
        Uri managementEndpoint;

        public LogsProvider(Uri managementEndpoint)
        {
            this.managementEndpoint = managementEndpoint;
        }

        public async Task<Stream> GetLogsStream(string module, bool follow, int? tail)
        {
            using (HttpClient httpClient = HttpClientHelper.GetHttpClient(this.managementEndpoint))
            {
                var baseUrl = HttpClientHelper.GetBaseUrl(this.managementEndpoint);
                string logsUrl = $"{baseUrl}/modules/{module}/logs?api-version={Constants.EdgeletManagementApiVersion}&follow={follow.ToString().ToLowerInvariant()}";
                if (tail.HasValue)
                {
                    logsUrl = $"{logsUrl}&tail={tail.Value}";
                }
                Console.WriteLine($"Logs url - {logsUrl}");
                var logsUri = new Uri(logsUrl);
                var httpRequest = new HttpRequestMessage(HttpMethod.Get, logsUri);
                httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage httpResponseMessage = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);
                Stream stream = await httpResponseMessage.Content.ReadAsStreamAsync();
                return stream;
            }
        }
    }
}
