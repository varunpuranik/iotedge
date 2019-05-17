// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System;
    using App.Metrics.Counter;

    public static class Metrics
    {
        IMetricsCounter Counter { get; } = new NullMetricsCounter();

        IMetricsMeter Meter { get; } = new NullMetricsMeter();

        IMetricsTimer Timer { get; } = new NullMetricsTimer();

        IMetricsHistogram Histogram { get; } = new NullMetricsHistogram();
    }

    public interface IMetricsCollector
    {
        IMetricsCounter Counter { get; }
        IMetricsMeter Meter { get; }
        IMetricsTimer Timer { get; }
        IMetricsHistogram Histogram { get; }
    }

    public interface IMetricsCounter
    {
        void Increment(long amount);
        void Decrement(long amount);
    }

    public interface IMetricsMeter
    {
        void Mark(long amount);
    }

    public interface IMetricsTimer
    {
        IDisposable GetTimer(string name);
    }

    public interface IMetricsHistogram
    {

    }
}
