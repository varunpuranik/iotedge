// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using System;

    public class ModuleLogMessage
    {
        public string Stream { get; set; }
        public int LogLevel { get; set; }
        public string LogMessage { get; set; }
        public DateTime? TimeStamp { get; set; }
        public string Source { get; set; }
    }
}
