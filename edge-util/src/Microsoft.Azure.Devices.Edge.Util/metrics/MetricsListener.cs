// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public class MetricsListener : IDisposable
    {
        readonly HttpListener httpListener;
        readonly CancellationTokenSource cts = new CancellationTokenSource();
        readonly IMetricsProvider metricsProvider;
        readonly Task processTask;
        readonly ILogger logger;

        MetricsListener(HttpListener httpListener, IMetricsProvider metricsProvider, ILogger logger)
        {
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
            this.httpListener = httpListener;
            this.metricsProvider = Preconditions.CheckNotNull(metricsProvider, nameof(metricsProvider));
            this.processTask = this.ProcessRequests();
        }

        public static MetricsListener Create(string url, IMetricsProvider metricsProvider, ILogger logger)
        {
            Preconditions.CheckNonWhiteSpace(url, nameof(url));
            try
            {
                var httpListener = new HttpListener();
                httpListener.Prefixes.Add(url);
                httpListener.Start();
                return new MetricsListener(httpListener, metricsProvider, logger);
            }
            catch (Exception e)
            {
                logger?.LogError(e, "Error creating metrics listener");
                throw;
            }
        }

        public void Dispose()
        {
            this.cts.Cancel();
            this.processTask.Wait();
            this.httpListener.Stop();
            ((IDisposable)this.httpListener)?.Dispose();
        }

        async Task ProcessRequests()
        {
            try
            {
                while (!this.cts.IsCancellationRequested)
                {
                    HttpListenerContext context = await this.httpListener.GetContextAsync();
                    using (Stream output = context.Response.OutputStream)
                    {
                        byte[] snapshot = await this.metricsProvider.GetSnapshot(this.cts.Token);
                        await output.WriteAsync(snapshot, 0, snapshot.Length, this.cts.Token);
                    }
                }
            }
            catch (Exception e)
            {
                this.logger.LogError(e, "Error processing metrics requests");
            }
        }
    }
}
