// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Nito.AsyncEx;
    using Xunit;
    using Microsoft.Azure.Devices.Edge.Util;

    [Unit]
    public class AsyncManualResetEventExtensionsTest
    {
        [Fact]
        public async Task TaskCompleteTest()
        {
            // Arrange
            var manualResetEvent = new AsyncManualResetEvent();
            TimeSpan smallTimeout = TimeSpan.FromSeconds(5);

            // Act
            Task waitTask = manualResetEvent.WaitAsync(smallTimeout);

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(5));
            Assert.True(waitTask.IsCompleted);
            Assert.False(manualResetEvent.IsSet);

            // Arrange
            TimeSpan largeTimeout = TimeSpan.FromSeconds(100);
            manualResetEvent.Reset();

            // Act
            waitTask = manualResetEvent.WaitAsync(largeTimeout);
            manualResetEvent.Set();

            // Assert
            Assert.True(waitTask.IsCompleted);
            Assert.True(manualResetEvent.IsSet);
        }
    }
}
