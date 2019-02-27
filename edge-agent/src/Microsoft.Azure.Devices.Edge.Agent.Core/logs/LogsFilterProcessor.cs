// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    extern alias akka;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using akka::Akka;
    using akka::Akka.Actor;
    using akka::Akka.IO;
    using Akka.Streams;
    using Akka.Streams.Dsl;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class LogsFilterProcessor : ILogsProcessor
    {
        static readonly Flow<ByteString, ByteString, NotUsed> FramingFlow
            = Framing.LengthField(4, int.MaxValue, 4, ByteOrder.BigEndian);

        readonly ILogsProcessor innerLogsProcessor;
        readonly ActorSystem system;
        readonly ActorMaterializer materializer;
        readonly string iotHub;
        readonly string deviceId;

        public LogsFilterProcessor(
            ILogsProcessor innerLogsProcessor,
            string iotHub,
            string deviceId)
        {
            this.iotHub = Preconditions.CheckNonWhiteSpace(iotHub, nameof(iotHub));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.innerLogsProcessor = Preconditions.CheckNotNull(innerLogsProcessor, nameof(innerLogsProcessor));
            this.system = ActorSystem.Create("LogsFilter");
            this.materializer = this.system.Materializer();
        }

        public async Task<Stream> GetLogsAsStream(LogsRequest logsRequest, CancellationToken cancellationToken)
        {
            var logMessageParser = new LogMessageParser(this.iotHub, this.deviceId, logsRequest.Id);
            Stream stream = await this.innerLogsProcessor.GetLogsAsStream(logsRequest, cancellationToken);
            var source = StreamConverters.FromInputStream(() => stream);
            var streamSink = StreamConverters.AsInputStream();
            IRunnableGraph<Stream> graph = source
                .Via(FramingFlow)
                .Select(b => b.Slice(8))
                .Select(logMessageParser.Parse)
                .Select(JsonConvert.SerializeObject)
                .Select(ByteString.FromString)
                .ToMaterialized(streamSink, Keep.Right);

            Stream filteredStream = graph.Run(this.materializer);
            return filteredStream;

        }

        public async Task<IEnumerable<ModuleLogMessage>> GetLogs(LogsRequest logsRequest, CancellationToken cancellationToken)
        {
            var logMessageParser = new LogMessageParser(this.iotHub, this.deviceId, logsRequest.Id);
            Stream stream = await this.innerLogsProcessor.GetLogsAsStream(logsRequest, cancellationToken);
            var source = StreamConverters.FromInputStream(() => stream);
            var seqSink = Sink.Seq<ModuleLogMessage>();
            IRunnableGraph<Task<IImmutableList<ModuleLogMessage>>> graph = source
                .Via(FramingFlow)
                .Select(b => b.Slice(8))
                .Select(logMessageParser.Parse)
                .ToMaterialized(seqSink, Keep.Right);

            IImmutableList<ModuleLogMessage> result = await graph.Run(this.materializer);
            return result;
        }
    }
}
