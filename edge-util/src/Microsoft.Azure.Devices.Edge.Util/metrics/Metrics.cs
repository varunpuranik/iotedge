// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Devices.Edge.Util.Metrics.AppMetrics;
    using Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class Metrics : IDisposable
    {
        const string DefaultHost = "*";
        const int DefaultPort = 80;
        const string DefaultSuffix = "metrics";
        const string MetricsUrlPrefixFormat = "http://{0}:{1}/{2}/";
        static readonly object StateLock = new object();
        static Option<MetricsListener> metricsListener = Option.None<MetricsListener>();

        public static IMetricsProvider Instance { get; private set; } = new NullMetricsProvider();

        public static void InitPrometheusMetrics(IConfiguration configuration, ILogger logger)
        {
            Preconditions.CheckNotNull(configuration, nameof(configuration));
            Preconditions.CheckNotNull(logger, nameof(logger));

            bool enabled = configuration.GetValue("enabled", false);
            if (enabled)
            {
                string suffix = DefaultSuffix;
                string host = DefaultHost;
                int port = DefaultPort;
                IConfiguration listenerConfiguration = configuration.GetSection("listener");
                if (listenerConfiguration != null)
                {
                    suffix = listenerConfiguration.GetValue("suffix", DefaultSuffix);
                    port = listenerConfiguration.GetValue("port", DefaultPort);
                    host = listenerConfiguration.GetValue("host", DefaultHost);
                }

                string url = GetMetricsListenerUrlPrefix(host, port, suffix);
                InitPrometheusMetrics(url, logger);
            }
        }

        public static void InitPrometheusMetrics(string prefixUrl, ILogger logger)
        {
            lock (StateLock)
            {
                if (!metricsListener.HasValue)
                {
                    Instance = MetricsProvider.Create();
                    metricsListener = Option.Some(MetricsListener.Create(prefixUrl, Instance, logger));
                    logger.LogInformation($"Initialized EdgeHub metrics in prometheus format which can be available here - {prefixUrl}");
                }
            }
        }

        public void Dispose() => metricsListener.ForEach(m => m.Dispose());

        static string GetMetricsListenerUrlPrefix(string host, int port, string urlSuffix)
            => string.Format(CultureInfo.InvariantCulture, MetricsUrlPrefixFormat, host, port.ToString(), urlSuffix.Trim('/', ' '));
    }
}
