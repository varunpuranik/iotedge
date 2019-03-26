// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using App.Metrics;
    using App.Metrics.AspNetCore;
    using App.Metrics.Formatters;
    using App.Metrics.Formatters.Prometheus;
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Hosting;

    public class Hosting
    {
        public static IWebHost BuildWebHost(IMetricsRoot metricsCollector)
        {
            return WebHost.CreateDefaultBuilder()
                .ConfigureMetrics(metricsCollector)
                .UseMetrics(
                    options =>
                    {
                        options.EndpointOptions = endpointsOptions =>
                        {
                            endpointsOptions.MetricsEndpointOutputFormatter = metricsCollector.OutputMetricsFormatters.GetType<MetricsPrometheusTextOutputFormatter>();
                        };
                    })
                .Build();
        }
    }
}
