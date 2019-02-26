// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    public class LogsManager
    {
        ILogsProcessor logsProcessor;

        public static ILogsProcessor Create(IRuntimeInfoProvider runtimeInfoProvider, string iotHub, string deviceId)
        {
            var environmentLogsProvider = new EnvironmentLogs(runtimeInfoProvider);
            var filterLogsProvider = new LogsFilterProcessor(environmentLogsProvider, iotHub, deviceId);
            var logsCompressionProvider = new LogsCompressor(filterLogsProvider);
            return logsCompressionProvider;
        }
    }
}
