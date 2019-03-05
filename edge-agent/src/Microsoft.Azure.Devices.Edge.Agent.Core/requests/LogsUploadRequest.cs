// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Util;

    public class LogsUploadRequest
    {
        public LogsUploadRequest(string id, LogsContentEncoding compression, LogsContentType format, string sasUrl)
        {
            this.Id = Preconditions.CheckNonWhiteSpace(id, nameof(id));
            this.SasUrl = Preconditions.CheckNonWhiteSpace(sasUrl, nameof(sasUrl));
            this.ContentEncoding = compression;
            this.ContentType = format;
        }

        public string Id { get; }
        public LogsContentEncoding ContentEncoding { get; }
        public LogsContentType ContentType { get; }
        public string SasUrl { get; }
    }
}
