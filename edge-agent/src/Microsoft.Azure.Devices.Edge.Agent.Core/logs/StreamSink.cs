// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    extern alias akka;
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using akka::Akka.IO;
    using Akka.Streams;
    using Akka.Streams.Stage;

    class StreamSink : GraphStageWithMaterializedValue<SinkShape<ByteString>, Task<Stream>>
    {
        private sealed class Logic : InGraphStageLogic
        {
            private StreamSink streamSink;
            private TaskCompletionSource<Stream> promise;
            private MemoryStream stream;

            public Logic(StreamSink streamSink, TaskCompletionSource<Stream> promise) : base(streamSink.Shape)
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

        public StreamSink()
        {
            //this.OutputStream = new MemoryStream();
            this.Shape = new SinkShape<ByteString>(inlet);
        }

        //public Stream OutputStream { get; }

        Inlet<ByteString> inlet = new Inlet<ByteString>("StreamSink");

        public override ILogicAndMaterializedValue<Task<Stream>> CreateLogicAndMaterializedValue(Attributes inheritedAttributes)
        {
            var promise = new TaskCompletionSource<Stream>();
            return new LogicAndMaterializedValue<Task<Stream>>(new Logic(this, promise), promise.Task);
        }

        public override SinkShape<ByteString> Shape { get; }
        //protected override GraphStageLogic CreateLogic(Attributes inheritedAttributes) => new Logic(this);
    }
}
