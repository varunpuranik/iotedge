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
    using Newtonsoft.Json;

    public class EnvironmentLogs : ILogsProcessor
    {
        readonly IRuntimeInfoProvider runtimeInfoProvider;

        public EnvironmentLogs(IRuntimeInfoProvider runtimeInfoProvider)
        {
            this.runtimeInfoProvider = runtimeInfoProvider;
        }

        public async Task<Stream> GetLogsAsStream(LogsRequest logsRequest, CancellationToken cancellationToken)
        { 
            string module = logsRequest.Id;
            bool follow = logsRequest.Follow;
            Option<int> tail = logsRequest.LogsFilter.Tail;
            Stream stream = await this.runtimeInfoProvider.GetModuleLogs(module, follow, tail, cancellationToken);
            //var ms = new MemoryStream();
            //await stream.CopyToAsync(ms);
            return stream;
        }

        public Task<IEnumerable<ModuleLogMessage>> GetLogs(LogsRequest logsRequest, CancellationToken cancellationToken) => throw new System.NotImplementedException();
    }

    public interface ILogsProcessor
    {
        Task<Stream> GetLogsAsStream(LogsRequest logsRequest, CancellationToken cancellationToken);

        Task<IEnumerable<ModuleLogMessage>> GetLogs(LogsRequest logsRequest, CancellationToken cancellationToken);
    }

    public class LogsCompressor : ILogsProcessor
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
                    using (var compressedStream = new MemoryStream())
                    {
                        using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Compress))
                        {
                            byte[] bytes = ReadStream(stream);
                            gzipStream.Write(bytes, 0, bytes.Length);
                        }

                        var zippedBytes = compressedStream.ToArray();
                        return new MemoryStream(zippedBytes);
                    }



                case CompressionFormat.Deflate:
                    var deflateStream = new DeflateStream(stream, CompressionMode.Compress);
                    return deflateStream;

                default:
                    return stream;
            }
        }

        static byte[] ReadStream(Stream s)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = s.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        public Task<IEnumerable<ModuleLogMessage>> GetLogs(LogsRequest logsRequest, CancellationToken cancellationToken)
            => this.logsProcessor.GetLogs(logsRequest, cancellationToken);
    }

    //public class LogsRequest
    //{
    //    [JsonConstructor]
    //    public LogsRequest(string id, CompressionFormat compression, LogsFormat format, LogsFilter logsFilter)
    //    {
    //        this.Id = Preconditions.CheckNonWhiteSpace(id, nameof(id));
    //        this.Compression = compression;
    //        this.Format = format;
    //        this.LogsFilter = Preconditions.CheckNotNull(logsFilter, nameof(logsFilter));
    //    }

    //    public string Id { get; }
    //    public LogsFilter LogsFilter { get; }
    //    public CompressionFormat Compression { get; }
    //    public LogsFormat Format { get; }
    //}

    public class LogsRequest
    {
        [JsonConstructor]
        public LogsRequest(string id, bool follow, CompressionFormat compression, LogsFormat format, LogsFilter logsFilter)
        {
            this.Id = Preconditions.CheckNonWhiteSpace(id, nameof(id));
            this.Follow = follow;
            this.Compression = compression;
            this.Format = format;
            this.LogsFilter = Preconditions.CheckNotNull(logsFilter, nameof(logsFilter));
        }

        public string Id { get; }
        public bool Follow { get; }
        public LogsFilter LogsFilter { get; }
        public CompressionFormat Compression { get; }
        public LogsFormat Format { get; }
    }

    public enum LogsFormat
    {
        Text,
        Json
    }

    public enum CompressionFormat
    {
        None,
        GZip,
        Deflate
    }

    public class LogsFilter
    {
        [JsonConstructor]
        public LogsFilter(int? tail, DateTime? since, int? logLevel, string filterRegex)
        {
            this.Tail = tail.HasValue ? Option.Some(tail.Value) : Option.None<int>();
            this.Since = since.HasValue ? Option.Some(since.Value) : Option.None<DateTime>();
            this.LogLevel = logLevel.HasValue ? Option.Some(logLevel.Value) : Option.None<int>();
            this.FilterRegex = Option.Maybe(filterRegex);
        }

        public Option<int> Tail { get; }
        public Option<DateTime> Since { get; }
        public Option<int> LogLevel { get; }
        public Option<string> FilterRegex { get; }
    }
}
