// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    extern alias akka;
    using System;
    using System.Collections.Immutable;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Akka.Streams;
    using Akka.Streams.Dsl;
    using Microsoft.Azure.Devices.Edge.Util;
    using akka::Akka.Actor;
    using akka::Akka.IO;

    public class ModuleLogsProvider : IModuleLogsProvider
    {
        readonly IRuntimeInfoProvider runtimeInfoProvider;

        public ModuleLogsProvider(IRuntimeInfoProvider runtimeInfoProvider)
        {
            this.runtimeInfoProvider = runtimeInfoProvider;
        }

        public Task<Stream> GetLogs(string module) => this.runtimeInfoProvider.GetModuleLogs(module, false, Option.None<int>(), CancellationToken.None);

        public async Task<string> GetLogs(string module, Option<int> tail)
        {
            Stream stream = await this.runtimeInfoProvider.GetModuleLogs(module, false, tail, CancellationToken.None);
            string logsString = await this.FilterLogs(stream);
            return logsString;
            //var sb = new StringBuilder();
            //using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
            //{
            //    string line;
            //    while ((line = await reader.ReadLineAsync()) != null)
            //    {
            //        Console.WriteLine($"Line = {line}");
            //        sb.AppendLine(line);
            //    }
            //}

            //return sb.ToString();
        }

        //Stream FilterLogs(Stream stream)
        //{
        //    using (var system = ActorSystem.Create("system"))
        //    using (var materializer = system.Materializer())
        //    {
        //        var flow = Framing.LengthField(4, Int32.MaxValue, 4, ByteOrder.BigEndian);
        //        var source = StreamConverters.FromInputStream(() => stream);
        //        var streamSink = StreamConverters.AsInputStream();
        //        var graph = source
        //            .Via(flow)
        //            .Select(b => b.Slice(8))
        //            .ToMaterialized(streamSink, Keep.Right);

        //        Stream result = graph.Run(materializer);
        //        return result;
        //    }
        //}

        async Task<string> FilterLogs(Stream stream)
        {
            using (var system = ActorSystem.Create("system"))
            using (var materializer = system.Materializer())
            {
                var flow = Framing.LengthField(4, Int32.MaxValue, 4, ByteOrder.BigEndian);
                var source = StreamConverters.FromInputStream(() => stream);
                var seqSink = Sink.Seq<string>();
                var graph = source
                    .Via(flow)
                    .Select(b => b.Slice(8))
                    .Select(b => b.ToString(Encoding.UTF8))
                    .ToMaterialized(seqSink, Keep.Right);

                IImmutableList<string> result = await graph.Run(materializer);
                return string.Join("", result);
            }
        }
    }
}
