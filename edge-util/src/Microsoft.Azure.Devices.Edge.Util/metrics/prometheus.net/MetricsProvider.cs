// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.Prometheus.Net
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using App.Metrics.Serialization;
    using global::Prometheus;
    using Microsoft.Azure.Devices.Edge.Util.Metrics.Prometheus.Net;

    public class MetricsProvider : IMetricsProvider
    {
        public IMetricsGauge CreateGauge(string name, string description, string[] labelNames)
            => new MetricsGauge(name, description, labelNames);

        public IMetricsCounter CreateCounter(string name, string description, string[] labelNames)
            => new MetricsCounter(name, description, labelNames);

        public IMetricsTimer CreateTimer(string name, string description, string[] labelNames)
            => new MetricsTimer(name, description, labelNames);

        public IMetricsHistogram CreateHistogram(string name, string description, string[] labelNames)
            => new MetricsHistogram(name, description, labelNames);

        public async Task<byte[]> GetSnapshot(CancellationToken cancellationToken)
        {
            using (var ms = new MemoryStream())
            {
                await Metrics.DefaultRegistry.CollectAndExportAsTextAsync(ms, cancellationToken);
                return ms.ToArray();
            }
        }
    }
}
