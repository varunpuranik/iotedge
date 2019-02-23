// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;

    public interface IBlobUploader
    {
        Task UploadToBlob(string containerUri, string blobName, byte[] payload);
    }

    public class BlobUploader : IBlobUploader
    {
        public async Task UploadToBlob(string containerUri, string blobName, byte[] payload)
        {
            var container = new CloudBlobContainer(new Uri(containerUri));

            CloudBlockBlob blob = container.GetBlockBlobReference(blobName);

            try
            {
                await blob.UploadFromByteArrayAsync(payload, 0, payload.Length);
            }
            catch (StorageException e)
            {
                Console.WriteLine($"Exception - {e}");
            }
        }
    }
}
