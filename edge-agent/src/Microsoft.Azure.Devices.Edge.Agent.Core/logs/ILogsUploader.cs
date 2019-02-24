// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using System.IO;
    using System.Threading.Tasks;

    public interface ILogsUploader
    {
        Task Upload(string uri, string module, Stream payloadStream);
    }
}
