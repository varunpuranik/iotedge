// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Storage.Blob;

    public class AzureBlobUploader : ILogsUploader
    {
        const int RetryCount = 2;

        static readonly ITransientErrorDetectionStrategy TransientErrorDetectionStrategy = new ErrorDetectionStrategy();

        static readonly RetryStrategy TransientRetryStrategy =
            new ExponentialBackoff(RetryCount, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(4));

        readonly string iotHubName;
        readonly string deviceId;

        public AzureBlobUploader(string iotHubName, string deviceId)
        {
            this.iotHubName = Preconditions.CheckNonWhiteSpace(iotHubName, nameof(iotHubName));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
        }

        public async Task Upload(string uri, string module, Stream payloadStream)
        {
            Preconditions.CheckNonWhiteSpace(uri, nameof(uri));
            Preconditions.CheckNonWhiteSpace(module, nameof(module));
            Preconditions.CheckNotNull(payloadStream, nameof(payloadStream));

            try
            {
                var containerUri = new Uri(uri);
                string blobName = this.GetBlobName(module);
                var container = new CloudBlobContainer(containerUri);
                Events.Uploading(blobName, container.Name);
                await ExecuteWithRetry(
                    () =>
                    {
                        CloudBlockBlob blob = container.GetBlockBlobReference(blobName);
                        return blob.UploadFromStreamAsync(payloadStream);
                    },
                    r => Events.UploadErrorRetrying(blobName, container.Name, r));
                Events.UploadSuccess(blobName, container.Name);
            }
            catch (Exception e)
            {
                Events.UploadError(e, module);
                throw;
            }
        }

        static Task ExecuteWithRetry(Func<Task> func, Action<RetryingEventArgs> onRetry)
        {
            var transientRetryPolicy = new RetryPolicy(TransientErrorDetectionStrategy, TransientRetryStrategy);
            transientRetryPolicy.Retrying += (_, args) => onRetry(args);
            return transientRetryPolicy.ExecuteAsync(func);
        }

        string GetBlobName(string module) => $"{this.iotHubName}/{this.deviceId}/{module}";

        class ErrorDetectionStrategy : ITransientErrorDetectionStrategy
        {
            public bool IsTransient(Exception ex) => !ex.IsFatal();
        }

        static class Events
        {
            const int IdStart = AgentEventIds.AzureBlobUploader;
            static readonly ILogger Log = Logger.Factory.CreateLogger<AzureBlobUploader>();

            enum EventIds
            {
                Uploading = IdStart + 1,
                UploadSuccess,
                ErrorHandlingRequest,
                UploadErrorRetrying,
                UploadError
            }

            public static void Uploading(string blobName, string container)
            {
                Log.LogInformation((int)EventIds.Uploading, $"Uploading blob {blobName} to container {container}");
            }

            public static void UploadSuccess(string blobName, string container)
            {
                Log.LogDebug((int)EventIds.UploadSuccess, $"Successfully uploaded blob {blobName} to container {container}");
            }

            public static void UploadErrorRetrying(string blobName, string container, RetryingEventArgs retryingEventArgs)
            {
                Log.LogDebug((int)EventIds.UploadErrorRetrying, retryingEventArgs.LastException, $"Error uploading {blobName} to container {container}. Retry count - {retryingEventArgs.CurrentRetryCount}");
            }

            public static void UploadError(Exception ex, string module)
            {
                Log.LogDebug((int)EventIds.UploadError, ex, $"Error uploading logs for {module}");
            }
        }
    }
}
