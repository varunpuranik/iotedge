// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class LogsUploadRequestHandler : RequestHandlerBase<LogsUploadRequest, object>
    {
        readonly ILogsUploader logsUploader;
        readonly ILogsProcessor logsProcessor;

        public LogsUploadRequestHandler(ILogsUploader logsUploader, ILogsProcessor logsProcessor)
        : base("UploadLogs")
        {
            this.logsProcessor = logsProcessor;
            this.logsUploader = logsUploader;
        }

        protected override async Task<object> HandleRequestInternal(LogsUploadRequest payload)
        {
            Stream logsStream = await this.logsProcessor.GetLogsAsStream(payload, CancellationToken.None);
            await this.logsUploader.Upload(payload.SasUrl, payload.Id, logsStream);
            return null;
        }
    }

    public class LogsUploadRequest : LogsRequest
    {
        [JsonConstructor]
        public LogsUploadRequest(string id, CompressionFormat compression, LogsFormat format, LogsFilter logsFilter, string sasUrl)
            : base(id, false, compression, format, logsFilter)
        {
            this.SasUrl = Preconditions.CheckNonWhiteSpace(sasUrl, nameof(sasUrl));
        }

        public string SasUrl { get; }
    }
}
