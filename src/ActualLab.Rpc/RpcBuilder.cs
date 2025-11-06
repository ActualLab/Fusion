using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ActualLab.Interception;
using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Clients;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization;
using ActualLab.Rpc.Testing;
using ActualLab.Rpc.Trimming;
using ActualLab.Trimming;

namespace ActualLab.Rpc;

public readonly struct RpcBuilder
{
    public IServiceCollection Services { get; }
    public RpcConfiguration Configuration { get; }
    public RpcServiceMode DefaultServiceMode { get; }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "CodeKeepers are used only to retain the code")]
    [UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "CodeKeepers are used only to retain the code")]
    static RpcBuilder() => CodeKeeper.AddFakeAction(
        static () => {
            CodeKeeper.KeepStatic(typeof(Proxies));
            CodeKeeper.KeepStatic(typeof(RpcDefaultDelegates));

            // Serializable types
            CodeKeeper.KeepSerializable<TypeRef>();

            // Interceptors
            CodeKeeper.Keep<RpcProxyCodeKeeper>();
            CodeKeeper.Keep<RpcRoutingInterceptor>();

            // Configuration
            CodeKeeper.Keep<RpcMethodDef>();
            CodeKeeper.Keep<RpcServiceDef>();
            CodeKeeper.Keep<RpcServiceRegistry>();
            CodeKeeper.Keep<RpcConfiguration>();
            CodeKeeper.Keep<RpcSerializationFormat>();
            CodeKeeper.Keep<RpcSerializationFormatResolver>();
            CodeKeeper.Keep<RpcByteArgumentSerializerV2>();
            CodeKeeper.Keep<RpcByteArgumentSerializerV1>();
            CodeKeeper.Keep<RpcByteMessageSerializerV4>();
            CodeKeeper.Keep<RpcDefaultCallTracer>();

            // Per-hub
            CodeKeeper.Keep<RpcHub>();
            CodeKeeper.Keep<RpcSystemCalls>();
            CodeKeeper.Keep<RpcInboundMiddlewares>();
            CodeKeeper.Keep<RpcOutboundMiddlewares>();
            CodeKeeper.Keep<RpcRandomDelayMiddleware>();

            // Per-peer
            CodeKeeper.Keep<RpcClientPeer>();
            CodeKeeper.Keep<RpcServerPeer>();
            CodeKeeper.Keep<RpcRemoteObjectTracker>();
            CodeKeeper.Keep<RpcSharedObjectTracker>();
            CodeKeeper.Keep<RpcSharedStream>();

            // Per-call
            CodeKeeper.Keep<RpcInboundContext>();
            CodeKeeper.Keep<RpcInboundContextFactory>();
            CodeKeeper.Keep<RpcOutboundContext>();
            CodeKeeper.Keep<RpcCacheInfoCapture>();
        });

    internal RpcBuilder(
        IServiceCollection services,
        Action<RpcBuilder>? configure,
        RpcServiceMode defaultServiceMode,
        bool saveDefaultServiceMode)
    {
        Services = services;
        if (services.FindInstance<RpcConfiguration>() is { } configuration) {
            DefaultServiceMode = defaultServiceMode.Or(configuration.DefaultServiceMode);
            if (saveDefaultServiceMode)
                configuration.DefaultServiceMode = DefaultServiceMode;

            // Already configured
            Configuration = configuration;
            configure?.Invoke(this);
            return;
        }

        DefaultServiceMode = defaultServiceMode.Or(RpcServiceMode.Server);
        Configuration = services.AddInstance(new RpcConfiguration());
        if (saveDefaultServiceMode)
            Configuration.DefaultServiceMode = DefaultServiceMode;

        // HostId & clocks
        services.AddSingleton(_ => new HostId());
        services.TryAddSingleton(_ => MomentClockSet.Default);
        services.AddSingleton(c => c.GetRequiredService<MomentClockSet>().SystemClock);

        // Core services
        services.AddSingleton(c => new RpcHub(c));
        services.AddSingleton(c => new RpcServiceRegistry(c));
        services.AddSingleton(_ => RpcSerializationFormatResolver.Default);
        services.AddSingleton(_ => RpcDefaultDelegates.ServiceDefBuilder);
        services.AddSingleton(_ => RpcDefaultDelegates.MethodDefBuilder);
        services.AddSingleton(_ => RpcDefaultDelegates.CallTimeoutsProvider);
        services.AddSingleton(_ => RpcDefaultDelegates.CallValidatorProvider);
        services.AddSingleton(_ => RpcDefaultDelegates.ServiceScopeResolver);
        services.AddSingleton(_ => RpcDefaultDelegates.InboundCallFilter);
        services.AddSingleton(_ => RpcDefaultDelegates.CallRouter);
        services.AddSingleton(_ => RpcDefaultDelegates.HashProvider);
        services.AddSingleton(_ => RpcDefaultDelegates.RerouteDelayer);
        services.AddSingleton(_ => RpcDefaultDelegates.PeerConnectionKindResolver);
        services.AddSingleton(_ => RpcDefaultDelegates.InboundContextFactory);
        services.AddSingleton(_ => RpcDefaultDelegates.PeerFactory);
        services.AddSingleton(_ => RpcDefaultDelegates.ServerConnectionFactory);
        services.AddSingleton(_ => RpcDefaultDelegates.WebSocketChannelOptionsProvider);
        services.AddSingleton(_ => RpcDefaultDelegates.ServerPeerCloseTimeoutProvider);
        services.AddSingleton(_ => RpcDefaultDelegates.PeerTerminalErrorDetector);
        services.AddSingleton(_ => RpcDefaultDelegates.CallTracerFactory);
        services.AddSingleton(_ => RpcDefaultDelegates.CallLoggerFactory);
        services.AddSingleton(_ => RpcDefaultDelegates.CallLoggerFilter);
        services.AddSingleton(c => new RpcSafeCallRouter(c));
        services.AddSingleton(c => new RpcInboundMiddlewares(c));
        services.AddSingleton(c => new RpcOutboundMiddlewares(c));
        services.AddTransient(_ => new RpcInboundCallTracker());
        services.AddTransient(_ => new RpcOutboundCallTracker());
        services.AddTransient(_ => new RpcRemoteObjectTracker());
        services.AddTransient(_ => new RpcSharedObjectTracker());
        services.AddSingleton(c => new RpcClientPeerReconnectDelayer(c));
        services.AddSingleton(_ => RpcLimits.Default);
        services.AddSingleton(_ => RpcRoutingInterceptor.Options.Default);

        // System services
        AddServerAndClient(typeof(IRpcSystemCalls), typeof(RpcSystemCalls), RpcSystemCalls.Name);
        services.AddSingleton(c => new RpcSystemCallSender(c));

        // And finally, invoke the configuration action
        configure?.Invoke(this);
    }

    // WithXxx

    public RpcBuilder WithServiceMode(
        RpcServiceMode serviceMode,
        bool makeDefault = false)
        => new(Services, null, serviceMode, makeDefault);

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

    public RpcBuilder AddService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService>
        (RpcServiceMode mode = default, string name = "")
        where TService : class, IRpcService
        => AddService(typeof(TService), mode, name);
    public RpcBuilder AddService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>
        (RpcServiceMode mode = default, string name = "")
        where TService : class, IRpcService
        where TImplementation : class, TService
        => AddService(typeof(TService), typeof(TImplementation), mode, name);
    public RpcBuilder AddService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        RpcServiceMode mode = default, string name = "")
        => AddService(serviceType, serviceType, mode, name);
    public RpcBuilder AddService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        RpcServiceMode mode = default, string name = "")
    {
        mode = mode.Or(DefaultServiceMode);
        return mode switch {
            RpcServiceMode.Local => AddLocalService(serviceType, implementationType),
            RpcServiceMode.Client => AddClient(serviceType, name),
            RpcServiceMode.Server => AddServer(serviceType, implementationType, name),
            RpcServiceMode.Distributed => AddDistributedService(serviceType, implementationType, name),
            RpcServiceMode.ServerAndClient => AddServerAndClient(serviceType, implementationType, name),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }

    public RpcBuilder AddLocalService<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService>()
        where TService : class, IRpcService
        => AddLocalService(typeof(TService));
    public RpcBuilder AddLocalService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>()
        where TService : class, IRpcService
        where TImplementation : class, TService
        => AddLocalService(typeof(TService), typeof(TImplementation));
    public RpcBuilder AddLocalService([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType)
        => AddLocalService(serviceType, serviceType);
    public RpcBuilder AddLocalService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType)
    {
        // DI container:
        // - TImplementation is a singleton
        // - IService as its alias, if IService != TImplementation
        // RPC:
        // - no configuration changes

        if (!serviceType.IsAssignableFrom(implementationType))
            throw ActualLab.Internal.Errors.MustBeAssignableTo(implementationType, serviceType, nameof(implementationType));
        if (!implementationType.IsClass)
            throw ActualLab.Internal.Errors.MustBeClass(implementationType, nameof(implementationType));

        Services.AddSingleton(implementationType);
        if (serviceType != implementationType)
            Services.AddAlias(serviceType, implementationType);
        return this;
    }

    public RpcBuilder AddClient<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService>
        (string name = "")
        where TService : class, IRpcService
        => AddClient(typeof(TService), name);
    public RpcBuilder AddClient(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        string name = "")
    {
        Service(serviceType).HasName(name).IsClient().Inject();
        return this;
    }

    public RpcBuilder AddServer<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService>
        (string name = "")
        where TService : class, IRpcService
        => AddServer(typeof(TService), name);
    public RpcBuilder AddServer<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>
        (string name = "")
        where TService : class, IRpcService
        where TImplementation : class, TService
        => AddServer(typeof(TService), typeof(TImplementation), name);
    public RpcBuilder AddServer(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        string name = "")
        => AddServer(serviceType, serviceType, name);
    public RpcBuilder AddServer(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        string name = "")
    {
        Service(serviceType).HasName(name).IsServer(implementationType).Inject();
        return this;
    }

    public RpcBuilder AddDistributedService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>
        (string name = "")
        where TService : class, IRpcService
        where TImplementation : class, TService
        => AddDistributedService(typeof(TService), typeof(TImplementation), name);
    public RpcBuilder AddDistributedService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        string name = "")
    {
        Service(serviceType).HasName(name).IsDistributed(implementationType).Inject();
        return this;
    }

    public RpcBuilder AddServerAndClient<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>
        (string name = "")
        where TService : class, IRpcService
        where TImplementation : class, TService
        => AddServerAndClient(typeof(TService), typeof(TImplementation), name);
    public RpcBuilder AddServerAndClient(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        string name = "")
    {
        Service(serviceType).HasName(name).IsServer(implementationType).HasClient().Inject();
        return this;
    }

    // More low-level configuration options stuff

    public RpcServiceBuilder Service<TService>(bool tryGetExisting = false)
        => Service(typeof(TService), tryGetExisting);

    public RpcServiceBuilder Service(Type serviceType, bool tryGetExisting = false)
    {
        if (tryGetExisting && Configuration.Services.TryGetValue(serviceType, out var service))
            return service;

        service = new RpcServiceBuilder(this, serviceType);
        Configuration.Services[serviceType] = service;
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

        var descriptor = factory is not null
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

        var descriptor = factory is not null
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
