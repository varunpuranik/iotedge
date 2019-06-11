// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.Prometheus.Net
{
    using global::Prometheus;

    public class MetricsGauge : IMetricsGauge
    {
        readonly Gauge gauge;

        public MetricsGauge(string name, string description, string[] labelNames)
        {
            this.gauge = Metrics.CreateGauge(name, description, labelNames);
        }

        public void Set(long value, string[] labelValues) => this.gauge.WithLabels(labelValues).Set(value);
    }
}
