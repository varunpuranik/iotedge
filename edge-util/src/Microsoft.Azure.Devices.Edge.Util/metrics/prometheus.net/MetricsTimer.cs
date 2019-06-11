// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.Prometheus.Net
{
    using System;
    using global::Prometheus;
    
    public class MetricsTimer : IMetricsTimer
    {
        readonly Summary summary;

        public MetricsTimer(string name, string description, string[] labelNames)
        {
            this.summary = Metrics.CreateSummary(
                name,
                description,
                new SummaryConfiguration
                {
                    Objectives = new[]
                    {
                        new QuantileEpsilonPair(0.5, 0.05),
                        new QuantileEpsilonPair(0.9, 0.05),
                        new QuantileEpsilonPair(0.95, 0.01),
                        new QuantileEpsilonPair(0.99, 0.01),
                        new QuantileEpsilonPair(0.999, 0.01),
                        new QuantileEpsilonPair(0.9999, 0.01),
                    },
                    LabelNames = labelNames
                });
        }

        public IDisposable GetTimer(string[] labelValues) => this.summary.WithLabels(labelValues).NewTimer();
    }
}
