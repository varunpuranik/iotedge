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
    using Akka.Streams.IO;
    using Akka.Streams.Stage;
    using Microsoft.Azure.Devices.Edge.Storage;
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

        public async Task<Stream> GetLogsAsStream2(LogsRequest logsRequest, CancellationToken cancellationToken)
        {
            var logMessageParser = new LogMessageParser(this.iotHub, this.deviceId, logsRequest.Id);
            Stream stream = await this.innerLogsProcessor.GetLogsAsStream(logsRequest, cancellationToken);
            var source = StreamConverters.FromInputStream(() => stream);
            //var streamSink = StreamConverters.AsInputStream();
            var streamSink = new StreamSinkWithValue();
            IRunnableGraph<Task<Stream>> graph = source
                .Via(FramingFlow)
                //.Select(b => b.Slice(8))
                .Select(logMessageParser.Parse)
                .Select(JsonConvert.SerializeObject)
                .Select(ByteString.FromString)
                .ToMaterialized(streamSink, Keep.Right);

            Stream outputStream = await graph.Run(this.materializer);
            return outputStream;
        }

        public async Task<Stream> GetLogsAsStream(LogsRequest logsRequest, CancellationToken cancellationToken)
        {
            var logMessageParser = new LogMessageParser(this.iotHub, this.deviceId, logsRequest.Id);
            Stream stream = await this.innerLogsProcessor.GetLogsAsStream(logsRequest, cancellationToken);
            var source = StreamConverters.FromInputStream(() => stream);
            var seqSink = Sink.Seq<ModuleLogMessage>();
            IRunnableGraph<Task<IImmutableList<ModuleLogMessage>>> graph = source
                .Via(FramingFlow)
                //.Select(b => b.Slice(8))
                .Select(logMessageParser.Parse)
                .ToMaterialized(seqSink, Keep.Right);

            IImmutableList<ModuleLogMessage> result = await graph.Run(this.materializer);
            string json = result.ToJson();
            byte[] outputBytes = json.ToBytes();
            return new MemoryStream(outputBytes);
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

        class StreamSink : GraphStage<SinkShape<ByteString>>
        {
            private sealed class Logic : GraphStageLogic
            {
                private StreamSink streamSink;

                public Logic(StreamSink streamSink) : base(streamSink.Shape)
                {
                    this.streamSink = streamSink;
                    SetHandler(streamSink.In, onPush: () =>
                    {
                        ByteString byteString = Grab(streamSink.In);
                        byte[] bytes = byteString.ToArray();
                        string str = Encoding.UTF8.GetString(bytes);
                        Console.WriteLine(str);
                        streamSink.OutputStream.Write(bytes, 0, bytes.Length);
                        streamSink.OutputStream.Flush();
                        Pull(streamSink.In);
                    });
                }

                public override void PreStart()
                {
                    //base.PreStart();
                    Pull(streamSink.In);
                }
            }

            public StreamSink()
            {
                this.OutputStream = new MemoryStream();
            }

            public Stream OutputStream { get; }

            public Inlet<ByteString> In { get; } = new Inlet<ByteString>("OutputStream");

            public override SinkShape<ByteString> Shape => new SinkShape<ByteString>(In);
            protected override GraphStageLogic CreateLogic(Attributes inheritedAttributes) => new Logic(this);
        }

        class StreamSinkWithValue : GraphStageWithMaterializedValue<SinkShape<ByteString>, Task<Stream>>
        {
            private sealed class Logic : InGraphStageLogic
            {
                private StreamSinkWithValue streamSink;
                private TaskCompletionSource<Stream> promise;
                private MemoryStream stream;

                public Logic(StreamSinkWithValue streamSink, TaskCompletionSource<Stream> promise) : base(streamSink.Shape)
                {
                    this.streamSink = streamSink;
                    this.promise = promise;
                    this.SetHandler(streamSink.inlet, this);
                    this.stream = new MemoryStream();
                    //SetHandler(streamSink.In, onPush: () =>
                    //{
                    //    ByteString byteString = Grab(streamSink.In);
                    //    byte[] bytes = byteString.ToArray();
                    //    string str = Encoding.UTF8.GetString(bytes);
                    //    Console.WriteLine(str);
                    //    streamSink.OutputStream.Write(bytes);
                    //    streamSink.OutputStream.Flush();
                    //    Pull(streamSink.In);
                    //});
                }

                public override void PreStart()
                {
                    //base.PreStart();
                    Pull(streamSink.inlet);
                }

                public override void OnPush()
                {
                    ByteString byteString = Grab(streamSink.inlet);
                    byte[] bytes = byteString.ToArray();
                    string str = Encoding.UTF8.GetString(bytes);
                    Console.WriteLine(str);
                    this.stream.Write(bytes, 0, bytes.Length);
                    Pull(streamSink.inlet);
                }

                public override void OnUpstreamFinish()
                {
                    this.stream.Flush();
                    this.stream.Seek(0, SeekOrigin.Begin);
                    promise.TrySetResult(this.stream);
                    CompleteStage();
                }
            }

            public StreamSinkWithValue()
            {
                //this.OutputStream = new MemoryStream();
                this.Shape = new SinkShape<ByteString>(inlet);
            }

            //public Stream OutputStream { get; }

            Inlet<ByteString> inlet = new Inlet<ByteString>("OutputStream");

            public override ILogicAndMaterializedValue<Task<Stream>> CreateLogicAndMaterializedValue(Attributes inheritedAttributes)
            {
                var promise = new TaskCompletionSource<Stream>();
                return new LogicAndMaterializedValue<Task<Stream>>(new Logic(this, promise), promise.Task);
            }

            public override SinkShape<ByteString> Shape { get; }
            //protected override GraphStageLogic CreateLogic(Attributes inheritedAttributes) => new Logic(this);
        }
    }
}
