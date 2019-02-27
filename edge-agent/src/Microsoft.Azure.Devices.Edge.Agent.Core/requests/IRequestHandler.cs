// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System.Threading.Tasks;

    public interface IRequestHandler
    {
        string RequestName { get; }

        Task<string> HandleRequest(string payloadJson);
    }
}
