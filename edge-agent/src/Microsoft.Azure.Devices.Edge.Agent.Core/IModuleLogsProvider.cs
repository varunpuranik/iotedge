// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IModuleLogsProvider
    {
        Task<string> GetLogsAsText(string module, Option<int> tail);
    }
}
