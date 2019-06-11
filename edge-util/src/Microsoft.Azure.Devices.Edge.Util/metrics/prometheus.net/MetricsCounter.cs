// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.Prometheus.Net
{
    using global::Prometheus;
    
    public class MetricsCounter : IMetricsCounter
    {
        readonly Counter counter;

        public MetricsCounter(string name, string description, string[] labelNames)
        {
            this.counter = Metrics.CreateCounter(name, description, labelNames);
        }

        public void Increment(string[] labelValues) => this.counter.WithLabels(labelValues).Inc();
    }
}
