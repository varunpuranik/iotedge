// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleLogsProvider : IModuleLogsProvider
    {
        readonly IRuntimeInfoProvider runtimeInfoProvider;

        public ModuleLogsProvider(IRuntimeInfoProvider runtimeInfoProvider)
        {
            this.runtimeInfoProvider = runtimeInfoProvider;
        }

        public Task<Stream> GetLogs(string module) => this.runtimeInfoProvider.GetModuleLogs(module, false, Option.None<int>(), CancellationToken.None);

        public async Task<string> GetLogs(string module, Option<int> tail)
        {
            Stream stream = await this.runtimeInfoProvider.GetModuleLogs(module, false, tail, CancellationToken.None);
            var sb = new StringBuilder();
            using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
            {
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    Console.WriteLine($"Line = {line}");
                    sb.AppendLine(line);
                }
            }

            return sb.ToString();
        }
    }
}
