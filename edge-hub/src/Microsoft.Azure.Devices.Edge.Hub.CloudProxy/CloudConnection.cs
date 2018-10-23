// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Common.Security;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    /// <summary>
    /// This class creates and manages cloud connections (CloudProxy instances)
    /// </summary>
    class CloudConnection : ICloudConnection
    {
        static readonly TimeSpan TokenExpiryBuffer = TimeSpan.FromMinutes(5); // Token is usable if it does not expire in 5 mins
        const uint OperationTimeoutMilliseconds = 20 * 1000; // 20 secs
        static readonly TimeSpan TokenRetryWaitTime = TimeSpan.FromSeconds(20);

        readonly Action<string, CloudConnectionStatus> connectionStatusChangedHandler;
        readonly ITransportSettings[] transportSettingsList;
        readonly IMessageConverterProvider messageConverterProvider;
        readonly AsyncLock identityUpdateLock = new AsyncLock();
        readonly AsyncLock tokenUpdateLock = new AsyncLock();
        readonly IClientProvider clientProvider;
        readonly ICloudListener cloudListener;
        readonly TimeSpan idleTimeout;
        readonly bool closeOnIdleTimeout;
        readonly IIdentity identity;

        bool callbacksEnabled = true;
        Option<TaskCompletionSource<string>> tokenGetter;
        Option<ICloudProxy> cloudProxy;

        public static async Task<CloudConnection> Create(
            ITokenCredentials tokenCredentials,
            Action<string, CloudConnectionStatus> connectionStatusChangedHandler,
            ITransportSettings[] transportSettings,
            IMessageConverterProvider messageConverterProvider,
            IClientProvider clientProvider,
            ICloudListener cloudListener,
            TimeSpan idleTimeout,
            bool closeOnIdleTimeout)            
        {
            Preconditions.CheckNotNull(tokenCredentials, nameof(tokenCredentials));            
            var cloudConnection = new CloudConnection(
                tokenCredentials.Identity,
                connectionStatusChangedHandler,
                transportSettings,
                messageConverterProvider,
                clientProvider,
                cloudListener,
                idleTimeout,
                closeOnIdleTimeout);
            ITokenProvider tokenProvider = new ClientTokenBasedTokenProvider(tokenCredentials, cloudConnection);
            ICloudProxy cloudProxy = await cloudConnection.GetCloudProxyAsync(tokenProvider);
            cloudConnection.cloudProxy = Option.Some(cloudProxy);
            return cloudConnection;
        }

        public static async Task<CloudConnection> Create(
            IIdentity identity,
            Action<string, CloudConnectionStatus> connectionStatusChangedHandler,
            ITransportSettings[] transportSettings,
            IMessageConverterProvider messageConverterProvider,
            IClientProvider clientProvider,
            ICloudListener cloudListener,
            ITokenProvider tokenProvider,
            TimeSpan idleTimeout,
            bool closeOnIdleTimeout)
        {
            Preconditions.CheckNotNull(tokenProvider, nameof(tokenProvider));
            var cloudConnection = new CloudConnection(
                identity,
                connectionStatusChangedHandler,
                transportSettings,
                messageConverterProvider,
                clientProvider,
                cloudListener,
                idleTimeout,
                closeOnIdleTimeout);
            ICloudProxy cloudProxy = await cloudConnection.GetCloudProxyAsync(tokenProvider);
            cloudConnection.cloudProxy = Option.Some(cloudProxy);
            return cloudConnection;
        }

        CloudConnection(
            IIdentity identity,
            Action<string, CloudConnectionStatus> connectionStatusChangedHandler,
            ITransportSettings[] transportSettings,
            IMessageConverterProvider messageConverterProvider,
            IClientProvider clientProvider,
            ICloudListener cloudListener,
            TimeSpan idleTimeout,
            bool closeOnIdleTimeout)
        {
            this.identity = Preconditions.CheckNotNull(identity, nameof(identity));
            this.connectionStatusChangedHandler = connectionStatusChangedHandler;
            this.transportSettingsList = Preconditions.CheckNotNull(transportSettings, nameof(transportSettings));
            this.messageConverterProvider = Preconditions.CheckNotNull(messageConverterProvider, nameof(messageConverterProvider));
            this.tokenGetter = Option.None<TaskCompletionSource<string>>();
            this.clientProvider = Preconditions.CheckNotNull(clientProvider, nameof(clientProvider));
            this.cloudListener = Preconditions.CheckNotNull(cloudListener, nameof(cloudListener));
            this.idleTimeout = idleTimeout;
            this.closeOnIdleTimeout = closeOnIdleTimeout;
            this.cloudProxy = Option.None<ICloudProxy>();
        }

        public Option<ICloudProxy> CloudProxy => this.cloudProxy.Filter(cp => cp.IsActive);

        public bool IsActive => this.cloudProxy
            .Map(cp => cp.IsActive)
            .GetOrElse(false);

        public Task<bool> CloseAsync() => this.cloudProxy.Map(cp => cp.CloseAsync()).GetOrElse(Task.FromResult(false));

        /// <summary>
        /// This method does the following -
        ///     1. Updates the identity to be used for the cloud connection
        ///     2. Updates the cloud proxy -
        ///         i. If there is an existing device client and 
        ///             a. If is waiting for an updated token, and the identity has a token,
        ///                then it uses that to give it to the waiting client authentication method.
        ///             b. If not, then it creates a new cloud proxy (and device client) and closes the existing one
        ///         ii. Else, if there is no cloud proxy, then opens a device client and creates a cloud proxy. 
        /// </summary>
        public async Task<ICloudProxy> UpdateTokenAsync(ITokenCredentials newTokenCredentials)
        {
            Preconditions.CheckNotNull(newTokenCredentials, nameof(newTokenCredentials));

            using (await this.identityUpdateLock.LockAsync())
            {
                // Disable callbacks while we update the cloud proxy.
                // TODO - instead of this, make convert Option<ICloudProxy> CloudProxy to Task<Option<ICloudProxy>> GetCloudProxy
                // which can be awaited when an update is in progress.
                this.callbacksEnabled = false;
                try
                {
                    ITokenProvider tokenProvider = new ClientTokenBasedTokenProvider(newTokenCredentials, this);
                    // First check if there is an existing cloud proxy
                    ICloudProxy proxy = await this.cloudProxy.Map(
                        async cp =>
                        {
                            // If the identity has a token, and we have a tokenGetter, that means
                            // the connection is waiting for a new token. So give it the token and
                            // complete the tokenGetter
                            if (this.tokenGetter.HasValue)
                            {
                                if (IsTokenExpired(this.identity.IotHubHostName, newTokenCredentials.Token))
                                {
                                    throw new InvalidOperationException($"Token for client {this.identity.Id} is expired");
                                }

                                this.tokenGetter.ForEach(tg =>
                                 {
                                     // First reset the token getter and then set the result.
                                     this.tokenGetter = Option.None<TaskCompletionSource<string>>();
                                     tg.SetResult(newTokenCredentials.Token);
                                 });
                                return cp;
                            }
                            // Else this is a new connection for the same device Id. So open a new connection,
                            // and if that is successful, close the existing one.
                            else
                            {
                                ICloudProxy newCloudProxy = await this.GetCloudProxyAsync(tokenProvider);
                                await cp.CloseAsync();
                                return newCloudProxy;
                            }
                        })
                        // No existing cloud proxy, so just create a new one.
                        .GetOrElse(() => this.GetCloudProxyAsync(tokenProvider));

                    // Set identity only after successfully opening cloud proxy
                    // That way, if a we have one existing connection for a deviceA,
                    // and a new connection for deviceA comes in with an invalid key/token,
                    // the existing connection is not affected.
                    this.cloudProxy = Option.Some(proxy);
                    Events.UpdatedCloudConnection(this.identity);
                    return proxy;
                }
                catch (Exception ex)
                {
                    Events.CreateException(ex, this.identity);
                    throw;
                }
                finally
                {
                    this.callbacksEnabled = true;
                }
            }
        }

        async Task<ICloudProxy> GetCloudProxyAsync(ITokenProvider newTokenProvider)
        {
            IClient client = await this.ConnectToIoTHub(newTokenProvider);
            ICloudProxy proxy = new CloudProxy(client,
                this.messageConverterProvider,
                this.identity.Id,
                this.connectionStatusChangedHandler,
                this.cloudListener,
                this.idleTimeout,
                this.closeOnIdleTimeout);
            return proxy;
        }

        async Task<IClient> ConnectToIoTHub(ITokenProvider newTokenProviders)
        {
            Try<IClient> deviceClientTry = await Fallback.ExecuteAsync(
                this.transportSettingsList.Select<ITransportSettings, Func<Task<IClient>>>(
                    ts =>
                        () => this.CreateDeviceClient(newTokenProviders, ts)).ToArray());

            return deviceClientTry.Success ? deviceClientTry.Value : throw deviceClientTry.Exception;
        }

        async Task<IClient> CreateDeviceClient(
            ITokenProvider newTokenProvider,
            ITransportSettings transportSettings)
        {
            Events.AttemptingConnectionWithTransport(transportSettings.GetTransportType(), this.identity);
            IClient client = this.clientProvider.Create(this.identity, newTokenProvider, new[] { transportSettings });
            client.SetOperationTimeoutInMilliseconds(OperationTimeoutMilliseconds);
            client.SetConnectionStatusChangedHandler(this.InternalConnectionStatusChangesHandler);

            // TODO: Add support for ProductInfo 
            //if (!string.IsNullOrWhiteSpace(newCredentials.ProductInfo))
            //{
            //    client.SetProductInfo(newCredentials.ProductInfo);
            //}

            await client.OpenAsync();
            Events.CreateDeviceClientSuccess(transportSettings.GetTransportType(), OperationTimeoutMilliseconds, this.identity);
            return client;
        }

        void InternalConnectionStatusChangesHandler(ConnectionStatus status, ConnectionStatusChangeReason reason)
        {
            // Don't invoke the callbacks if callbacks are not enabled, i.e. when the
            // cloudProxy is being updated. That is because this method can be called before
            // this.CloudProxy has been set/updated, so the old CloudProxy object may be returned.
            if (this.callbacksEnabled)
            {
                if (status == ConnectionStatus.Connected)
                {
                    this.connectionStatusChangedHandler?.Invoke(this.identity.Id, CloudConnectionStatus.ConnectionEstablished);
                }
                else if (reason == ConnectionStatusChangeReason.Expired_SAS_Token)
                {
                    this.connectionStatusChangedHandler?.Invoke(this.identity.Id, CloudConnectionStatus.DisconnectedTokenExpired);
                }
                else
                {
                    this.connectionStatusChangedHandler?.Invoke(this.identity.Id, CloudConnectionStatus.Disconnected);
                }
            }
        }

        /// <summary>
        /// If the existing identity has a usable token, then use it.
        /// Else, generate a notification of token being near expiry and return a task that
        /// can be completed later.
        /// Keep retrying till we get a usable token.
        /// Note - Don't use this.Identity in this method, as it may not have been set yet!
        /// </summary>
        async Task<string> GetNewToken(string currentToken)
        {
            Events.GetNewToken(this.identity.Id);
            bool retrying = false;
            string token = currentToken;
            while (true)
            {
                // We have to catch UnauthorizedAccessException, because on IsTokenUsable, we call parse from
                // Device Client and it throws if the token is expired.
                if (IsTokenUsable(this.identity.IotHubHostName, token))
                {
                    if (retrying)
                    {
                        Events.NewTokenObtained(this.identity, token);
                    }
                    else
                    {
                        Events.UsingExistingToken(this.identity.Id);
                    }
                    return token;
                }
                else
                {
                    Events.TokenNotUsable(this.identity, token);
                }

                bool newTokenGetterCreated = false;
                // No need to lock here as the lock is being held by the refresher.
                TaskCompletionSource<string> tcs = this.tokenGetter
                    .GetOrElse(
                        () =>
                        {
                            Events.SafeCreateNewToken(this.identity.Id);
                            var taskCompletionSource = new TaskCompletionSource<string>();
                            this.tokenGetter = Option.Some(taskCompletionSource);
                            newTokenGetterCreated = true;
                            return taskCompletionSource;
                        });

                // If a new tokenGetter was created, then invoke the connection status changed handler
                if (newTokenGetterCreated)
                {
                    // If retrying, wait for some time.
                    if (retrying)
                    {
                        await Task.Delay(TokenRetryWaitTime);
                    }
                    this.connectionStatusChangedHandler(this.identity.Id, CloudConnectionStatus.TokenNearExpiry);
                }

                retrying = true;
                // this.tokenGetter will be reset when this task returns.
                token = await tcs.Task;
            }
        }

        internal static DateTime GetTokenExpiry(string hostName, string token)
        {
            try
            {
                SharedAccessSignature sharedAccessSignature = SharedAccessSignature.Parse(hostName, token);
                DateTime expiryTime = sharedAccessSignature.ExpiresOn.ToUniversalTime();
                return expiryTime;
            }
            catch (UnauthorizedAccessException)
            {
                return DateTime.MinValue;
            }
        }

        internal static bool IsTokenExpired(string hostName, string token)
        {
            try
            {
                SharedAccessSignature sharedAccessSignature = SharedAccessSignature.Parse(hostName, token);
                return sharedAccessSignature.IsExpired();
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }

        internal static TimeSpan GetTokenExpiryTimeRemaining(string hostName, string token) => GetTokenExpiry(hostName, token) - DateTime.UtcNow;

        // Checks if the token expires too soon
        static bool IsTokenUsable(string hostname, string token)
        {
            try
            {
                return GetTokenExpiryTimeRemaining(hostname, token) > TokenExpiryBuffer;
            }
            catch (Exception e)
            {
                Events.ErrorCheckingTokenUsable(e);
                return false;
            }
        }

        class ClientTokenBasedTokenProvider : ITokenProvider
        {
            readonly CloudConnection cloudConnection;
            string token;

            public ClientTokenBasedTokenProvider(ITokenCredentials tokenCredentials, CloudConnection cloudConnection)
            {
                this.cloudConnection = cloudConnection;
                this.token = tokenCredentials.Token;
            }

            public async Task<string> GetTokenAsync(Option<TimeSpan> ttl)
            {
                using (await this.cloudConnection.tokenUpdateLock.LockAsync())
                {
                    try
                    {
                        this.token = await this.cloudConnection.GetNewToken(this.token);
                        return this.token;
                    }
                    catch (Exception ex)
                    {
                        Events.ErrorRenewingToken(ex);
                        throw;
                    }
                }
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<CloudConnection>();
            const int IdStart = CloudProxyEventIds.CloudConnection;

            enum EventIds
            {
                CloudConnectError = IdStart,
                AttemptingTransport,
                TransportConnected,
                CreateNewToken,
                UpdatedCloudConnection,
                ObtainedNewToken,
                ErrorRenewingToken,
                ErrorCheckingTokenUsability
            }

            static string TransportName(TransportType type)
            {
                switch (type)
                {
                    case TransportType.Amqp_Tcp_Only:
                        return "AMQP";
                    case TransportType.Amqp_WebSocket_Only:
                        return "AMQP over WebSocket";
                    default:
                        return type.ToString();
                }
            }

            public static void AttemptingConnectionWithTransport(TransportType transport, IIdentity identity)
            {
                Log.LogInformation((int)EventIds.AttemptingTransport, $"Attempting to connect to IoT Hub for client {identity.Id} via {TransportName(transport)}...");
            }

            public static void CreateDeviceClientSuccess(TransportType transport, uint timeout, IIdentity identity)
            {
                Log.LogInformation((int)EventIds.TransportConnected, $"Created cloud proxy for client {identity.Id} via {TransportName(transport)}, with client operation timeout {timeout}.");
            }

            internal static void GetNewToken(string id)
            {
                Log.LogDebug((int)EventIds.CreateNewToken, Invariant($"Getting new token for {id}."));
            }

            internal static void UsingExistingToken(string id)
            {
                Log.LogInformation((int)EventIds.CreateNewToken, Invariant($"New token requested by client {id}, but using existing token as it is usable."));
            }

            internal static void SafeCreateNewToken(string id)
            {
                Log.LogInformation((int)EventIds.CreateNewToken, Invariant($"Existing token not found for {id}. Getting new token from the client..."));
            }

            internal static void CreateException(Exception ex, IIdentity identity)
            {
                Log.LogError((int)EventIds.CloudConnectError, ex, Invariant($"Error creating or updating the cloud proxy for client {identity.Id}"));
            }

            internal static void UpdatedCloudConnection(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.UpdatedCloudConnection, Invariant($"Updated cloud connection for client {identity.Id}"));
            }

            internal static void NewTokenObtained(IIdentity identity, string newToken)
            {
                TimeSpan timeRemaining = GetTokenExpiryTimeRemaining(identity.IotHubHostName, newToken);
                Log.LogInformation((int)EventIds.ObtainedNewToken, Invariant($"Obtained new token for client {identity.Id} that expires in {timeRemaining}"));
            }

            internal static void ErrorRenewingToken(Exception ex)
            {
                Log.LogDebug((int)EventIds.ErrorRenewingToken, ex, "Critical Error trying to renew Token.");
            }

            public static void ErrorCheckingTokenUsable(Exception ex)
            {
                Log.LogDebug((int)EventIds.ErrorCheckingTokenUsability, ex, "Error checking if token is usable.");
            }

            public static void TokenNotUsable(IIdentity identity, string newToken)
            {
                TimeSpan timeRemaining = GetTokenExpiryTimeRemaining(identity.IotHubHostName, newToken);
                Log.LogDebug((int)EventIds.ObtainedNewToken, Invariant($"Token received for client {identity.Id} expires in {timeRemaining}, and so is not usable. Getting a fresh token..."));
            }
        }
    }
}
