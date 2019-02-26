// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    class EnvironmentLogs : ILogsProcessor
    {
        readonly IRuntimeInfoProvider runtimeInfoProvider;

        public EnvironmentLogs(IRuntimeInfoProvider runtimeInfoProvider)
        {
            this.runtimeInfoProvider = runtimeInfoProvider;
        }

        public Task<Stream> GetLogsAsStream(LogsRequest logsRequest, CancellationToken cancellationToken)
        { 
            string module = logsRequest.Id;
            bool follow = logsRequest.Follow;
            Option<int> tail = logsRequest.LogsFilter.Tail;
            return this.runtimeInfoProvider.GetModuleLogs(module, follow, tail, cancellationToken);
        }

        public Task<IEnumerable<ModuleLogMessage>> GetLogs(LogsRequest logsRequest, CancellationToken cancellationToken) => throw new System.NotImplementedException();
    }

    interface ILogsProcessor
    {
        Task<Stream> GetLogsAsStream(LogsRequest logsRequest, CancellationToken cancellationToken);

        Task<IEnumerable<ModuleLogMessage>> GetLogs(LogsRequest logsRequest, CancellationToken cancellationToken);
    }

    class LogsCompressor : ILogsProcessor
    {
        readonly ILogsProcessor logsProcessor;

        public LogsCompressor(ILogsProcessor logsProcessor)
        {
            this.logsProcessor = Preconditions.CheckNotNull(logsProcessor, nameof(logsProcessor));
        }

        public async Task<Stream> GetLogsAsStream(LogsRequest logsRequest, CancellationToken cancellationToken)
        {
            Stream stream = await this.logsProcessor.GetLogsAsStream(logsRequest, cancellationToken);
            switch (logsRequest.Compression)
            {
                case CompressionFormat.GZip:
                    var compressionStream = new GZipStream(stream, CompressionMode.Compress);
                    return compressionStream;

                case CompressionFormat.Deflate:
                    var deflateStream = new DeflateStream(stream, CompressionMode.Compress);
                    return deflateStream;

                default:
                    return stream;
            }
        }

        public Task<IEnumerable<ModuleLogMessage>> GetLogs(LogsRequest logsRequest, CancellationToken cancellationToken)
            => this.logsProcessor.GetLogs(logsRequest, cancellationToken);
    }

    class LogsRequest
    {
        public string Id { get; }
        public bool Follow { get; }
        public LogsFilter LogsFilter { get; }
        public CompressionFormat Compression { get; }
        public string Format { get; }
    }

    enum CompressionFormat
    {
        None,
        GZip,
        Deflate
    }

    class LogsFilter
    {
        public Option<int> Tail { get; }
        public Option<DateTime> Since { get; }
        public Option<int> LogLevel { get; }
        public Option<string> FilterRegex { get; }
    }
}
