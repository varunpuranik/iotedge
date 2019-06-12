// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IMetricsProvider
    {
        IMetricsCounter CreateCounter(string name, string description, string[] labelNames);

        IMetricsGauge CreateGauge(string name, string description, string[] labelNames);

        IMetricsTimer CreateTimer(string name, string description, string[] labelNames);

        IMetricsHistogram CreateHistogram(string name, string description, string[] labelNames);

        Task<byte[]> GetSnapshot(CancellationToken cancellationToken);
    }
}
