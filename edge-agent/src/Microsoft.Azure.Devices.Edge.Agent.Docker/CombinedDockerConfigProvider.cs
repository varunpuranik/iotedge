// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// This implementation combines docker image and docker create
    /// options from the module, and the registry credentials from the runtime info or environment
    /// and returns them.
    /// </summary>
    public class CombinedDockerConfigProvider : ICombinedConfigProvider<CombinedDockerConfig>
    {
        readonly IEnumerable<AuthConfig> authConfigs;
        readonly ISecretsProvider secretsProvider;

        public CombinedDockerConfigProvider(IEnumerable<AuthConfig> authConfigs, ISecretsProvider secretsProvider)
        {
            this.authConfigs = Preconditions.CheckNotNull(authConfigs, nameof(authConfigs));
            this.secretsProvider = Preconditions.CheckNotNull(secretsProvider, nameof(secretsProvider));
        }

        public virtual async Task<CombinedDockerConfig> GetCombinedConfig(IModule module, IRuntimeInfo runtimeInfo)
        {
            if (!(module is IModule<DockerConfig> moduleWithDockerConfig))
            {
                throw new InvalidOperationException("Module does not contain DockerConfig");
            }

            if (!(runtimeInfo is IRuntimeInfo<DockerRuntimeConfig> dockerRuntimeConfig))
            {
                throw new InvalidOperationException("RuntimeInfo does not contain DockerRuntimeConfig");
            }

            // Convert registry credentials from config to AuthConfig objects            
            List<Task<AuthConfig>> deploymentAuthConfigTasks = dockerRuntimeConfig.Config.RegistryCredentials
                .Select(async c => new AuthConfig { ServerAddress = c.Value.Address, Username = c.Value.Username, Password = await this.GetPassword(c.Value) })
                .ToList();

            AuthConfig[] deploymentAuthConfigs = await Task.WhenAll(deploymentAuthConfigTasks);

            // First try to get matching auth config from the runtime info. If no match is found,
            // then try the auth configs from the environment
            Option<AuthConfig> authConfig = deploymentAuthConfigs.FirstAuthConfig(moduleWithDockerConfig.Config.Image)
                .Else(() => this.authConfigs.FirstAuthConfig(moduleWithDockerConfig.Config.Image));

            return new CombinedDockerConfig(moduleWithDockerConfig.Config.Image, moduleWithDockerConfig.Config.CreateOptions, authConfig);
        }

        Task<string> GetPassword(RegistryCredentials registryCredentials) =>
            registryCredentials.Password
                .Map(Task.FromResult)
                .GetOrElse(
                    () => registryCredentials.SecretPassword.Map(this.secretsProvider.GetSecret).GetOrElse(Task.FromResult(string.Empty)));
    }
}
