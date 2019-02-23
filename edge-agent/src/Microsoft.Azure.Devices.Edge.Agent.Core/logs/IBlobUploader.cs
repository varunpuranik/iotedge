// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using System.Threading.Tasks;

    public interface IBlobUploader
    {
        Task UploadToBlob(string containerUri, string blobName, byte[] payload);
    }
}
