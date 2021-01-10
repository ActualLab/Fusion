using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using RestEase;
using Stl.CommandR;
using Stl.DependencyInjection;
using Stl.Fusion.Bridge;
using Stl.Fusion.Bridge.Interception;
using Stl.Fusion.Client.RestEase.Internal;
using Stl.Fusion.Interception;
using Stl.Interception;
using Stl.Reflection;
using Stl.Serialization;

namespace Stl.Fusion.Client
{
    public struct FusionRestEaseClientBuilder
    {
        private class AddedTag { }
        private static readonly ServiceDescriptor AddedTagDescriptor =
            new(typeof(AddedTag), new AddedTag());

        public FusionBuilder Fusion { get; }
        public IServiceCollection Services => Fusion.Services;

        internal FusionRestEaseClientBuilder(FusionBuilder fusion)
        {
            Fusion = fusion;
            if (Services.Contains(AddedTagDescriptor))
                return;
            // We want above Contains call to run in O(1), so...
            Services.Insert(0, AddedTagDescriptor);

            Fusion.AddReplicator();
            Services.TryAddSingleton<WebSocketChannelProvider.Options>();
            Services.TryAddSingleton<IChannelProvider, WebSocketChannelProvider>();

            // FusionHttpMessageHandler (handles Fusion headers)
            Services.AddHttpClient();
            Services.TryAddEnumerable(ServiceDescriptor.Singleton<
                IHttpMessageHandlerBuilderFilter,
                FusionHttpMessageHandlerBuilderFilter>());
            Services.TryAddTransient<FusionHttpMessageHandler>();

            // ResponseDeserializer & ReplicaResponseDeserializer
            Services.TryAddTransient<ResponseDeserializer>(c => new JsonResponseDeserializer() {
                JsonSerializerSettings = JsonNetSerializer.DefaultSettings
            });
            Services.TryAddTransient<RequestBodySerializer>(c => new JsonRequestBodySerializer() {
                JsonSerializerSettings = JsonNetSerializer.DefaultSettings
            });
        }

        // ConfigureXxx

        public FusionRestEaseClientBuilder ConfigureHttpClientFactory(
            Action<IServiceProvider, string?, HttpClientFactoryOptions> httpClientFactoryOptionsBuilder)
        {
            Services.Configure(httpClientFactoryOptionsBuilder);
            return this;
        }

        public FusionRestEaseClientBuilder ConfigureWebSocketChannel(
            WebSocketChannelProvider.Options options)
        {
            var serviceDescriptor = new ServiceDescriptor(
                typeof(WebSocketChannelProvider.Options),
                options);
            Services.Replace(serviceDescriptor);
            return this;
        }

        public FusionRestEaseClientBuilder ConfigureWebSocketChannel(
            Action<IServiceProvider, WebSocketChannelProvider.Options> optionsBuilder)
        {
            var serviceDescriptor = new ServiceDescriptor(
                typeof(WebSocketChannelProvider.Options),
                c => {
                    var options = new WebSocketChannelProvider.Options();
                    optionsBuilder.Invoke(c, options);
                    return options;
                },
                ServiceLifetime.Singleton);
            Services.Replace(serviceDescriptor);
            return this;
        }

        // User-defined client-side services

        public FusionRestEaseClientBuilder AddClientService<TClient>(string? clientName = null)
            => AddClientService(typeof(TClient), clientName);
        public FusionRestEaseClientBuilder AddClientService<TService, TClient>(string? clientName = null)
            => AddClientService(typeof(TService), typeof(TClient), clientName);
        public FusionRestEaseClientBuilder AddClientService(Type clientType, string? clientName = null)
            => AddClientService(clientType, clientType, clientName);
        public FusionRestEaseClientBuilder AddClientService(Type serviceType, Type clientType, string? clientName = null)
        {
            if (!(serviceType.IsInterface && serviceType.IsVisible))
                throw Internal.Errors.InterfaceTypeExpected(serviceType, true, nameof(serviceType));
            if (!(clientType.IsInterface && clientType.IsVisible))
                throw Internal.Errors.InterfaceTypeExpected(clientType, true, nameof(clientType));
            clientName ??= clientType.FullName;

            object Factory(IServiceProvider c)
            {
                // 1. Create REST client (of clientType)
                var httpClientFactory = c.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(clientName);
                var client = new RestClient(httpClient) {
                    RequestBodySerializer = c.GetRequiredService<RequestBodySerializer>(),
                    ResponseDeserializer = c.GetRequiredService<ResponseDeserializer>(),
                }.For(clientType);

                // 2. Create view mapping clientType to serviceType
                if (clientType != serviceType)
                    client = c.GetTypeViewFactory().CreateView(client, clientType, serviceType);

                return client;
            }

            Services.TryAddSingleton(serviceType, Factory);
            return this;
        }

        public FusionRestEaseClientBuilder AddReplicaService<TClient>(
            string? clientName = null, bool isCommandService = true)
            where TClient : class
            => AddReplicaService(typeof(TClient), clientName, isCommandService);
        public FusionRestEaseClientBuilder AddReplicaService<TService, TClient>(
            string? clientName = null, bool isCommandService = true)
            where TClient : class
            => AddReplicaService(typeof(TService), typeof(TClient), clientName, isCommandService);
        public FusionRestEaseClientBuilder AddReplicaService(
            Type clientType,
            string? clientName = null, bool isCommandService = true)
            => AddReplicaService(clientType, clientType, clientName, isCommandService);
        public FusionRestEaseClientBuilder AddReplicaService(
            Type serviceType, Type clientType,
            string? clientName = null, bool isCommandService = true)
        {
            if (!(serviceType.IsInterface && serviceType.IsVisible))
                throw Internal.Errors.InterfaceTypeExpected(serviceType, true, nameof(serviceType));
            if (!(clientType.IsInterface && clientType.IsVisible))
                throw Internal.Errors.InterfaceTypeExpected(clientType, true, nameof(clientType));
            clientName ??= clientType.FullName;

            object Factory(IServiceProvider c)
            {
                // 1. Validate types
                var replicaMethodInterceptor = c.GetRequiredService<ReplicaMethodInterceptor>();
                replicaMethodInterceptor.ValidateType(clientType);
                var commandMethodInterceptor = c.GetRequiredService<ComputeMethodInterceptor>();
                commandMethodInterceptor.ValidateType(serviceType);

                // 2. Create REST client (of clientType)
                var httpClientFactory = c.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(clientName);
                var client = new RestClient(httpClient) {
                    RequestBodySerializer = c.GetRequiredService<RequestBodySerializer>(),
                    ResponseDeserializer = c.GetRequiredService<ResponseDeserializer>()
                }.For(clientType);

                // 3. Create view mapping clientType to serviceType
                if (clientType != serviceType)
                    client = c.GetTypeViewFactory().CreateView(client, clientType, serviceType);

                // 4. Create Replica Client
                var replicaProxyGenerator = c.GetRequiredService<IReplicaServiceProxyGenerator>();
                var replicaProxyType = replicaProxyGenerator.GetProxyType(serviceType, isCommandService);
                var replicaInterceptors = c.GetRequiredService<ReplicaServiceInterceptor[]>();
                client = replicaProxyType.CreateInstance(replicaInterceptors, client);
                return client;
            }

            Services.TryAddSingleton(serviceType, Factory);
            if (isCommandService)
                Services.AddCommander().AddCommandService(serviceType);
            return this;
        }

    }
}
