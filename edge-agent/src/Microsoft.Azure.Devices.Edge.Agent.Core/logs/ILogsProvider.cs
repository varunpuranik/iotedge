// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using System.Threading.Tasks;

    public interface ILogsProvider
    {
        Task<byte[]> GetLogs(ModuleLogOptions logOptions);
    }
}
