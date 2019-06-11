// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System.Threading.Tasks;

    public interface IMetricsProvider
    {
        IMetricsCounter CreateCounter(string name, string[] labelNames);

        IMetricsGauge CreateGauge(string name, string[] labelNames);

        IMetricsTimer CreateTimer(string name, string[] labelNames);

        IMetricsHistogram CreateHistogram(string name, string[] labelNames);

        Task StartListener();
    }
}
