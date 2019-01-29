// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Nito.AsyncEx;

    public static class AsyncManualResetEventExtensions
    {
        public static async Task WaitAsync(this AsyncManualResetEvent manualResetEvent, TimeSpan timeout)
        {
            using (var cts = new CancellationTokenSource())
            {
                Task timerTask = Task.Delay(timeout, cts.Token);
                Task completedTask = await Task.WhenAny(manualResetEvent.WaitAsync(), timerTask);
                if (completedTask != timerTask)
                {
                    cts.Cancel();
                }
            }
        }
    }
}
