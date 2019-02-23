// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IModuleLogsProvider
    {
        Task<string> GetLogsAsText(string module, Option<int> tail);

        Task<IEnumerable<ModuleLogMessage>> GetLogs(string module, Option<int> tail);
    }
}
