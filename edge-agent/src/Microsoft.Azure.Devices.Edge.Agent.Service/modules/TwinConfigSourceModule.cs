// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Service.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Requests;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.ConfigSources;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.Reporters;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.Stream;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;

    public class TwinConfigSourceModule : Module
    {
        const string DockerType = "docker";
        readonly string backupConfigFilePath;
        readonly IConfiguration configuration;
        readonly VersionInfo versionInfo;
        readonly TimeSpan configRefreshFrequency;
        readonly string deviceId;
        readonly string iotHubHostName;

        public TwinConfigSourceModule(
            string iotHubHostname,
            string deviceId,
            string backupConfigFilePath,
            IConfiguration config,
            VersionInfo versionInfo,
            TimeSpan configRefreshFrequency)
        {
            this.iotHubHostName = Preconditions.CheckNonWhiteSpace(iotHubHostname, nameof(iotHubHostname));
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.backupConfigFilePath = Preconditions.CheckNonWhiteSpace(backupConfigFilePath, nameof(backupConfigFilePath));
            this.configuration = Preconditions.CheckNotNull(config, nameof(config));
            this.versionInfo = Preconditions.CheckNotNull(versionInfo, nameof(versionInfo));
            this.configRefreshFrequency = configRefreshFrequency;
        }

        protected override void Load(ContainerBuilder builder)
        {
            // Task<ILogsProvider>
            builder.Register(
                    async c =>
                    {
                        var logsProcessor = new LogsProcessor(new LogMessageParser(this.iotHubHostName, this.deviceId));
                        IRuntimeInfoProvider runtimeInfoProvider = await c.Resolve<Task<IRuntimeInfoProvider>>();
                        return new LogsProvider(runtimeInfoProvider, logsProcessor) as ILogsProvider;
                    })
                    .As<Task<ILogsProvider>>()
                    .SingleInstance();

            // IRequestManager
            builder.Register(
                    c =>
                    {
                        var requestHandlers = new List<IRequestHandler>
                        {
                            new PingRequestHandler()
                        };
                        return new RequestManager(requestHandlers);
                    })
                .As<IRequestManager>()
                .SingleInstance();

            // Task<IStreamRequestListener>
            builder.Register(
                    async c =>
                    {
                        ILogsProvider logsProvider = await c.Resolve<Task<ILogsProvider>>();
                        var streamRequestHandlerProvider = new StreamRequestHandlerProvider(logsProvider);
                        return new StreamRequestListener(streamRequestHandlerProvider) as IStreamRequestListener;
                    })
                .As<Task<IStreamRequestListener>>()
                .SingleInstance();

            // IEdgeAgentConnection
            builder.Register(
                    async c =>
                    {
                        var requestManager = c.Resolve<IRequestManager>();
                        var serde = c.Resolve<ISerde<DeploymentConfig>>();
                        var deviceClientprovider = c.Resolve<IModuleClientProvider>();
                        var streamRequestListener = await c.Resolve<Task<IStreamRequestListener>>();
                        IEdgeAgentConnection edgeAgentConnection = new EdgeAgentConnection(deviceClientprovider, serde, requestManager, streamRequestListener, this.configRefreshFrequency);
                        return edgeAgentConnection;
                    })
                .As<Task<IEdgeAgentConnection>>()
                .SingleInstance();

            // Task<IConfigSource>
            builder.Register(
                    async c =>
                    {
                        var serde = c.Resolve<ISerde<DeploymentConfigInfo>>();
                        var edgeAgentConnectionTask = c.Resolve<Task<IEdgeAgentConnection>>();
                        IEncryptionProvider encryptionProvider = await c.Resolve<Task<IEncryptionProvider>>();
                        IEdgeAgentConnection edgeAgentConnection = await edgeAgentConnectionTask;
                        var twinConfigSource = new TwinConfigSource(edgeAgentConnection, this.configuration);
                        IConfigSource backupConfigSource = new FileBackupConfigSource(this.backupConfigFilePath, twinConfigSource, serde, encryptionProvider);
                        return backupConfigSource;
                    })
                .As<Task<IConfigSource>>()
                .SingleInstance();

            // IReporter
            builder.Register(
                    async c =>
                    {
                        var runtimeInfoDeserializerTypes = new Dictionary<string, Type>
                        {
                            [DockerType] = typeof(DockerReportedRuntimeInfo),
                            [Constants.Unknown] = typeof(UnknownRuntimeInfo)
                        };

                        var edgeAgentDeserializerTypes = new Dictionary<string, Type>
                        {
                            [DockerType] = typeof(EdgeAgentDockerRuntimeModule),
                            [Constants.Unknown] = typeof(UnknownEdgeAgentModule)
                        };

                        var edgeHubDeserializerTypes = new Dictionary<string, Type>
                        {
                            [DockerType] = typeof(EdgeHubDockerRuntimeModule),
                            [Constants.Unknown] = typeof(UnknownEdgeHubModule)
                        };

                        var moduleDeserializerTypes = new Dictionary<string, Type>
                        {
                            [DockerType] = typeof(DockerRuntimeModule)
                        };

                        var deserializerTypesMap = new Dictionary<Type, IDictionary<string, Type>>
                        {
                            { typeof(IRuntimeInfo), runtimeInfoDeserializerTypes },
                            { typeof(IEdgeAgentModule), edgeAgentDeserializerTypes },
                            { typeof(IEdgeHubModule), edgeHubDeserializerTypes },
                            { typeof(IModule), moduleDeserializerTypes }
                        };

                        IEdgeAgentConnection edgeAgentConnection = await c.Resolve<Task<IEdgeAgentConnection>>();

                        return new IoTHubReporter(
                            edgeAgentConnection,
                            new TypeSpecificSerDe<AgentState>(deserializerTypesMap),
                            this.versionInfo) as IReporter;
                    })
                .As<Task<IReporter>>()
                .SingleInstance();

            base.Load(builder);
        }
    }
}
