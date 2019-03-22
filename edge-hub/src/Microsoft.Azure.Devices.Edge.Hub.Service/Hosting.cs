// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System.Net;
    using System.Net.Sockets;
    using System.Security.Cryptography.X509Certificates;
    using App.Metrics.AspNetCore;
    using Autofac;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Server.Kestrel.Https;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Extensions;
    using EdgeMetrics = Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using App.Metrics.Formatters.Prometheus;
    using App.Metrics.Formatters;

    public class Hosting
    {
        Hosting(IWebHost webHost, IContainer container)
        {
            this.WebHost = webHost;
            this.Container = container;
        }

        public IContainer Container { get; }

        public IWebHost WebHost { get; }

        public static Hosting Initialize(
            IConfigurationRoot configuration,
            X509Certificate2 serverCertificate,
            IDependencyManager dependencyManager,
            bool clientCertAuthEnabled)
        {
            int port = configuration.GetValue("httpSettings:port", 443);
            var certificateMode = clientCertAuthEnabled ? ClientCertificateMode.AllowCertificate : ClientCertificateMode.NoCertificate;
            IWebHostBuilder webHostBuilder = new WebHostBuilder()
                .UseKestrel(
                    options =>
                    {
                        options.Listen(
                            !Socket.OSSupportsIPv6 ? IPAddress.Any : IPAddress.IPv6Any,
                            port,
                            listenOptions =>
                            {
                                listenOptions.UseHttpsExtensions(
                                    new HttpsConnectionAdapterOptions()
                                    {
                                        ServerCertificate = serverCertificate,
                                        ClientCertificateValidation = (clientCert, chain, policyErrors) => true,
                                        ClientCertificateMode = certificateMode
                                    });
                            });

                        options.Listen(
                            !Socket.OSSupportsIPv6 ? IPAddress.Any : IPAddress.IPv6Any,
                            18085);
                    })
                .UseSockets()
                .ConfigureServices(
                    serviceCollection =>
                    {
                        serviceCollection.AddSingleton(configuration);
                        serviceCollection.AddSingleton(dependencyManager);
                    })
                .ConfigureMetrics(EdgeMetrics.MetricsCollector.OrDefault())
                .UseMetrics(
                    options =>
                    {                        
                        options.EndpointOptions = endpointsOptions =>
                        {
                            
                            endpointsOptions.MetricsTextEndpointOutputFormatter = EdgeMetrics.MetricsCollector.OrDefault().OutputMetricsFormatters.GetType<MetricsPrometheusTextOutputFormatter>();
                            endpointsOptions.MetricsEndpointOutputFormatter = EdgeMetrics.MetricsCollector.OrDefault().OutputMetricsFormatters.GetType<MetricsPrometheusProtobufOutputFormatter>();
                        };
                    })
                .UseStartup<Startup>();
            IWebHost webHost = webHostBuilder.Build();
            IContainer container = webHost.Services.GetService(typeof(IStartup)) is Startup startup ? startup.Container : null;
            return new Hosting(webHost, container);
        }
    }
}
