// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class EdgeAgentConnectionLogsManager
    {
        readonly IModuleLogsProvider moduleLogsProvider;

        public EdgeAgentConnectionLogsManager(IModuleLogsProvider moduleLogsProvider)
        {
            this.moduleLogsProvider = moduleLogsProvider;
        }

        public Task Init(IModuleClient moduleClient)
        {
            return moduleClient.SetMethodHandlerAsync("Logs", this.GetLogsCallback);
        }

        async Task<MethodResponse> GetLogsCallback(MethodRequest methodRequest, object usercontext)
        {
            Console.WriteLine("Received request to get logs...");
            try
            {
                var logsRequestData = methodRequest.DataAsJson.FromJson<LogsRequestData>();
                Console.WriteLine($"Getting logs without follow");
                string logs = await this.moduleLogsProvider.GetLogs(logsRequestData.ModuleId, Option.None<int>());
                Console.WriteLine($"Logs for module {logsRequestData.ModuleId} = {logs}");
                var response = new LogsResponseData { Logs = logs, ModuleId = logsRequestData.ModuleId };
                var responsString = response.ToJson();
                byte[] logBytes = Encoding.UTF8.GetBytes(responsString);
                return new MethodResponse(logBytes, 200);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error handling Logs callback - {e}");
                return new MethodResponse(500);
            }
        }

        public class LogsRequestData
        {
            [JsonProperty("moduleId")]
            public string ModuleId { get; set; }

            [JsonProperty(PropertyName = "tail")]
            public int? Tail { get; set; }
        }

        class LogsResponseData
        {
            [JsonProperty("moduleId")]
            public string ModuleId { get; set; }

            [JsonProperty("logs")]
            public string Logs { get; set; }
        }
    }
}
