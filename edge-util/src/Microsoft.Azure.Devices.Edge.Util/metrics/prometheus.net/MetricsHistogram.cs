// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.Prometheus.Net
{
    using global::Prometheus;

    public class MetricsHistogram : IMetricsHistogram
    {
        readonly Summary summary;

        public MetricsHistogram(string name, string description, string[] labels)
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
                    LabelNames = labels
                });
        }

        public void Update(long value) => this.summary.Observe(value);

        public void Update(long value, string[] labelValues) =>
            this.summary
                .WithLabels(labelValues)
                .Observe(value);
    }
}
