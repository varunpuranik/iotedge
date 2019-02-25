// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
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
    using Newtonsoft.Json;

    public class ModuleLogsProvider : IModuleLogsProvider, IDisposable
    {
        static readonly Flow<ByteString, ByteString, NotUsed> FramingFlow
            = Framing.LengthField(4, int.MaxValue, 4, ByteOrder.BigEndian);

        readonly IRuntimeInfoProvider runtimeInfoProvider;
        readonly ActorSystem system;
        readonly ActorMaterializer materializer;

        public ModuleLogsProvider(IRuntimeInfoProvider runtimeInfoProvider)
        {
            this.runtimeInfoProvider = runtimeInfoProvider;
            this.system = ActorSystem.Create("system");
            this.materializer = this.system.Materializer();
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

        public async Task<Stream> GetLogs(string module, Option<int> tail)
        {
            Stream stream = await this.runtimeInfoProvider.GetModuleLogs(module, false, tail, CancellationToken.None);
            IEnumerable<ModuleLogMessage> logMessages = await this.FilterLogs(module, stream);
            return logMessages;
        }

        async Task<string> FilterLogs(Stream stream)
        {
            var source = StreamConverters.FromInputStream(() => stream);
            var seqSink = Sink.Seq<string>();
            IRunnableGraph<Task<IImmutableList<string>>> graph = source
                .Via(FramingFlow)
                .Select(b => b.Slice(8))
                .Select(b => b.ToString(Encoding.UTF8))
                .ToMaterialized(seqSink, Keep.Right);

            IImmutableList<string> result = await graph.Run(this.materializer);
            return string.Join("", result);
        }

        async Task<IEnumerable<ModuleLogMessage>> FilterLogs(string module, Stream stream)
        {
            var source = StreamConverters.FromInputStream(() => stream);
            var seqSink = Sink.Seq<ModuleLogMessage>();
            IRunnableGraph<Task<IImmutableList<ModuleLogMessage>>> graph = source
                .Via(FramingFlow)
                .Select(b => b.Slice(8))
                .Select(b => ToLogMessage(module, b))
                .ToMaterialized(seqSink, Keep.Right);

            IImmutableList<ModuleLogMessage> result = await graph.Run(this.materializer);
            return result;
        }

        Stream FilterLogsToStream(string module, Stream stream)
        {
            var source = StreamConverters.FromInputStream(() => stream);
            var streamSink = StreamConverters.AsInputStream();
            IRunnableGraph<Stream> graph = source
                .Via(FramingFlow)
                .Select(b => b.Slice(8))
                .Select(b => ToLogMessage(module, b))
                .Select(JsonConvert.SerializeObject)
                .Select(ByteString.FromString)
                .ToMaterialized(streamSink, Keep.Right);

           Stream filteredStream = graph.Run(this.materializer);
            return filteredStream;
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

        public void Dispose()
        {
            this.system?.Dispose();
            this.materializer?.Dispose();
        }
    }
}
