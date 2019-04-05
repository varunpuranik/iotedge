// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public static class DockerFraming
    {
        static readonly byte[] Padding = { 0, 0, 0 };

        public static ArraySegment<byte> Frame(string stream, string text)
        {
            byte streamByte = GetStreamByte(stream);
            var outputBytes = new List<byte>();
            byte[] textBytes = Encoding.UTF8.GetBytes(text);
            byte[] lenBytes = GetLengthBytes(textBytes.Length);
            outputBytes.Add(streamByte);
            outputBytes.AddRange(Padding);
            outputBytes.AddRange(lenBytes);
            outputBytes.AddRange(textBytes);
            byte[] frameBytes = outputBytes.ToArray();
            return new ArraySegment<byte>(frameBytes);
        }

        internal static byte[] GetLengthBytes(int len)
        {
            byte[] intBytes = BitConverter.GetBytes(len);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(intBytes);
            }

            byte[] result = intBytes;
            return result;
        }

        internal static byte GetStreamByte(string stream) => (byte)(stream == "stderr" ? 2 : 1);
    }
}
