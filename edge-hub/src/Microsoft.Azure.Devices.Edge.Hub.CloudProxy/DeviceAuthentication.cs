// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;

    public class DeviceAuthentication : DeviceAuthenticationWithTokenRefresh
    {
        const int TokenTimeToLiveSeconds = 3600; // 1 hour
        const int TokenExpiryBufferPercentage = 8; // Assuming a standard token for 1 hr, we set expiry time to around 5 mins.

        readonly ITokenProvider tokenProvider;

        public DeviceAuthentication(ITokenProvider tokenProvider, string deviceId)
            : base(deviceId, TokenTimeToLiveSeconds, TokenExpiryBufferPercentage)
        {
            this.tokenProvider = Preconditions.CheckNotNull(tokenProvider);
        }

        public DeviceAuthentication(ITokenProvider tokenProvider, string deviceId, int suggestedTimeToLive, int timeBufferPercentage)
            : base(deviceId, suggestedTimeToLive, timeBufferPercentage)
        {
            this.tokenProvider = Preconditions.CheckNotNull(tokenProvider);
        }

        protected override Task<string> SafeCreateNewToken(string iotHub, int suggestedTimeToLive) =>
            this.tokenProvider.GetTokenAsync(Option.Some(TimeSpan.FromSeconds(suggestedTimeToLive)));
    }
}
