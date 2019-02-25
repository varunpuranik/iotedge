// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class EnvironmentLogs : ILogsProcessor
    {
        readonly IRuntimeInfoProvider runtimeInfoProvider;

        public EnvironmentLogs(IRuntimeInfoProvider runtimeInfoProvider)
        {
            this.runtimeInfoProvider = runtimeInfoProvider;
        }

        Task<Stream> ILogsProcessor.GetLogs(LogsRequest logsRequest, CancellationToken cancellationToken)
        { 
            string module = logsRequest.Id;
            bool follow = logsRequest.Follow;
            Option<int> tail = logsRequest.LogsFilter.Tail;
            return this.runtimeInfoProvider.GetModuleLogs(module, follow, tail, cancellationToken);
        }

        async Task<IEnumerable<ModuleLogMessage>> ILogsProcessor.GetLogs(LogsRequest logsRequest) => throw new System.NotImplementedException();
    }

    public interface ILogsProcessor
    {
        Task<Stream> GetLogs(LogsRequest logsRequest);

        Task<IEnumerable<ModuleLogMessage>> GetLogs(LogsRequest logsRequest);
    }

    class LogsRequest
    {
        public string Id { get; }
        public bool Follow { get; }
        public LogsFilter LogsFilter { get; }
        public bool Compress { get; }
        public string Format { get; }
    }

    class LogsFilter
    {
        public Option<int> Tail { get; }
        public Option<DateTime> Since { get; }
        public Option<int> LogLevel { get; }
        public Option<string> FilterRegex { get; }
    }
}
