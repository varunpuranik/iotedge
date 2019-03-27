// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using System.Net;
    using System.Net.Sockets;
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
            IWebHostBuilder webHostBuilder = new WebHostBuilder()
                .UseKestrel(
                    options =>
                    {
                        options.Listen(
                            !Socket.OSSupportsIPv6 ? IPAddress.Any : IPAddress.IPv6Any,
                            18085);
                    })
                .ConfigureMetrics(metricsCollector)
                .UseMetrics(
                    options =>
                    {
                        options.EndpointOptions = endpointsOptions =>
                        {
                            endpointsOptions.MetricsEndpointOutputFormatter = metricsCollector.OutputMetricsFormatters.GetType<MetricsPrometheusTextOutputFormatter>();
                        };
                    })
                .UseStartup<Startup>();
            IWebHost webHost = webHostBuilder.Build();
            return webHost;
        }
    }
}
