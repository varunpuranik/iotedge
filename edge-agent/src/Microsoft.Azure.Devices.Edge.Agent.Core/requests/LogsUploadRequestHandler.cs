// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Util;

    public class LogsUploadRequestHandler : RequestHandlerBase<LogsUploadRequest, object>
    {
        readonly ILogsUploader logsUploader;
        readonly ILogsProcessor logsProcessor;

        public LogsUploadRequestHandler(ILogsUploader logsUploader, ILogsProcessor logsProcessor)
            : base("UploadLogs")
        {
            this.logsProcessor = Preconditions.CheckNotNull(logsProcessor, nameof(logsProcessor));
            this.logsUploader = Preconditions.CheckNotNull(logsUploader, nameof(logsUploader));
        }

        protected override async Task<object> HandleRequestInternal(LogsUploadRequest payload)
        {
            //Stream logsStream2 = await this.logsProcessor.GetLogsAsStream(payload, CancellationToken.None);
            //byte[] bytes = new byte[1024];
            //int count = 0;
            //while ((count = await logsStream2.ReadAsync(bytes, 0, 1024)) > 0)
            //{
            //    string str = System.Text.Encoding.UTF8.GetString(bytes, 0, count);
            //    System.Console.WriteLine(str);
            //}

            Stream logsStream = await this.logsProcessor.GetLogsAsStream(payload, CancellationToken.None);
            await this.logsUploader.Upload(payload.SasUrl, payload.Id, logsStream);
            return null;
        }

        protected override async Task<Option<object>> HandleRequestInternal(Option<LogsUploadRequest> payload)
        {

        }
    }
}
