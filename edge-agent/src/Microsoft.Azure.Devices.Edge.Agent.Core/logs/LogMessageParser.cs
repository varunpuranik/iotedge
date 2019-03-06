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
        const int DefaultLogLevel = 6;
        const string LogRegexPattern = @"^(<(?<logLevel>\d)>)?\s*((?<timestamp>\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}.\d{3}\s[+-]\d{2}:\d{2})\s)?";        

        readonly string iotHubName;
        readonly string deviceId;

        public LogMessageParser(string iotHubName, string deviceId)
        {
            this.iotHubName = Preconditions.CheckNonWhiteSpace(iotHubName, nameof(iotHubName));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
        }

        public ModuleLogMessage Parse(ByteString byteString, string moduleId) =>
            GetLogMessage(byteString, this.iotHubName, this.deviceId, moduleId);

        internal static ModuleLogMessage GetLogMessage(ByteString arg, string iotHubName, string deviceId, string moduleId)
        {
            string stream = GetStream(arg[0]);
            ByteString payload = arg.Slice(8);
            string payloadString = payload.ToString(Encoding.UTF8);
            (int logLevel, Option<DateTime> timeStamp) = ParseLogLine(payloadString);
            var moduleLogMessage = new ModuleLogMessage(iotHubName, deviceId, moduleId, stream, logLevel, timeStamp, payloadString);
            return moduleLogMessage;
        }

        internal static string GetStream(byte streamByte) => streamByte == 1 ? "stdout" : "stderr";

        internal static (int logLevel, Option<DateTime> timeStamp) ParseLogLine(string value)
        {            
            var regex = new Regex(LogRegexPattern);
            var match = regex.Match(value);
            int logLevel = DefaultLogLevel;
            Option<DateTime> timeStamp = Option.None<DateTime>();
            if (match.Success)
            {                
                var tsg = match.Groups["timestamp"];
                if (tsg?.Length > 0)
                {
                    string timestamp = match.Groups["timestamp"].Value;
                    if (DateTime.TryParse(timestamp, out DateTime dt))
                    {
                        timeStamp = Option.Some(dt);
                    }
                }

                var llg = match.Groups["logLevel"];
                if (llg?.Length > 0)
                {
                    string ll = llg.Value;
                    int.TryParse(ll, out logLevel);
                }
            }

            return (logLevel, timeStamp);
        }
    }
}
