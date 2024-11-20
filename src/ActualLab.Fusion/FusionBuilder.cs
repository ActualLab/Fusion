using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ActualLab.Conversion;
using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.Interception;
using ActualLab.Fusion.Internal;
using ActualLab.Fusion.Operations.Internal;
using ActualLab.Fusion.Operations.Reprocessing;
using ActualLab.Fusion.Client.Interception;
using ActualLab.Fusion.Client.Internal;
using ActualLab.Fusion.Trimming;
using ActualLab.Fusion.UI;
using ActualLab.Resilience;
using ActualLab.Rpc;
using ActualLab.Trimming;
using Errors = ActualLab.Internal.Errors;
using UnreferencedCode = ActualLab.Fusion.Internal.UnreferencedCode;

namespace ActualLab.Fusion;

public readonly struct FusionBuilder
{
    public IServiceCollection Services { get; }
    public CommanderBuilder Commander { get; }
    public RpcBuilder Rpc { get; }
    public RpcServiceMode DefaultServiceMode { get; }

    static FusionBuilder() => CodeKeeper.AddFakeAction(
        static () => {
            CodeKeeper.Keep<CommanderBuilder>();
            CodeKeeper.Keep<RpcBuilder>();

            // Interceptors
            CodeKeeper.Keep<FusionProxyCodeKeeper>();
            CodeKeeper.Keep<ComputeServiceInterceptor>();
            CodeKeeper.Keep<RemoteComputeServiceInterceptor>();

            // Other services
            CodeKeeper.Keep<RpcComputeSystemCalls>();
        });

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
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

        // Interceptors
        services.AddSingleton(_ => ComputeServiceInterceptor.Options.Default);
        services.AddSingleton(_ => RemoteComputeServiceInterceptor.Options.Default);
        services.AddSingleton(c => new ComputeServiceInterceptor(
            c.GetRequiredService<ComputeServiceInterceptor.Options>(),
            c.FusionHub()));

        // StateFactory
        services.AddSingleton(c => new MixedModeService<StateFactory>.Singleton(new StateFactory(c), c));
        services.AddScoped(c => new MixedModeService<StateFactory>.Scoped(new StateFactory(c), c));
        services.AddTransient(c => c.GetRequiredMixedModeService<StateFactory>());

        // Update delayer & UI action tracker
        services.AddSingleton(_ => new UIActionTracker.Options());
        services.AddScoped<UIActionTracker>(c => new UIActionTracker(
            c.GetRequiredService<UIActionTracker.Options>(), c));
        services.AddScoped<IUpdateDelayer>(c => new UpdateDelayer(c.UIActionTracker()));

        // CommandR, command completion and invalidation
        var commander = Commander;

        // Transient operation scope & its provider
        if (!services.HasService<InMemoryOperationScopeProvider>()) {
            services.AddSingleton(c => new InMemoryOperationScopeProvider(c));
            commander.AddHandlers<InMemoryOperationScopeProvider>();
        }

        // Nested command logger
        if (!services.HasService<NestedOperationLogger>()) {
            services.AddSingleton(c => new NestedOperationLogger(c));
            commander.AddHandlers<NestedOperationLogger>();
        }

        // Operation completion - notifier & producer
        services.AddSingleton(_ => new OperationCompletionNotifier.Options());
        services.AddSingleton<IOperationCompletionNotifier>(c => new OperationCompletionNotifier(
            c.GetRequiredService<OperationCompletionNotifier.Options>(), c));
        services.AddSingleton(_ => new CompletionProducer.Options());
        services.TryAddEnumerable(ServiceDescriptor.Singleton(
            typeof(IOperationCompletionListener),
            typeof(CompletionProducer)));

        // Command completion handler performing invalidations
        services.AddSingleton(_ => new ComputeServiceCommandCompletionInvalidator.Options());
        if (!services.HasService<ComputeServiceCommandCompletionInvalidator>()) {
            services.AddSingleton(c => new ComputeServiceCommandCompletionInvalidator(
                c.GetRequiredService<ComputeServiceCommandCompletionInvalidator.Options>(), c));
            commander.AddHandlers<ComputeServiceCommandCompletionInvalidator>();
        }

        // Completion terminator
        if (!services.HasService<CompletionTerminator>()) {
            services.AddSingleton(_ => new CompletionTerminator());
            commander.AddHandlers<CompletionTerminator>();
        }

        // Core authentication services
        services.AddScoped<ISessionResolver>(c => new SessionResolver(c));
        services.AddScoped(c => c.GetRequiredService<ISessionResolver>().Session);

        // RPC

        // Compute system calls service + call type
        if (!Rpc.Configuration.Services.ContainsKey(typeof(IRpcComputeSystemCalls))) {
            Rpc.Service<IRpcComputeSystemCalls>().HasServer<RpcComputeSystemCalls>().HasName(RpcComputeSystemCalls.Name);
            services.AddSingleton(c => new RpcComputeSystemCalls(c));
            services.AddSingleton(c => new RpcComputeSystemCallSender(c));
            RpcComputeCallType.Register();
        }

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

    public FusionBuilder WithServiceMode(
        RpcServiceMode serviceMode,
        bool makeDefault = false)
        => new(this, serviceMode, makeDefault);

    // ComputeService

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public FusionBuilder AddService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService>(
        RpcServiceMode mode = RpcServiceMode.Default,
        bool addCommandHandlers = true)
        where TService : class, IComputeService
        => AddService(typeof(TService), typeof(TService), ServiceLifetime.Singleton, mode, addCommandHandlers);
    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public FusionBuilder AddService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>(
        RpcServiceMode mode = RpcServiceMode.Default,
        bool addCommandHandlers = true)
        where TService : class
        where TImplementation : class, TService, IComputeService
        => AddService(typeof(TService), typeof(TImplementation), ServiceLifetime.Singleton, mode, addCommandHandlers);
    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public FusionBuilder AddService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService>(
        ServiceLifetime lifetime,
        RpcServiceMode mode = RpcServiceMode.Default,
        bool addCommandHandlers = true)
        where TService : class, IComputeService
        => AddService(typeof(TService), typeof(TService), lifetime, mode, addCommandHandlers);
    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public FusionBuilder AddService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>(
        ServiceLifetime lifetime,
        RpcServiceMode mode = RpcServiceMode.Default,
        bool addCommandHandlers = true)
        where TService : class
        where TImplementation : class, TService, IComputeService
        => AddService(typeof(TService), typeof(TImplementation), lifetime, mode, addCommandHandlers);

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public FusionBuilder AddService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        RpcServiceMode mode = RpcServiceMode.Default,
        bool addCommandHandlers = true)
        => AddService(serviceType, serviceType, ServiceLifetime.Singleton, mode, addCommandHandlers);
    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public FusionBuilder AddService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        RpcServiceMode mode = RpcServiceMode.Default,
        bool addCommandHandlers = true)
        => AddService(serviceType, implementationType, ServiceLifetime.Singleton, mode, addCommandHandlers);
    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public FusionBuilder AddService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        ServiceLifetime lifetime,
        RpcServiceMode mode = RpcServiceMode.Default,
        bool addCommandHandlers = true)
        => AddService(serviceType, serviceType, lifetime, mode, addCommandHandlers);
    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public FusionBuilder AddService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        ServiceLifetime lifetime,
        RpcServiceMode mode = RpcServiceMode.Default,
        bool addCommandHandlers = true)
    {
        if (lifetime != ServiceLifetime.Singleton) {
            var scopedServiceMode = mode.Or(RpcServiceMode.Local);
            if (scopedServiceMode is not RpcServiceMode.Local)
                throw new ArgumentOutOfRangeException(nameof(mode));

            return AddComputeService(serviceType, implementationType, lifetime);
        }

        mode = mode.Or(DefaultServiceMode);
        return mode switch {
            RpcServiceMode.Local => AddComputeService(serviceType, implementationType, addCommandHandlers),
            RpcServiceMode.Client => AddClient(serviceType, default, addCommandHandlers),
            RpcServiceMode.Server => AddServer(serviceType, implementationType, default, addCommandHandlers),
            RpcServiceMode.Distributed => AddDistributedService(serviceType, implementationType, default, addCommandHandlers),
            RpcServiceMode.DistributedPair => AddDistributedServicePair(serviceType, implementationType, default, addCommandHandlers),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public FusionBuilder AddClient<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService>
        (Symbol name = default, bool addCommandHandlers = true)
        => AddClient(typeof(TService), name, addCommandHandlers);
    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public FusionBuilder AddClient(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        Symbol name = default, bool addCommandHandlers = true)
    {
        if (!typeof(IComputeService).IsAssignableFrom(serviceType))
            throw Errors.MustImplement<IComputeService>(serviceType, nameof(serviceType));

        Services.AddSingleton(serviceType,
            c => c.FusionHub().NewRemoteComputeServiceProxy(serviceType, serviceType, null));
        if (addCommandHandlers)
            Commander.AddHandlers(serviceType);
        Rpc.Service(serviceType).HasName(name);
        return this;
    }

    public FusionBuilder AddComputeService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService>
        (bool addCommandHandlers = true)
        => AddComputeService(typeof(TService), typeof(TService), ServiceLifetime.Singleton, addCommandHandlers);
    public FusionBuilder AddComputeService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService>
        (ServiceLifetime lifetime, bool addCommandHandlers = true)
        => AddComputeService(typeof(TService), typeof(TService), lifetime, addCommandHandlers);
    public FusionBuilder AddComputeService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>
        (bool addCommandHandlers = true)
        => AddComputeService(typeof(TService), typeof(TImplementation), ServiceLifetime.Singleton, addCommandHandlers);
    public FusionBuilder AddComputeService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>
        (ServiceLifetime lifetime, bool addCommandHandlers = true)
        => AddComputeService(typeof(TService), typeof(TImplementation), lifetime, addCommandHandlers);

    public FusionBuilder AddComputeService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        bool addCommandHandlers = true)
        => AddComputeService(serviceType, serviceType, ServiceLifetime.Singleton, addCommandHandlers);
    public FusionBuilder AddComputeService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        ServiceLifetime lifetime, bool addCommandHandlers = true)
        => AddComputeService(serviceType, serviceType, lifetime, addCommandHandlers);
    public FusionBuilder AddComputeService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        bool addCommandHandlers = true)
        => AddComputeService(serviceType, implementationType, ServiceLifetime.Singleton, addCommandHandlers);
    public FusionBuilder AddComputeService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        ServiceLifetime lifetime, bool addCommandHandlers = true)
    {
        // ~ RpcBuilder.AddLocalService, but for Compute Service

        if (!typeof(IComputeService).IsAssignableFrom(implementationType))
            throw Errors.MustImplement<IComputeService>(implementationType, nameof(serviceType));
        if (!serviceType.IsAssignableFrom(implementationType))
            throw Errors.MustBeAssignableTo(implementationType, serviceType, nameof(implementationType));
        if (!implementationType.IsClass)
            throw Errors.MustBeClass(implementationType, nameof(implementationType));
        if (lifetime != ServiceLifetime.Singleton && !typeof(IHasIsDisposed).IsAssignableFrom(implementationType))
            throw Errors.MustImplement<IHasIsDisposed>(implementationType, nameof(implementationType));

        var descriptor = new ServiceDescriptor(serviceType,
            c => c.FusionHub().NewComputeServiceProxy(c, implementationType),
            lifetime);
        Services.Add(descriptor);
        if (addCommandHandlers)
            Commander.AddHandlers(serviceType, implementationType);
        return this;
    }

    public FusionBuilder AddServer<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>
        (Symbol name = default, bool addCommandHandlers = true)
        => AddServer(typeof(TService), typeof(TImplementation), name, addCommandHandlers);
    public FusionBuilder AddServer(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        Symbol name = default,
        bool addCommandHandlers = true)
    {
        // ~ RpcBuilder.AddServer, but for Compute Service

        if (!typeof(IComputeService).IsAssignableFrom(serviceType))
            throw Errors.MustImplement<IComputeService>(serviceType, nameof(serviceType));

        AddComputeService(serviceType, implementationType, false);
        if (addCommandHandlers)
            Commander.AddHandlers(serviceType);
        Rpc.Service(serviceType).HasServer(serviceType).HasName(name);
        return this;
    }

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public FusionBuilder AddDistributedService<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>
        (Symbol name = default, bool addCommandHandlers = true)
        => AddDistributedService(typeof(TService), typeof(TImplementation), name, addCommandHandlers);
    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public FusionBuilder AddDistributedService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        Symbol name = default,
        bool addCommandHandlers = true)
    {
        // ~ RpcBuilder.AddDistributedService, but for Compute Service

        if (!typeof(IComputeService).IsAssignableFrom(serviceType))
            throw Errors.MustImplement<IRpcService>(serviceType, nameof(serviceType));
        if (!serviceType.IsAssignableFrom(implementationType))
            throw Errors.MustBeAssignableTo(implementationType, serviceType, nameof(implementationType));
        if (!implementationType.IsClass)
            throw Errors.MustBeClass(implementationType, nameof(implementationType));

        Services.AddSingleton(serviceType,
            c => c.FusionHub().NewRemoteComputeServiceProxy(serviceType, implementationType, null));
        if (addCommandHandlers)
            Commander.AddHandlers(serviceType);
        Rpc.Service(serviceType).HasServer(serviceType).HasName(name);
        return this;
    }

    public FusionBuilder AddDistributedServicePair<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>
        (Symbol name = default, bool addCommandHandlers = true)
        => AddDistributedServicePair(typeof(TService), typeof(TImplementation), name, addCommandHandlers);
    public FusionBuilder AddDistributedServicePair(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        Symbol name = default,
        bool addCommandHandlers = true)
    {
        // ~ RpcBuilder.AddDistributedServicePair, but for Compute Service

        if (!typeof(IComputeService).IsAssignableFrom(serviceType))
            throw Errors.MustImplement<IComputeService>(serviceType, nameof(serviceType));

        AddComputeService(implementationType, false);
        Services.AddSingleton(serviceType, c => {
            var localTarget = c.GetRequiredService(implementationType);
            return c.FusionHub().NewRemoteComputeServiceProxy(serviceType, serviceType, localTarget);
        });
        if (addCommandHandlers)
            Commander.AddHandlers(serviceType);
        Rpc.Service(serviceType).HasServer(implementationType).HasName(name);
        return this;
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

    public class FusionTag
    {
        public RpcServiceMode DefaultServiceMode {
            get;
            set => field = value.Or(RpcServiceMode.Local);
        } = RpcServiceMode.Local;
    }
}
