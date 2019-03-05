// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    extern alias akka;
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using akka::Akka;
    using akka::Akka.Actor;
    using akka::Akka.IO;
    using Akka.Streams;
    using Akka.Streams.Dsl;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;

    public class LogsProvider : ILogsProvider
    {
        readonly IRuntimeInfoProvider runtimeInfoProvider;
        readonly ILogsProcessor logsProcessor;
        readonly ILogMessageParser logsMessageParser;


        public LogsProvider(IRuntimeInfoProvider runtimeInfoProvider, ILogsProcessor logsProcessor)
        {
            this.runtimeInfoProvider = Preconditions.CheckNotNull(runtimeInfoProvider, nameof(runtimeInfoProvider));
            this.logsProcessor = Preconditions.CheckNotNull(logsProcessor, nameof(logsProcessor));
        }

        public async Task<byte[]> GetLogs(ModuleLogOptions logOptions, CancellationToken cancellationToken)
        {
            Stream logsStream = await this.runtimeInfoProvider.GetModuleLogs(logOptions.Id, false, Option.None<int>(), cancellationToken);
            byte[] logBytes = await this.GetProcessedLogs(logsStream, logOptions);
            return logBytes;
        }

        async Task<byte[]> GetProcessedLogs(Stream logsStream, ModuleLogOptions logOptions)
        {
            byte[] logBytes = await this.ProcessByContentType(logsStream, logOptions.ContentType);
            logBytes = ProcessByContentEncoding(logBytes, logOptions.ContentEncoding);
            return logBytes;
        }

        async Task<byte[]> ProcessByContentType(Stream logsStream, LogsContentType logsContentType)
        {
            switch (logsContentType)
            {
                case LogsContentType.Json:
                    IEnumerable<ModuleLogMessage> logMessages = await this.logsProcessor.GetMessages(logsStream, this.logsMessageParser, logOptions.Id);
                    return logMessages.ToBytes();

                default:
                    IEnumerable<string> logTexts = await this.logsProcessor.GetText(logsStream);
                    return logTexts.ToBytes();
            }
        }

        static byte[] ProcessByContentEncoding(byte[] bytes, LogsContentEncoding contentEncoding) =>
            contentEncoding == LogsContentEncoding.Gzip
                ? Compression.CompressToGzip(bytes)
                : bytes;
    }

    public class LogsProcessor : ILogsProcessor, IDisposable
    {
        static readonly Flow<ByteString, ByteString, NotUsed> FramingFlow
            = Framing.LengthField(4, int.MaxValue, 4, ByteOrder.BigEndian);

        readonly ActorSystem system;
        readonly ActorMaterializer materializer;

        public LogsProcessor()
        {
            this.system = ActorSystem.Create("LogsProcessor");
            this.materializer = this.system.Materializer();
        }

        public async Task<IEnumerable<ModuleLogMessage>> GetMessages(Stream stream, ILogMessageParser logMessageParser, string moduleId)
        {
            Preconditions.CheckNotNull(stream, nameof(stream));
            Preconditions.CheckNotNull(logMessageParser, nameof(logMessageParser));
            Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));

            var source = StreamConverters.FromInputStream(() => stream);
            var seqSink = Sink.Seq<ModuleLogMessage>();
            IRunnableGraph<Task<IImmutableList<ModuleLogMessage>>> graph = source
                .Via(FramingFlow)
                .Select(b => logMessageParser.Parse(b, moduleId))
                .ToMaterialized(seqSink, Keep.Right);

            IImmutableList<ModuleLogMessage> result = await graph.Run(this.materializer);
            return result;
        }

        public async Task<IEnumerable<string>> GetText(Stream stream)
        {
            Preconditions.CheckNotNull(stream, nameof(stream));
            var source = StreamConverters.FromInputStream(() => stream);
            var seqSink = Sink.Seq<string>();
            IRunnableGraph<Task<IImmutableList<string>>> graph = source
                .Via(FramingFlow)
                .Select(b => b.Slice(8))
                .Select(b => b.ToString(Encoding.UTF8))
                .ToMaterialized(seqSink, Keep.Right);

            IImmutableList<string> result = await graph.Run(this.materializer);
            return result;
        }

        public void Dispose()
        {
            this.system?.Dispose();
            this.materializer?.Dispose();
        }
    }

    public interface ILogsProcessor
    {
        Task<IEnumerable<ModuleLogMessage>> GetMessages(Stream stream, ILogMessageParser logMessageParser, string moduleId);

        Task<IEnumerable<string>> GetText(Stream stream);
    }
}
