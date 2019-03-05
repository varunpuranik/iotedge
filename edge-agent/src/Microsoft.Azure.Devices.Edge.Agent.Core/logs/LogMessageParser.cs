// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    extern alias akka;
    using System;
    using System.Text;
    using System.Text.RegularExpressions;
    using akka::Akka.IO;
    using Microsoft.Azure.Devices.Edge.Util;

    public class LogMessageParser : ILogMessageParser
    {
        readonly string iotHubName;
        readonly string deviceId;

        public LogMessageParser(string iotHubName, string deviceId)
        {
            this.iotHubName = Preconditions.CheckNonWhiteSpace(iotHubName, nameof(iotHubName));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
        }

        public ModuleLogMessage Parse(ByteString byteString, string moduleId) =>
            GetLogMessage(byteString, this.iotHubName, this.deviceId, moduleId);

        static ModuleLogMessage GetLogMessage(ByteString arg, string iotHubName, string deviceId, string moduleId)
        {
            string stream = GetStream(arg[0]);
            ByteString payload = arg.Slice(8);
            string payloadString = payload.ToString(Encoding.UTF8);
            (int logLevel, DateTime? timeStamp) = ParseLogLine(payloadString);
            var moduleLogMessage = new ModuleLogMessage(iotHubName, deviceId, moduleId, stream, logLevel, timeStamp, payloadString);
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
    }
}
