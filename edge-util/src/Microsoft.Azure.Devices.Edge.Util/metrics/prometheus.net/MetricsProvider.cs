// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.Prometheus.Net
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Metrics.Prometheus.Net;

    public class MetricsProvider : IMetricsProvider
    {
        public IMetricsGauge CreateGauge(string name, Dictionary<string, string> defaultTags) => throw new System.NotImplementedException();

        public IMetricsCounter CreateCounter(string name, Dictionary<string, string> defaultTags) => throw new System.NotImplementedException();

        public IMetricsTimer CreateTimer(string name, Dictionary<string, string> defaultTags) => throw new System.NotImplementedException();

        public IMetricsHistogram CreateHistogram(string name, string description, string[] labelNames) => new MetricsHistogram(name, description, labelNames);

        public async Task<byte[]> GetSnapshot(CancellationToken cancellationToken) => throw new System.NotImplementedException();
    }
}
