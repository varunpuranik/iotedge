// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Threading.Tasks;

    public interface ISecretsProvider
    {
        Task<string> GetSecret(string secretUrl);
    }

    public class NullSecretsProvider : ISecretsProvider
    {
        public Task<string> GetSecret(string secretUrl) => Task.FromResult(string.Empty);
    }
}
