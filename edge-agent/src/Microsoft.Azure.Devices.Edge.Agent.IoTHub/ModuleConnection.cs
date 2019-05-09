// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Requests;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.Stream;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;

    public interface IModuleConnection
    {
        Task<IModuleClient> GetModuleClient(ConnectionStatusChangesHandler connectionStatusChangesHandler);
    }

    public class ModuleConnection : IModuleConnection
    {
        readonly IModuleClientProvider moduleClientProvider;
        readonly AsyncLock stateLock = new AsyncLock();
        readonly IRequestManager requestManager;
        readonly IStreamRequestListener streamRequestListener;
        readonly ConnectionStatusChangesHandler connectionStatusChangesHandler;
        readonly DesiredPropertyUpdateCallback desiredPropertyUpdateCallback;
        readonly bool enableSubscriptions;

        Option<IModuleClient> moduleClient;

        public ModuleConnection(IModuleClientProvider moduleClientProvider, bool enableSubscriptions)
        {
            this.moduleClientProvider = Preconditions.CheckNotNull(moduleClientProvider, nameof(moduleClientProvider));
        }

        public async Task<IModuleClient> GetModuleClient()
        {
            IModuleClient moduleClient = await this.moduleClient
                .Filter(m => m.IsActive)
                .Map(Task.FromResult)
                .GetOrElse(this.InitModuleClient);
            return moduleClient;
        }

        async Task<MethodResponse> MethodCallback(MethodRequest methodRequest, object _)
        {
            (int responseStatus, Option<string> responsePayload) = await this.requestManager.ProcessRequest(methodRequest.Name, methodRequest.DataAsJson);
            return responsePayload
                .Map(r => new MethodResponse(Encoding.UTF8.GetBytes(r), responseStatus))
                .GetOrElse(() => new MethodResponse(responseStatus));
        }

        async Task<IModuleClient> InitModuleClient()
        {
            using (await this.stateLock.LockAsync())
            {
                IModuleClient moduleClient = await this.moduleClient
                    .Filter(m => m.IsActive)
                    .Map(Task.FromResult)
                    .GetOrElse(
                        async () =>
                        {
                            IModuleClient mc = await this.moduleClientProvider.Create(this.connectionStatusChangesHandler);
                            mc.Closed += this.OnModuleClientClosed;
                            await mc.SetDefaultMethodHandlerAsync(this.MethodCallback);
                            await mc.SetDesiredPropertyUpdateCallbackAsync(this.desiredPropertyUpdateCallback);
                            this.streamRequestListener.InitPump(mc);
                            this.moduleClient = Option.Some(mc);
                            return mc;
                        });
                return moduleClient;
            }
        }

        async void OnModuleClientClosed(object sender, System.EventArgs e)
        {
            try
            {
                await this.InitModuleClient();
            }
            catch (Exception ex)
            {
                Events.ErrorHandlingModuleClosedEvent(ex);
            }
        }

        static class Events
        {
            public static readonly ILogger Log = Logger.Factory.CreateLogger<ModuleClient>();
            const int IdStart = AgentEventIds.ModuleClientProvider;

            enum EventIds
            {
                ClosingModuleClient = IdStart,
                ExceptionInHandleException,
                TimedOutClosing,
                ErrorClosingClient
            }

            public static void ClosingModuleClient(Exception ex)
            {
                Log.LogWarning((int)EventIds.ClosingModuleClient, ex, "Closing module client");
            }

            public static void ExceptionInHandleException(ModuleClient moduleClient, Exception ex, Exception e)
            {
                Log.LogWarning((int)EventIds.ExceptionInHandleException, "Encountered error - {e} while trying to handle error {ex.Message}");
            }

            public static void ErrorClosingClient(Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorClosingClient, ex, "Error closing module client");
            }

            public static void TimedOutClosing()
            {
                Log.LogInformation((int)EventIds.TimedOutClosing, "Edge agent module client timed out due to inactivity, closing...");
            }
        }
    }
}
