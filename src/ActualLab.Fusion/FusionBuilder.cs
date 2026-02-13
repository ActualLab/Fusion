using Microsoft.Extensions.DependencyInjection.Extensions;
using ActualLab.Conversion;
using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.Interception;
using ActualLab.Fusion.Internal;
using ActualLab.Fusion.Operations.Internal;
using ActualLab.Fusion.Operations.Reprocessing;
using ActualLab.Fusion.Client.Interception;
using ActualLab.Fusion.Client.Internal;
using ActualLab.Fusion.Rpc;
using ActualLab.Fusion.Trimming;
using ActualLab.Fusion.UI;
using ActualLab.Resilience;
using ActualLab.Rpc;
using ActualLab.Trimming;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Fusion;

/// <summary>
/// A builder for registering Fusion services, compute services, and related infrastructure
/// in an <see cref="IServiceCollection"/>.
/// </summary>
public readonly struct FusionBuilder
{
    public IServiceCollection Services { get; }
    public CommanderBuilder Commander { get; }
    public RpcBuilder Rpc { get; }
    public RpcServiceMode DefaultServiceMode { get; }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "CodeKeepers are used only to retain the code")]
    [UnconditionalSuppressMessage("Trimming", "IL2110", Justification = "CodeKeepers are used only to retain the code")]
    [UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "CodeKeepers are used only to retain the code")]
    static FusionBuilder()
    {
        FusionModuleInitializer.Touch();
        CodeKeeper.AddFakeAction(static () => {
            CodeKeeper.Keep<CommanderBuilder>();
            CodeKeeper.Keep<RpcBuilder>();

            // Interceptors
            CodeKeeper.Keep<FusionProxyCodeKeeper>();
            CodeKeeper.Keep<ComputeServiceInterceptor>();
            CodeKeeper.Keep<RemoteComputeServiceInterceptor>();

            // Other services
            CodeKeeper.Keep<RpcComputeSystemCalls>();
        });
    }

    internal FusionBuilder(
        IServiceCollection services,
        Action<FusionBuilder>? configure,
        RpcServiceMode defaultServiceMode,
        bool saveDefaultServiceMode)
    {
        Services = services;
        Commander = services.AddCommander();
        if (services.FindInstance<FusionTag>() is { } fusionTag) {
            DefaultServiceMode = defaultServiceMode.Or(fusionTag.DefaultServiceMode);
            Rpc = services.AddRpc(DefaultServiceMode);
            if (saveDefaultServiceMode)
                fusionTag.DefaultServiceMode = DefaultServiceMode;

            configure?.Invoke(this);
            return;
        }

        DefaultServiceMode = defaultServiceMode.Or(RpcServiceMode.Local);
        Rpc = services.AddRpc(DefaultServiceMode);
        fusionTag = services.AddInstance(new FusionTag(), addInFront: true);
        if (saveDefaultServiceMode)
            fusionTag.DefaultServiceMode = DefaultServiceMode;

        // Common services
        services.AddOptions();
        services.AddConverters();
        services.TryAddSingleton(_ => TransiencyResolvers.PreferTransient.ForContext<Computed>());
        services.AddSingleton(c => new FusionHub(c));
        services.AddSingleton(c => new ComputedOptionsProvider(c));
        services.AddSingleton(_ => FusionDefaultDelegates.ComputedOutputEqualityComparer);

        // Interceptors
        services.AddSingleton(_ => ComputeServiceInterceptor.Options.Default);
        services.AddSingleton(_ => RemoteComputeServiceInterceptor.Options.Default);
        services.AddSingleton(c => new ComputeServiceInterceptor(
            c.GetRequiredService<ComputeServiceInterceptor.Options>(),
            c.FusionHub()));

        // StateFactory
        services.AddScopedOrSingleton((c, isScoped) => new StateFactory(c, isScoped));

        // Update delayer & UI action tracker
        services.AddSingleton(_ => new UIActionTracker.Options());
        services.AddScoped<UIActionTracker>(c => new UIActionTracker(
            c.GetRequiredService<UIActionTracker.Options>(), c));
        services.AddScoped<IUpdateDelayer>(c => new UpdateDelayer(c.UIActionTracker()));

        // CommandR, command completion and invalidation
        var commander = Commander;

        // Transient operation scope and its provider
        services.AddSingleton(c => new InMemoryOperationScopeProvider(c));
        commander.AddHandlers<InMemoryOperationScopeProvider>();

        // Nested command logger
        services.AddSingleton(c => new NestedOperationLogger(c));
        commander.AddHandlers<NestedOperationLogger>();

        // Operation completion - notifier & producer
        services.AddSingleton(_ => new OperationCompletionNotifier.Options());
        services.AddSingleton<IOperationCompletionNotifier>(c => new OperationCompletionNotifier(
            c.GetRequiredService<OperationCompletionNotifier.Options>(), c));
        services.AddSingleton(_ => new CompletionProducer.Options());
        services.TryAddEnumerable(ServiceDescriptor.Singleton(
            typeof(IOperationCompletionListener),
            typeof(CompletionProducer)));

        // Command completion handler performing invalidations
        services.AddSingleton(_ => new InvalidatingCommandCompletionHandler.Options());
        services.AddSingleton(c => new InvalidatingCommandCompletionHandler(
            c.GetRequiredService<InvalidatingCommandCompletionHandler.Options>(), c));
        commander.AddHandlers<InvalidatingCommandCompletionHandler>();

        // Completion terminator
        services.AddSingleton(_ => new CompletionTerminator());
        commander.AddHandlers<CompletionTerminator>();

        // Core authentication services
        services.AddScoped<ISessionResolver>(c => new SessionResolver(c));
        services.AddScoped(c => c.GetRequiredService<ISessionResolver>().Session);

        // RPC:
        // 1. Register IRpcComputeSystemCalls service and RpcComputeCallType
        Rpc.AddServerAndClient(typeof(IRpcComputeSystemCalls), typeof(RpcComputeSystemCalls), RpcComputeSystemCalls.Name);
        services.AddSingleton(c => new RpcComputeSystemCallSender(c));
        // 2. Add middlewares
        Rpc.AddMiddleware(_ => new RpcInboundComputeCallHandler());

        // And finally, invoke the configuration action
        configure?.Invoke(this);
    }

    internal FusionBuilder(FusionBuilder fusion, RpcServiceMode defaultServiceMode, bool setDefaultServiceMode)
    {
        Services = fusion.Services;
        Commander = fusion.Commander;
        Rpc = fusion.Rpc;
        DefaultServiceMode = defaultServiceMode;
        if (!setDefaultServiceMode)
            return;

        if (Services.FindInstance<FusionTag>() is not { } fusionTag)
            throw Errors.InternalError("Something is off: FusionTag service must be registered at this point.");

        DefaultServiceMode = defaultServiceMode.Or(fusionTag.DefaultServiceMode);
        fusionTag.DefaultServiceMode = DefaultServiceMode;
    }

    // WithServiceMode

    public FusionBuilder WithServiceMode(
        RpcServiceMode serviceMode,
        bool makeDefault = false)
        => new(this, serviceMode, makeDefault);

    // AddXxx - Service, Client, ComputeService, Server, DistributedService, DistributedServicePair

    public FusionBuilder AddService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService>(
        RpcServiceMode mode = RpcServiceMode.Default,
        bool hasCommandHandlers = true)
        where TService : class, IComputeService
        => AddService(typeof(TService), typeof(TService), ServiceLifetime.Singleton, mode, hasCommandHandlers);
    public FusionBuilder AddService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>(
        RpcServiceMode mode = RpcServiceMode.Default,
        bool hasCommandHandlers = true)
        where TService : class
        where TImplementation : class, TService, IComputeService
        => AddService(typeof(TService), typeof(TImplementation), ServiceLifetime.Singleton, mode, hasCommandHandlers);
    public FusionBuilder AddService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService>(
        ServiceLifetime lifetime,
        RpcServiceMode mode = RpcServiceMode.Default,
        bool hasCommandHandlers = true)
        where TService : class, IComputeService
        => AddService(typeof(TService), typeof(TService), lifetime, mode, hasCommandHandlers);
    public FusionBuilder AddService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>(
        ServiceLifetime lifetime,
        RpcServiceMode mode = RpcServiceMode.Default,
        bool hasCommandHandlers = true)
        where TService : class
        where TImplementation : class, TService, IComputeService
        => AddService(typeof(TService), typeof(TImplementation), lifetime, mode, hasCommandHandlers);

    public FusionBuilder AddService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        RpcServiceMode mode = RpcServiceMode.Default,
        bool hasCommandHandlers = true)
        => AddService(serviceType, serviceType, ServiceLifetime.Singleton, mode, hasCommandHandlers);
    public FusionBuilder AddService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        RpcServiceMode mode = RpcServiceMode.Default,
        bool hasCommandHandlers = true)
        => AddService(serviceType, implementationType, ServiceLifetime.Singleton, mode, hasCommandHandlers);
    public FusionBuilder AddService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        ServiceLifetime lifetime,
        RpcServiceMode mode = RpcServiceMode.Default,
        bool hasCommandHandlers = true)
        => AddService(serviceType, serviceType, lifetime, mode, hasCommandHandlers);
    public FusionBuilder AddService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        ServiceLifetime lifetime,
        RpcServiceMode mode = RpcServiceMode.Default,
        bool hasCommandHandlers = true)
    {
        if (lifetime != ServiceLifetime.Singleton) {
            var scopedServiceMode = mode.Or(RpcServiceMode.Local);
            if (scopedServiceMode is not RpcServiceMode.Local)
                throw new ArgumentOutOfRangeException(nameof(mode));

            return AddComputeService(serviceType, implementationType, lifetime);
        }

        mode = mode.Or(DefaultServiceMode);
        return mode switch {
            RpcServiceMode.Local => AddComputeService(serviceType, implementationType, hasCommandHandlers),
            RpcServiceMode.Client => AddClient(serviceType, "", hasCommandHandlers),
            RpcServiceMode.Server => AddServer(serviceType, implementationType, "", hasCommandHandlers),
            RpcServiceMode.Distributed => AddDistributedService(serviceType, implementationType, "", hasCommandHandlers),
            RpcServiceMode.ServerAndClient => AddServerAndClient(serviceType, implementationType, "", hasCommandHandlers),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }

    public FusionBuilder AddClient<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService>
        (string name = "", bool hasCommandHandlers = true)
        where TService : class, IComputeService
        => AddClient(typeof(TService), name, hasCommandHandlers);
    public FusionBuilder AddClient(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        string name = "", bool hasCommandHandlers = true)
    {
        // ~ RpcBuilder.AddClient, but for Compute Service

        Configure(serviceType, tryGetExisting: false)
            .HasName(name)
            .IsClient()
            .HasCommandHandlers(hasCommandHandlers)
            .Inject();
        return this;
    }

    public FusionBuilder AddComputeService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService>
        (bool hasCommandHandlers = true)
        where TService : class, IComputeService
        => AddComputeService(typeof(TService), typeof(TService), ServiceLifetime.Singleton, hasCommandHandlers);
    public FusionBuilder AddComputeService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService>
        (ServiceLifetime lifetime, bool hasCommandHandlers = true)
        where TService : class, IComputeService
        => AddComputeService(typeof(TService), typeof(TService), lifetime, hasCommandHandlers);
    public FusionBuilder AddComputeService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>
        (bool hasCommandHandlers = true)
        where TService : class, IComputeService
        where TImplementation : class, TService
        => AddComputeService(typeof(TService), typeof(TImplementation), ServiceLifetime.Singleton, hasCommandHandlers);
    public FusionBuilder AddComputeService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>
        (ServiceLifetime lifetime, bool hasCommandHandlers = true)
        where TService : class, IComputeService
        where TImplementation : class, TService
        => AddComputeService(typeof(TService), typeof(TImplementation), lifetime, hasCommandHandlers);

    public FusionBuilder AddComputeService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        bool hasCommandHandlers = true)
        => AddComputeService(serviceType, serviceType, ServiceLifetime.Singleton, hasCommandHandlers);
    public FusionBuilder AddComputeService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        ServiceLifetime lifetime, bool hasCommandHandlers = true)
        => AddComputeService(serviceType, serviceType, lifetime, hasCommandHandlers);
    public FusionBuilder AddComputeService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        bool hasCommandHandlers = true)
        => AddComputeService(serviceType, implementationType, ServiceLifetime.Singleton, hasCommandHandlers);
    public FusionBuilder AddComputeService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        ServiceLifetime lifetime, bool hasCommandHandlers = true)
    {
        // ~ RpcBuilder.AddLocalService, but for Compute Service

        if (lifetime is ServiceLifetime.Singleton) {
            Configure(serviceType, tryGetExisting: false)
                .IsLocal(implementationType)
                .HasCommandHandlers(hasCommandHandlers)
                .Inject();
            return this;
        }

        if (!typeof(IComputeService).IsAssignableFrom(implementationType))
            throw Errors.MustImplement<IComputeService>(implementationType, nameof(serviceType));
        if (!serviceType.IsAssignableFrom(implementationType))
            throw Errors.MustBeAssignableTo(implementationType, serviceType, nameof(implementationType));
        if (!implementationType.IsClass)
            throw Errors.MustBeClass(implementationType, nameof(implementationType));
        if (!typeof(IHasDisposeStatus).IsAssignableFrom(implementationType))
            throw Errors.MustImplement<IHasDisposeStatus>(implementationType, nameof(implementationType));

        object CreateComputeService(IServiceProvider c)
            => c.FusionHub().NewComputeServiceProxy(c, implementationType);

        Services.Add(new ServiceDescriptor(serviceType, CreateComputeService, lifetime));
        if (hasCommandHandlers)
            Commander.AddHandlers(serviceType, implementationType);
        return this;

    }

    public FusionBuilder AddServer<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>
        (string name = "", bool hasCommandHandlers = true)
        where TService : class, IComputeService
        where TImplementation : class, TService
        => AddServer(typeof(TService), typeof(TImplementation), name, hasCommandHandlers);
    public FusionBuilder AddServer(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        string name = "", bool hasCommandHandlers = true)
    {
        // ~ RpcBuilder.AddServer, but for Compute Service

        Configure(serviceType, tryGetExisting: false)
            .HasName(name)
            .IsServer(implementationType)
            .HasCommandHandlers(hasCommandHandlers)
            .Inject();
        return this;
    }

    public FusionBuilder AddDistributedService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>
        (string name = "", bool hasCommandHandlers = true)
        where TService : class, IComputeService
        where TImplementation : class, TService
        => AddDistributedService(typeof(TService), typeof(TImplementation), name, hasCommandHandlers);
    public FusionBuilder AddDistributedService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        string name = "", bool hasCommandHandlers = true)
    {
        // ~ RpcBuilder.AddDistributedService, but for Compute Service

        Configure(serviceType, tryGetExisting: false)
            .HasName(name)
            .IsDistributed(implementationType)
            .HasCommandHandlers(hasCommandHandlers)
            .Inject();
        return this;
    }

    public FusionBuilder AddServerAndClient<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>
        (string name = "", bool hasCommandHandlers = true)
        where TService : class, IComputeService
        where TImplementation : class, TService
        => AddServerAndClient(typeof(TService), typeof(TImplementation), name, hasCommandHandlers);
    public FusionBuilder AddServerAndClient(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        string name = "", bool hasCommandHandlers = true)
    {
        // ~ RpcBuilder.AddDistributedService, but for Compute Service

        Configure(serviceType, tryGetExisting: false)
            .HasName(name)
            .IsServer(implementationType)
            .HasClient()
            .HasCommandHandlers(hasCommandHandlers)
            .Inject();
        return this;
    }

    // More low-level configuration options stuff

    public FusionRpcServiceBuilder Configure<TService>(bool tryGetExisting = true)
        => Configure(typeof(TService), tryGetExisting);

    public FusionRpcServiceBuilder Configure(Type serviceType, bool tryGetExisting = true)
    {
        var rpcServices = Rpc.Configuration.Services;
        if (tryGetExisting
            && rpcServices.TryGetValue(serviceType, out var rpcService)
            && rpcService is FusionRpcServiceBuilder fusionRpcService)
            return fusionRpcService;

        fusionRpcService = new FusionRpcServiceBuilder(this, serviceType);
        rpcServices[serviceType] = fusionRpcService;
        return fusionRpcService;
    }

    // AddOperationReprocessor

    public FusionBuilder AddOperationReprocessor(
        Func<IServiceProvider, OperationReprocessor.Options>? optionsFactory = null)
        => AddOperationReprocessor<OperationReprocessor>(optionsFactory);

    public FusionBuilder AddOperationReprocessor<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TOperationReprocessor>(
        Func<IServiceProvider, OperationReprocessor.Options>? optionsFactory = null)
        where TOperationReprocessor : class, IOperationReprocessor
    {
        var services = Services;
        services.AddSingleton(optionsFactory, _ => OperationReprocessor.Options.Default);
        if (services.HasService<TOperationReprocessor>())
            return this;

        services.AddTransient<TOperationReprocessor>();
        services.AddAlias<IOperationReprocessor, TOperationReprocessor>(ServiceLifetime.Transient);
        Commander.AddHandlers<TOperationReprocessor>();
        services.AddSingleton(TransiencyResolvers.PreferNonTransient.ForContext<IOperationReprocessor>());
        return this;
    }

    // AddClientComputeCache

    public FusionBuilder AddRemoteComputedCache<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCache, TOptions>(
        Func<IServiceProvider, TOptions> optionsFactory)
        where TCache : class, IRemoteComputedCache
        where TOptions : class
    {
        var services = Services;
        services.AddSingleton(optionsFactory);
        if (services.HasService<TCache>())
            return this;

        services.AddSingleton<TCache>();
        services.AddAlias<IRemoteComputedCache, TCache>();
        return this;
    }

    public FusionBuilder AddSharedRemoteComputedCache<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCache, TOptions>(
        Func<IServiceProvider, TOptions> optionsFactory)
        where TCache : RemoteComputedCache
        where TOptions : class
    {
        var services = Services;
        services.AddSingleton(optionsFactory);
        if (services.HasService<TCache>())
            return this;

        services.AddSingleton<TCache>();
        services.AddSingleton(c => new SharedRemoteComputedCache(c.GetRequiredService<TCache>));
        services.AddAlias<IRemoteComputedCache, SharedRemoteComputedCache>();
        return this;
    }

    public FusionBuilder AddInMemoryRemoteComputedCache(
        Func<IServiceProvider, InMemoryRemoteComputedCache.Options>? optionsFactory = null)
        => AddRemoteComputedCache<InMemoryRemoteComputedCache, InMemoryRemoteComputedCache.Options>(
            optionsFactory ?? (_ => InMemoryRemoteComputedCache.Options.Default));

    // Nested types

    /// <summary>
    /// A marker service instance stored in <see cref="IServiceCollection"/>
    /// to track the default <see cref="RpcServiceMode"/> for Fusion.
    /// </summary>
    public class FusionTag
    {
        public RpcServiceMode DefaultServiceMode {
            get;
            set => field = value.Or(RpcServiceMode.Local);
        } = RpcServiceMode.Local;
    }
}
