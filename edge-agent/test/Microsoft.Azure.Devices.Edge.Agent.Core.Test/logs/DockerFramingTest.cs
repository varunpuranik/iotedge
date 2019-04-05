// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Logs
{
    using System;
    using System.Collections.Generic;
    using Akka.IO;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class DockerFramingTest
    {
        [Theory]
        [MemberData(nameof(GetLengthBytesTestData))]
        public void GetLengthBytesTest(int length, byte[] bytes)
        {
            Assert.Equal(bytes, DockerFraming.GetLengthBytes(length));
        }

        [Theory]
        [InlineData("stderr", 2)]
        [InlineData("stdout", 1)]
        public void GetStreamTest(string stream, byte streamByte)
        {
            Assert.Equal(streamByte, DockerFraming.GetStreamByte(stream));
        }

        [Theory]
        [InlineData("stderr", "<6> 2019-02-08 02:23:23.137 +00:00 [INF] - Starting an important module.\n")]
        [InlineData("stdout", "<4> 2019-03-08 02:23:23.137 +00:00 [WRN] - Warning, something bad happened.\n")]
        public void GetFrameTest(string stream, string text)
        {
            // Arrange
            string iothub = "foo.azure-devices.net";
            string deviceId = "dev1";
            string moduleId = "mod1";
            var logMessageParser = new LogMessageParser(iothub, deviceId);

            // Act
            ArraySegment<byte> frame = DockerFraming.Frame(stream, text);

            // Assert
            ModuleLogMessageData moduleLogMessage = logMessageParser.Parse(ByteString.FromBytes(frame), moduleId);
            Assert.Equal(stream, moduleLogMessage.Stream);
            Assert.Equal(text, moduleLogMessage.FullText);
        }

        public static IEnumerable<object[]> GetLengthBytesTestData()
        {
            yield return new object[] { 42, new byte[] { 0, 0, 32, 0 } };

            yield return new object[] { 1, new byte[] { 0, 0, 0, 1 } };

            yield return new object[] { 8192, new byte[] { 0, 0, 32, 0 } };
        }
    }
}
