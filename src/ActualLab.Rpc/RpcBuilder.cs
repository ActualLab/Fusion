using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ActualLab.Interception;
using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Clients;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;
using Errors = ActualLab.Rpc.Internal.Errors;

namespace ActualLab.Rpc;

public readonly struct RpcBuilder
{
    public IServiceCollection Services { get; }
    public RpcConfiguration Configuration { get; }

#if NET5_0_OR_GREATER
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Proxies))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcDefaultDelegates))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcMethodDef))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcServiceDef))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcServiceRegistry))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcConfiguration))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcByteArgumentSerializer))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcMethodTracer))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcMethodActivityCounters))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcClientInterceptor))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcRoutingInterceptor))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcInboundContext))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcInboundContextFactory))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcOutboundContext))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcInboundCall<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcInbound404Call<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcOutboundCall<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcMiddlewares<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcInboundMiddleware))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcOutboundMiddleware))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcServerPeer))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcClientPeer))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcHub))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcRemoteObjectTracker))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcSharedObjectTracker))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcSharedStream))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcCacheInfoCapture))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcCacheEntry))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcSystemCalls))]
#endif
    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    internal RpcBuilder(
        IServiceCollection services,
        Action<RpcBuilder>? configure)
    {
        Services = services;
        if (GetConfiguration(services) is { } configuration) {
            // Already configured
            Configuration = configuration;
            configure?.Invoke(this);
            return;
        }

        // We want above GetConfiguration call to run in O(1), so...
        Configuration = new RpcConfiguration();
        services.Insert(0, new ServiceDescriptor(typeof(RpcConfiguration), Configuration));
        services.AddSingleton(c => new RpcHub(c));

        // Common services
        services.TryAddSingleton(c => new RpcServiceRegistry(c));
        services.TryAddSingleton(_ => RpcDefaultDelegates.ServiceDefBuilder);
        services.TryAddSingleton(_ => RpcDefaultDelegates.MethodDefBuilder);
        services.TryAddSingleton(_ => RpcDefaultDelegates.InboundCallFilter);
        services.TryAddSingleton(_ => RpcDefaultDelegates.CallRouter);
        services.TryAddSingleton(_ => RpcDefaultDelegates.InboundContextFactory);
        services.TryAddSingleton(_ => RpcDefaultDelegates.PeerFactory);
        services.TryAddSingleton(_ => RpcDefaultDelegates.ClientConnectionFactory);
        services.TryAddSingleton(_ => RpcDefaultDelegates.ServerConnectionFactory);
        services.TryAddSingleton(_ => RpcDefaultDelegates.BackendServiceDetector);
        services.TryAddSingleton(_ => RpcDefaultDelegates.UnrecoverableErrorDetector);
        services.TryAddSingleton(_ => RpcDefaultDelegates.MethodTracerFactory);
        services.TryAddSingleton(_ => RpcArgumentSerializer.Default);
        services.TryAddSingleton(c => new RpcInboundMiddlewares(c));
        services.TryAddSingleton(c => new RpcOutboundMiddlewares(c));
        services.TryAddTransient(_ => new RpcInboundCallTracker());
        services.TryAddTransient(_ => new RpcOutboundCallTracker());
        services.TryAddTransient(_ => new RpcRemoteObjectTracker());
        services.TryAddTransient(_ => new RpcSharedObjectTracker());
        services.TryAddSingleton(c => new RpcClientPeerReconnectDelayer(c));
        services.TryAddSingleton(_ => RpcLimits.Default);

        // Interceptors
        services.TryAddSingleton(_ => RpcClientInterceptor.Options.Default);
        services.TryAddTransient(c => new RpcClientInterceptor(c.GetRequiredService<RpcClientInterceptor.Options>(), c));
        services.TryAddSingleton(_ => RpcRoutingInterceptor.Options.Default);
        services.TryAddTransient(c => new RpcRoutingInterceptor(c.GetRequiredService<RpcRoutingInterceptor.Options>(), c));

        // System services
        if (!Configuration.Services.ContainsKey(typeof(IRpcSystemCalls))) {
            Service<IRpcSystemCalls>().HasServer<RpcSystemCalls>().HasName(RpcSystemCalls.Name);
            services.TryAddSingleton(c => new RpcSystemCalls(c));
            services.TryAddSingleton(c => new RpcSystemCallSender(c));
        }
    }

    // WebSocket client

    public RpcBuilder AddWebSocketClient(Uri hostUri)
        => AddWebSocketClient(_ => hostUri.ToString());

    public RpcBuilder AddWebSocketClient(string hostUrl)
        => AddWebSocketClient(_ => hostUrl);

    public RpcBuilder AddWebSocketClient(Func<IServiceProvider, string> hostUrlFactory)
        => AddWebSocketClient(c => RpcWebSocketClient.Options.Default with {
            HostUrlResolver = (_, _) => hostUrlFactory.Invoke(c),
        });

    public RpcBuilder AddWebSocketClient(Func<IServiceProvider, RpcWebSocketClient.Options>? optionsFactory = null)
    {
        var services = Services;
        services.AddSingleton(optionsFactory, _ => RpcWebSocketClient.Options.Default);
        if (services.HasService<RpcWebSocketClient>())
            return this;

        services.AddSingleton(c => new RpcWebSocketClient(
            c.GetRequiredService<RpcWebSocketClient.Options>(), c));
        services.AddAlias<RpcClient, RpcWebSocketClient>();
        return this;
    }

    // Share, Connect, Route

    public RpcBuilder AddService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService>
        (RpcServiceMode mode, Symbol name = default)
        where TService : class
        => AddService(typeof(TService), mode, name);
    public RpcBuilder AddService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TServer>
        (RpcServiceMode mode, Symbol name = default)
        where TService : class
        where TServer : class, TService
        => AddService(typeof(TService), typeof(TServer), mode, name);
    public RpcBuilder AddService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        RpcServiceMode mode, Symbol name = default)
        => AddService(serviceType, serviceType, mode, name);
    public RpcBuilder AddService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serverType,
        RpcServiceMode mode, Symbol name = default)
        => mode switch {
            RpcServiceMode.Server => AddServer(serviceType, serverType, name),
            RpcServiceMode.Router => AddRouter(serviceType, serverType, name),
            RpcServiceMode.ServingRouter => AddRouter(serviceType, serverType).AddServer(serviceType, name),
            RpcServiceMode.RoutingServer => AddRouter(serviceType, serverType).AddServer(serviceType, serverType, name),
            _ => Service(serverType).HasName(name).Rpc,
        };

    public RpcBuilder AddServer<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService>
        (Symbol name = default)
        where TService : class
        => AddServer(typeof(TService), name);
    public RpcBuilder AddServer<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TServer>
        (Symbol name = default)
        where TService : class
        where TServer : class, TService
        => AddServer(typeof(TService), typeof(TServer), name);
    public RpcBuilder AddServer(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        Symbol name = default)
        => AddServer(serviceType, serviceType, name);
    public RpcBuilder AddServer(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serverType,
        Symbol name = default)
    {
        if (!serviceType.IsInterface)
            throw ActualLab.Internal.Errors.MustBeInterface(serviceType, nameof(serviceType));
        if (!typeof(IRpcService).IsAssignableFrom(serviceType))
            throw ActualLab.Internal.Errors.MustImplement<IRpcService>(serviceType, nameof(serviceType));
        if (!serviceType.IsAssignableFrom(serverType))
            throw ActualLab.Internal.Errors.MustBeAssignableTo(serverType, serviceType, nameof(serverType));

        Service(serviceType).HasServer(serverType).HasName(name);
        if (!serverType.IsInterface)
            Services.AddSingleton(serverType);
        return this;
    }

    public RpcBuilder AddClient<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService>
        (Symbol name = default)
        where TService : class
        => AddClient(typeof(TService), name);
    public RpcBuilder AddClient<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TClient>
        (Symbol name = default)
        where TService : class
        where TClient : class, TService
        => AddClient(typeof(TService), typeof(TClient), name);
    public RpcBuilder AddClient(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        Symbol name = default)
        => AddClient(serviceType, serviceType, name);
    public RpcBuilder AddClient(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type clientType,
        Symbol name = default)
    {
        if (!serviceType.IsInterface)
            throw ActualLab.Internal.Errors.MustBeInterface(serviceType, nameof(serviceType));
        if (!typeof(IRpcService).IsAssignableFrom(serviceType))
            throw ActualLab.Internal.Errors.MustImplement<IRpcService>(serviceType, nameof(serviceType));
        if (!serviceType.IsAssignableFrom(clientType))
            throw ActualLab.Internal.Errors.MustBeAssignableTo(clientType, serviceType, nameof(clientType));

        Service(serviceType).HasName(name);
        Services.AddSingleton(clientType, c => RpcProxies.NewClientProxy(c, serviceType, clientType));
        return this;
    }

    public RpcBuilder AddRouter<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TServer>
        (Symbol name = default)
        where TService : class
        where TServer : class, TService
        => AddRouter(typeof(TService), typeof(TServer), name);
    public RpcBuilder AddRouter(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serverType,
        Symbol name = default)
        => serviceType == serverType
            ? throw new ArgumentOutOfRangeException(nameof(serverType))
            : AddRouter(serviceType, ServiceResolver.New(serverType), name);
    public RpcBuilder AddRouter(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        ServiceResolver serverResolver, Symbol name = default)
    {
        if (!serviceType.IsInterface)
            throw ActualLab.Internal.Errors.MustBeInterface(serviceType, nameof(serviceType));
        if (!typeof(IRpcService).IsAssignableFrom(serviceType))
            throw ActualLab.Internal.Errors.MustImplement<IRpcService>(serviceType, nameof(serviceType));

        Service(serviceType).HasName(name);
        Services.AddSingleton(serviceType, c => RpcProxies.NewRoutingProxy(c, serviceType, serverResolver));
        return this;
    }

    // More low-level configuration options stuff

    public RpcServiceBuilder Service<TService>()
        => Service(typeof(TService));

    public RpcServiceBuilder Service(Type serviceType)
    {
        if (Configuration.Services.TryGetValue(serviceType, out var service))
            return service;

        service = new RpcServiceBuilder(this, serviceType);
        Configuration.Services.Add(serviceType, service);
        return service;
    }

    public RpcBuilder AddInboundMiddleware<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TMiddleware>
        (Func<IServiceProvider, TMiddleware>? factory = null)
        where TMiddleware : RpcInboundMiddleware
        => AddInboundMiddleware(typeof(TMiddleware), factory);

    public RpcBuilder AddInboundMiddleware(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type middlewareType,
        Func<IServiceProvider, object>? factory = null)
    {
        if (!typeof(RpcInboundMiddleware).IsAssignableFrom(middlewareType))
            throw ActualLab.Internal.Errors.MustBeAssignableTo<RpcInboundMiddleware>(middlewareType, nameof(middlewareType));

        var descriptor = factory != null
            ? ServiceDescriptor.Singleton(typeof(RpcInboundMiddleware), factory)
            : ServiceDescriptor.Singleton(typeof(RpcInboundMiddleware), middlewareType);
        Services.TryAddEnumerable(descriptor);
        return this;
    }

    public RpcBuilder RemoveInboundMiddleware<TMiddleware>()
        where TMiddleware : RpcInboundMiddleware
        => RemoveInboundMiddleware(typeof(TMiddleware));

    public RpcBuilder RemoveInboundMiddleware(Type middlewareType)
    {
        if (!typeof(RpcInboundMiddleware).IsAssignableFrom(middlewareType))
            throw ActualLab.Internal.Errors.MustBeAssignableTo<RpcInboundMiddleware>(middlewareType, nameof(middlewareType));

        Services.RemoveAll(d =>
            d.ImplementationType == middlewareType
            && d.ServiceType == typeof(RpcInboundMiddleware));
        return this;
    }

    public RpcBuilder AddOutboundMiddleware<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TMiddleware>
        (Func<IServiceProvider, TMiddleware>? factory = null)
        where TMiddleware : RpcOutboundMiddleware
        => AddOutboundMiddleware(typeof(TMiddleware), factory);

    public RpcBuilder AddOutboundMiddleware(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type middlewareType,
        Func<IServiceProvider, object>? factory = null)
    {
        if (!typeof(RpcOutboundMiddleware).IsAssignableFrom(middlewareType))
            throw ActualLab.Internal.Errors.MustBeAssignableTo<RpcOutboundMiddleware>(middlewareType, nameof(middlewareType));

        var descriptor = factory != null
            ? ServiceDescriptor.Singleton(typeof(RpcOutboundMiddleware), factory)
            : ServiceDescriptor.Singleton(typeof(RpcOutboundMiddleware), middlewareType);
        Services.TryAddEnumerable(descriptor);
        return this;
    }

    public RpcBuilder RemoveOutboundMiddleware<TMiddleware>()
        where TMiddleware : RpcOutboundMiddleware
        => RemoveOutboundMiddleware(typeof(TMiddleware));

    public RpcBuilder RemoveOutboundMiddleware(Type middlewareType)
    {
        if (!typeof(RpcOutboundMiddleware).IsAssignableFrom(middlewareType))
            throw ActualLab.Internal.Errors.MustBeAssignableTo<RpcOutboundMiddleware>(middlewareType, nameof(middlewareType));

        Services.RemoveAll(d =>
            d.ImplementationType == middlewareType
            && d.ServiceType == typeof(RpcOutboundMiddleware));
        return this;
    }

    // Private methods

    private static RpcConfiguration? GetConfiguration(IServiceCollection services)
    {
        for (var i = 0; i < services.Count; i++) {
            var descriptor = services[i];
            if (descriptor.ServiceType == typeof(RpcConfiguration)) {
                if (i > 16) {
                    // Let's move it to the beginning of the list to speed up future lookups
                    services.RemoveAt(i);
                    services.Insert(0, descriptor);
                }
                return (RpcConfiguration?)descriptor.ImplementationInstance
                    ?? throw Errors.RpcOptionsMustBeRegisteredAsInstance();
            }
        }
        return null;
    }
}
