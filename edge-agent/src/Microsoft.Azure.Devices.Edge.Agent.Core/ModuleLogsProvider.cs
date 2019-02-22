// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    extern alias akka;
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using akka::Akka;
    using Akka.Streams;
    using Akka.Streams.Dsl;
    using Microsoft.Azure.Devices.Edge.Util;
    using akka::Akka.Actor;
    using akka::Akka.IO;

    public class ModuleLogsProvider : IModuleLogsProvider
    {
        static readonly Flow<ByteString, ByteString, NotUsed> FramingFlow
            = Framing.LengthField(4, int.MaxValue, 4, ByteOrder.BigEndian);

        readonly IRuntimeInfoProvider runtimeInfoProvider;

        public ModuleLogsProvider(IRuntimeInfoProvider runtimeInfoProvider)
        {
            this.runtimeInfoProvider = runtimeInfoProvider;
        }

        public async Task<string> GetLogsAsText(string module, Option<int> tail)
        {
            Stream stream = await this.runtimeInfoProvider.GetModuleLogs(module, false, tail, CancellationToken.None);
            string logsString = await this.FilterLogs(stream);
            return logsString;
        }

        public async Task<IEnumerable<ModuleLogMessage>> GetLogs(string module, Option<int> tail)
        {
            Stream stream = await this.runtimeInfoProvider.GetModuleLogs(module, false, tail, CancellationToken.None);
            IEnumerable<ModuleLogMessage> logMessages = await this.FilterLogs(module, stream);
            return logMessages;
        }

        async Task<string> FilterLogs(Stream stream)
        {
            using (ActorSystem system = ActorSystem.Create("system"))
            using (ActorMaterializer materializer = system.Materializer())
            {
                var source = StreamConverters.FromInputStream(() => stream);
                var seqSink = Sink.Seq<string>();
                IRunnableGraph<Task<IImmutableList<string>>> graph = source
                    .Via(FramingFlow)
                    .Select(b => b.Slice(8))
                    .Select(b => b.ToString(Encoding.UTF8))
                    .ToMaterialized(seqSink, Keep.Right);                    

                IImmutableList<string> result = await graph.Run(materializer);
                return string.Join("", result);
            }
        }

        async Task<IEnumerable<ModuleLogMessage>> FilterLogs(string module, Stream stream)
        {
            using (ActorSystem system = ActorSystem.Create("system"))
            using (ActorMaterializer materializer = system.Materializer())
            {
                var source = StreamConverters.FromInputStream(() => stream);
                var seqSink = Sink.Seq<ModuleLogMessage>();
                IRunnableGraph<Task<IImmutableList<ModuleLogMessage>>> graph = source
                    .Via(FramingFlow)
                    .Select(b => b.Slice(8))
                    .Select(b => ToLogMessage(module, b))
                    .ToMaterialized(seqSink, Keep.Right);

                IImmutableList<ModuleLogMessage> result = await graph.Run(materializer);
                return result;
            }
        }

        static ModuleLogMessage ToLogMessage(string module, ByteString arg)
        {
            string stream = GetStream(arg[0]);
            ByteString payload = arg.Slice(8);
            string payloadString = payload.ToString(Encoding.UTF8);
            (int logLevel, DateTime? timeStamp) = ParseLogLine(payloadString);
            var moduleLogMessage = new ModuleLogMessage
            {
                LogLevel = logLevel,
                LogMessage = payloadString,
                Source = module,
                Stream = stream,
                TimeStamp = timeStamp
            };
            return moduleLogMessage;
        }

        static string GetStream(byte streamByte) => streamByte == 1 ? "stdout" : "stderr";

        static (int logLevel, DateTime? timeStamp) ParseLogLine(string value)
        {
            value = @"   2019-02-19 22:58:41.847 +00:00 ";
            string regexPattern = @"^\s*(?<timestamp>\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}.\d{3}\s[+-]\d{2}:\d{2})\s";
            var regex = new Regex(regexPattern);
            var match = regex.Match(value);
            if (match.Success)
            {
                Console.WriteLine($"Value = {match.Value}");
                string timestamp = match.Groups["timestamp"].Value;
                var dt = DateTime.Parse(timestamp);
                Console.WriteLine(dt);
            }

            return (6, null);
        }

        public class ModuleLogMessage
        {
            public string Stream { get; set; }
            public int LogLevel { get; set; }
            public string LogMessage { get; set; }

            public DateTime? TimeStamp { get; set; }
            public string Source { get; set; }
        }
    }
}
