// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System.Threading.Tasks;

    public class PingRequestHandler : RequestHandlerBase<object, object>
    {
        public PingRequestHandler()
            : base("ping")
        { }

        protected override Task<object> HandleRequestInternal(object payload)
            => Task.FromResult(default(object));

        protected override object ParsePayload(string payloadJson) => new object();
    }
}
