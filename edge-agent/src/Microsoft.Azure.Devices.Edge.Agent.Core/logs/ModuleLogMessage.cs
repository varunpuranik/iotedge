// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleLogMessage
    {
        public ModuleLogMessage(string iotHub, string deviceId, string moduleId, string stream, int logLevel, DateTime? timeStamp, string text)
        {
            // Don't check parameters by design. They could be null depending on what we can parse from the log line.
            this.IoTHub = Preconditions.CheckNonWhiteSpace(iotHub, nameof(iotHub));
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.Stream = stream;
            this.LogLevel = logLevel;
            this.TimeStamp = timeStamp;
            this.Text = Preconditions.CheckNotNull(text, nameof(text));
        }

        public string IoTHub { get; }
        public string DeviceId { get; }
        public string ModuleId { get; }
        public string Stream { get; }
        public int LogLevel { get; }
        public string Text { get; }
        public DateTime? TimeStamp { get; }        
    }
}
