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
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;

    public class TwinConfigSourceModule : Module
    {
        const string DockerType = "docker";
        readonly string backupConfigFilePath;
        readonly IConfiguration configuration;
        readonly VersionInfo versionInfo;
        readonly TimeSpan configRefreshFrequency;
        readonly string iotHubHostName;
        readonly string deviceId;

        public TwinConfigSourceModule(
            string backupConfigFilePath,
            IConfiguration config,
            VersionInfo versionInfo,
            TimeSpan configRefreshFrequency,
            string iotHubHostName,
            string deviceId)
        {
            this.backupConfigFilePath = Preconditions.CheckNonWhiteSpace(backupConfigFilePath, nameof(backupConfigFilePath));
            this.configuration = Preconditions.CheckNotNull(config, nameof(config));
            this.versionInfo = Preconditions.CheckNotNull(versionInfo, nameof(versionInfo));
            this.configRefreshFrequency = configRefreshFrequency;
            this.iotHubHostName = iotHubHostName;
            this.deviceId = deviceId;
        }

        protected override void Load(ContainerBuilder builder)
        {
            // ILogsUploader
            builder.Register(c => new AzureBlobUploader(this.iotHubHostName, this.deviceId))
                .As<ILogsUploader>()
                .SingleInstance();

            builder.Register(
                    async c =>
                    {
                        var runtimeInfoProvider = await c.Resolve<Task<IRuntimeInfoProvider>>();
                        var environmentLogsProvider = new EnvironmentLogs(runtimeInfoProvider);
                        var filterLogsProvider = new LogsFilterProcessor(environmentLogsProvider, this.iotHubHostName, this.deviceId);
                        var logsCompressionProvider = new LogsCompressor(filterLogsProvider);
                        return (ILogsProcessor)logsCompressionProvider;
                    })
                .As<Task<ILogsProcessor>>()
                .SingleInstance();

            // IRequestManager
            builder.Register(async c =>
                {
                    var logsUploader = c.Resolve<ILogsUploader>();
                    var logsProcessor = await c.Resolve<Task<ILogsProcessor>>();
                    IRequestHandler pingRequestHandler = new PingRequestHandler();
                    IRequestHandler logsUploadHandler = new LogsUploadRequestHandler(logsUploader, logsProcessor);
                    return new RequestManager(new[] { pingRequestHandler, logsUploadHandler }) as IRequestManager;
                })
                .As<Task<IRequestManager>>()
                .SingleInstance();

            // IEdgeAgentConnection
            builder.Register(
                    async c =>
                    {
                        var requestManagerTask = c.Resolve<Task<IRequestManager>>();
                        var serde = c.Resolve<ISerde<DeploymentConfig>>();
                        var deviceClientprovider = c.Resolve<IModuleClientProvider>();
                        var requestManager = await requestManagerTask;
                        IEdgeAgentConnection edgeAgentConnection = new EdgeAgentConnection(deviceClientprovider, serde, requestManager, this.configRefreshFrequency);
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

                        var edgeAgentConnectionTask = c.Resolve<Task<IEdgeAgentConnection>>();
                        IEdgeAgentConnection edgeAgentConnection = await edgeAgentConnectionTask;

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
