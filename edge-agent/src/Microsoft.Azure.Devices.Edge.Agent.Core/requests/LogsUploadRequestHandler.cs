// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Util;

    public class LogsUploadRequestHandler : RequestHandlerBase<LogsUploadRequest, object>
    {
        readonly ILogsUploader logsUploader;
        readonly ILogsProvider logsProvider;

        public LogsUploadRequestHandler(ILogsUploader logsUploader, ILogsProvider logsProvider)
        {
            this.logsProvider = Preconditions.CheckNotNull(logsProvider, nameof(logsProvider));
            this.logsUploader = Preconditions.CheckNotNull(logsUploader, nameof(logsUploader));
        }

        protected override async Task<Option<object>> HandleRequestInternal(Option<LogsUploadRequest> payload)
        {
            LogsUploadRequest logsUploadRequest = payload.Expect(() => new ArgumentException("Valid payload required to process upload logs request"));
            var moduleLogOptions = new ModuleLogOptions(logsUploadRequest.Id, logsUploadRequest.ContentEncoding, logsUploadRequest.ContentType);
            byte[] bytes = await this.logsProvider.GetLogs(moduleLogOptions, CancellationToken.None);
            await this.logsUploader.Upload(logsUploadRequest.SasUrl, logsUploadRequest.Id, bytes, logsUploadRequest.ContentEncoding, logsUploadRequest.ContentType);
            return Option.None<object>();
        }
    }
}
