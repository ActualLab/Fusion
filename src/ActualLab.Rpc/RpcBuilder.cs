using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ActualLab.Interception;
using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Clients;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc;

public readonly struct RpcBuilder
{
    public IServiceCollection Services { get; }
    public RpcConfiguration Configuration { get; }

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Proxies))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcDefaultDelegates))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcMethodDef))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcServiceDef))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcServiceRegistry))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcConfiguration))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcByteArgumentSerializer))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcDefaultCallTracer))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcRoutingInterceptor))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcSwitchInterceptor))]
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
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcSystemCalls))]
    internal RpcBuilder(
        IServiceCollection services,
        Action<RpcBuilder>? configure)
    {
        Services = services;
        if (services.FindInstance<RpcConfiguration>() is { } configuration) {
            // Already configured
            Configuration = configuration;
            configure?.Invoke(this);
            return;
        }

        Configuration = services.AddInstance(new RpcConfiguration());

        // HostId & clocks
        services.AddSingleton(_ => new HostId());
        services.TryAddSingleton(_ => MomentClockSet.Default);
        services.AddSingleton(c => c.GetRequiredService<MomentClockSet>().SystemClock);

        // Core services
        services.AddSingleton(c => new RpcHub(c));
        services.AddSingleton(c => new RpcServiceRegistry(c));
        services.AddSingleton(_ => RpcDefaultDelegates.ServiceDefBuilder);
        services.AddSingleton(_ => RpcDefaultDelegates.MethodDefBuilder);
        services.AddSingleton(_ => RpcDefaultDelegates.BackendServiceDetector);
        services.AddSingleton(_ => RpcDefaultDelegates.CommandTypeDetector);
        services.AddSingleton(_ => RpcDefaultDelegates.CallTimeoutsProvider);
        services.AddSingleton(_ => RpcDefaultDelegates.ServiceScopeResolver);
        services.AddSingleton(_ => RpcDefaultDelegates.InboundCallFilter);
        services.AddSingleton(_ => RpcDefaultDelegates.CallRouter);
        services.AddSingleton(_ => RpcDefaultDelegates.HashProvider);
        services.AddSingleton(_ => RpcDefaultDelegates.RerouteDelayer);
        services.AddSingleton(_ => RpcDefaultDelegates.InboundContextFactory);
        services.AddSingleton(_ => RpcDefaultDelegates.PeerFactory);
        services.AddSingleton(_ => RpcDefaultDelegates.ClientConnectionFactory);
        services.AddSingleton(_ => RpcDefaultDelegates.ServerConnectionFactory);
        services.AddSingleton(_ => RpcDefaultDelegates.PeerTerminalErrorDetector);
        services.AddSingleton(_ => RpcDefaultDelegates.CallTracerFactory);
        services.AddSingleton(_ => RpcDefaultDelegates.CallLoggerFactory);
        services.AddSingleton(_ => RpcDefaultDelegates.CallLoggerFilter);
        services.AddSingleton(_ => RpcArgumentSerializer.Default);
        services.AddSingleton(c => new RpcSafeCallRouter(c));
        services.AddSingleton(c => new RpcInboundMiddlewares(c));
        services.AddSingleton(c => new RpcOutboundMiddlewares(c));
        services.AddTransient(_ => new RpcInboundCallTracker());
        services.AddTransient(_ => new RpcOutboundCallTracker());
        services.AddTransient(_ => new RpcRemoteObjectTracker());
        services.AddTransient(_ => new RpcSharedObjectTracker());
        services.AddSingleton(c => new RpcClientPeerReconnectDelayer(c));
        services.AddSingleton(_ => RpcLimits.Default);

        // Interceptor options (the instances are created by RpcProxies)
        services.AddSingleton(_ => RpcNonRoutingInterceptor.Options.Default);
        services.AddSingleton(_ => RpcRoutingInterceptor.Options.Default);
        services.AddSingleton(_ => RpcSwitchInterceptor.Options.Default);

        // System services
        if (!Configuration.Services.ContainsKey(typeof(IRpcSystemCalls))) {
            Service<IRpcSystemCalls>().HasServer<RpcSystemCalls>().HasName(RpcSystemCalls.Name);
            services.AddSingleton(c => new RpcSystemCalls(c));
            services.AddSingleton(c => new RpcSystemCallSender(c));
        }
    }

    // WebSocket client

    public RpcBuilder AddWebSocketClient(Uri hostUri)
        => AddWebSocketClient(_ => hostUri.ToString());

    public RpcBuilder AddWebSocketClient(string hostUrl)
        => AddWebSocketClient(_ => hostUrl);

    public RpcBuilder AddWebSocketClient(Func<IServiceProvider, string> hostUrlResolver)
        => AddWebSocketClient(c => RpcWebSocketClient.Options.Default with {
            HostUrlResolver = (_, _) => hostUrlResolver.Invoke(c),
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

    // AddService & its specific variants

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public RpcBuilder AddService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService>
        (RpcServiceMode mode, Symbol name = default)
        where TService : class
        => AddService(typeof(TService), mode, name);
    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public RpcBuilder AddService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>
        (RpcServiceMode mode, Symbol name = default)
        where TService : class
        where TImplementation : class, TService
        => AddService(typeof(TService), typeof(TImplementation), mode, name);
    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public RpcBuilder AddService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        RpcServiceMode mode, Symbol name = default)
        => AddService(serviceType, serviceType, mode, name);
    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public RpcBuilder AddService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        RpcServiceMode mode, Symbol name = default)
        => mode switch {
            RpcServiceMode.Local => AddLocal(serviceType, implementationType),
            RpcServiceMode.Client => AddClient(serviceType, name),
            RpcServiceMode.Server => AddServer(serviceType, implementationType, name),
            RpcServiceMode.Distributed => AddDistributed(serviceType, implementationType, name),
            RpcServiceMode.DistributedPair => AddDistributedPair(serviceType, implementationType, name),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public RpcBuilder AddClient<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService>
        (Symbol name = default)
        where TService : class
        => AddClient(typeof(TService), typeof(TService), name);
    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public RpcBuilder AddClient<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TProxyBase>
        (Symbol name = default)
        where TService : class
        where TProxyBase : class, TService
        => AddClient(typeof(TService), typeof(TProxyBase), name);
    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public RpcBuilder AddClient(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        Symbol name = default)
        => AddClient(serviceType, serviceType, name);
    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public RpcBuilder AddClient(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type proxyBaseType,
        Symbol name = default)
    {
        // DI container:
        // - TProxyBaseType is a singleton RPC client for TService
        // - IService as its alias, if TProxyBaseType != TService
        // RPC:
        // - TService configured as client

        if (!typeof(IRpcService).IsAssignableFrom(serviceType))
            throw ActualLab.Internal.Errors.MustImplement<IRpcService>(serviceType, nameof(serviceType));
        if (!serviceType.IsAssignableFrom(proxyBaseType))
            throw ActualLab.Internal.Errors.MustBeAssignableTo(proxyBaseType, serviceType, nameof(proxyBaseType));

        if (serviceType == proxyBaseType)
            Services.AddSingleton(serviceType,
                c => c.RpcHub().InternalServices.NewRoutingProxy(serviceType, proxyBaseType));
        else {
            Services.AddSingleton(proxyBaseType,
                c => c.RpcHub().InternalServices.NewRoutingProxy(serviceType, proxyBaseType));
            Services.AddAlias(serviceType, proxyBaseType);
        }
        Service(serviceType).HasName(name);
        return this;
    }

    public RpcBuilder AddLocal<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService>()
        where TService : class
        => AddLocal(typeof(TService));
    public RpcBuilder AddLocal<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>()
        where TService : class
        where TImplementation : class, TService
        => AddLocal(typeof(TService), typeof(TImplementation));
    public RpcBuilder AddLocal([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType)
        => AddLocal(serviceType, serviceType);
    public RpcBuilder AddLocal(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType)
    {
        // DI container:
        // - TImplementation is a singleton
        // - IService as its alias, if IService != TImplementation
        // RPC:
        // - no configuration changes

        if (!typeof(IRpcService).IsAssignableFrom(serviceType))
            throw ActualLab.Internal.Errors.MustImplement<IRpcService>(serviceType, nameof(serviceType));
        if (!serviceType.IsAssignableFrom(implementationType))
            throw ActualLab.Internal.Errors.MustBeAssignableTo(implementationType, serviceType, nameof(implementationType));
        if (!implementationType.IsClass)
            throw ActualLab.Internal.Errors.MustBeClass(implementationType, nameof(implementationType));

        Services.Add(new ServiceDescriptor(implementationType, implementationType, ServiceLifetime.Singleton));
        if (serviceType != implementationType)
            Services.AddAlias(serviceType, implementationType);
        return this;
    }

    public RpcBuilder AddServer<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService>
        (Symbol name = default)
        where TService : class
        => AddServer(typeof(TService), name);
    public RpcBuilder AddServer<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>
        (Symbol name = default)
        where TService : class
        where TImplementation : class, TService
        => AddServer(typeof(TService), typeof(TImplementation), name);
    public RpcBuilder AddServer(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        Symbol name = default)
        => AddServer(serviceType, serviceType, name);
    public RpcBuilder AddServer(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        Symbol name = default)
    {
        // DI container:
        // - TImplementation is a singleton
        // - IService as its alias, if IService != TImplementation
        // RPC:
        // - TService configured as server resolving to TImplementation

        AddLocal(serviceType, implementationType);
        Service(serviceType).HasServer(implementationType).HasName(name);
        return this;
    }

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public RpcBuilder AddDistributed<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>
        (Symbol name = default)
        where TService : class
        where TImplementation : class, TService
        => AddDistributed(typeof(TService), typeof(TImplementation), name);
    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public RpcBuilder AddDistributed(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        Symbol name = default)
    {
        // DI container:
        // - TService is a singleton mapped to a hybrid proxy extending TImplementation,
        //   which routes calls to:
        //   - either its own (TImplementation) methods,
        //   - or its internal TService client.
        // RPC:
        // - TService is configured as server resolving to TService, i.e. it routes incoming calls

        if (!typeof(IRpcService).IsAssignableFrom(serviceType))
            throw ActualLab.Internal.Errors.MustImplement<IRpcService>(serviceType, nameof(serviceType));
        if (!serviceType.IsAssignableFrom(implementationType))
            throw ActualLab.Internal.Errors.MustBeAssignableTo(implementationType, serviceType, nameof(implementationType));
        if (!implementationType.IsClass)
            throw ActualLab.Internal.Errors.MustBeClass(implementationType, nameof(implementationType));

        Services.AddSingleton(serviceType,
            c => c.RpcHub().InternalServices.NewRoutingProxy(serviceType, implementationType));
        Service(serviceType).HasServer(serviceType).HasName(name);
        return this;
    }

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public RpcBuilder AddDistributedPair<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>
        (Symbol name = default)
        where TService : class
        where TImplementation : class, TService
        => AddDistributedPair(typeof(TService), typeof(TImplementation), name);
    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public RpcBuilder AddDistributedPair(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        Symbol name = default)
    {
        // DI container:
        // - TImplementation is a singleton
        // - TService is a switch proxy singleton routing calls to:
        //   - either TImplementation singleton,
        //   - or its internal TService client.
        // RPC:
        // - TService configured as server resolving to TImplementation, so incoming calls won't be routed

        AddLocal(implementationType);
        Services.AddSingleton(serviceType, c => {
            var hub = c.RpcHub();
            var localTarget = c.GetRequiredService(implementationType);
            var remoteTarget = hub.InternalServices.NewNonRoutingInterceptor(serviceType);
            return c.RpcHub().InternalServices.NewSwitchProxy(serviceType, serviceType, localTarget, remoteTarget);
        });
        Service(serviceType).HasServer(implementationType).HasName(name);
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
}
