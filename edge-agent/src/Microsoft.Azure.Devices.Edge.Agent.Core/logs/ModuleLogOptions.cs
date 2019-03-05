// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    public class ModuleLogOptions
    {
        public ModuleLogOptions(string id, LogsContentEncoding contentEncoding, LogsContentType compression, LogsFormat format, LogsFilter logsFilter)
        {
            this.Id = Preconditions.CheckNonWhiteSpace(id, nameof(id));
            this.Follow = follow;
            this.Compression = compression;
            this.Format = format;
            this.LogsFilter = Preconditions.CheckNotNull(logsFilter, nameof(logsFilter));
        }

        public string Id { get; }
        public LogsContentEncoding ContentEncoding { get; }
        public LogsContentType ContentType { get; }
    }
}
