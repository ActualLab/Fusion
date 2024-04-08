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
using ActualLab.Fusion.UI;
using ActualLab.Rpc;
using ActualLab.Versioning.Providers;

namespace ActualLab.Fusion;

public readonly struct FusionBuilder
{
    public IServiceCollection Services { get; }
    public CommanderBuilder Commander { get; }
    public RpcBuilder Rpc { get; }
    public RpcServiceMode ServiceMode { get; }

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(CommanderBuilder))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcBuilder))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ComputeServiceInterceptor))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ComputeMethodFunction<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcComputeSystemCalls))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcInboundComputeCall<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcOutboundComputeCall<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ClientComputeServiceInterceptor))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ClientComputeMethodFunction<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(FuncComputedState<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(AnonymousComputedSource<>))]
    internal FusionBuilder(
        IServiceCollection services,
        Action<FusionBuilder>? configure,
        RpcServiceMode serviceMode,
        bool setDefaultServiceMode)
    {
        Services = services;
        Commander = services.AddCommander();
        Rpc = services.AddRpc();
        var dFusionTag = services.FirstOrDefault(d => d.ServiceType == typeof(FusionTag));
        if (dFusionTag is { ImplementationInstance: FusionTag fusionTag }) {
            ServiceMode = serviceMode.Or(fusionTag.ServiceMode);
            if (setDefaultServiceMode)
                fusionTag.ServiceMode = ServiceMode;

            configure?.Invoke(this);
            return;
        }

        // We want above FusionTag lookup to run in O(1), so...
        ServiceMode = serviceMode.OrNone();
        services.RemoveAll<FusionTag>();
        services.Insert(0, new ServiceDescriptor(
            typeof(FusionTag),
            new FusionTag(setDefaultServiceMode ? ServiceMode : RpcServiceMode.Local)));

        // Common services
        services.AddOptions();
        services.AddConverters();
        services.TryAddSingleton(_ => ClockBasedVersionGenerator.DefaultCoarse);
        services.TryAddSingleton(c => new FusionInternalHub(c));

        // Compute services & their dependencies
        services.TryAddSingleton(_ => new ComputedOptionsProvider());
        services.TryAddSingleton(_ => TransientErrorDetector.DefaultPreferTransient.For<IComputed>());
        services.TryAddSingleton(_ => new ComputeServiceInterceptor.Options());
        services.TryAddSingleton(c => new ComputeServiceInterceptor(
            c.GetRequiredService<ComputeServiceInterceptor.Options>(), c));

        // States
        services.TryAddSingleton(c => new MixedModeService<IStateFactory>.Singleton(new StateFactory(c), c));
        services.TryAddScoped(c => new MixedModeService<IStateFactory>.Scoped(new StateFactory(c), c));
        services.TryAddTransient(c => c.GetRequiredMixedModeService<IStateFactory>());
        services.TryAddSingleton(typeof(MutableState<>.Options));
        services.TryAddTransient(typeof(IMutableState<>), typeof(MutableState<>));

        // Update delayer & UI action tracker
        services.TryAddSingleton(_ => new UIActionTracker.Options());
        services.TryAddScoped<UIActionTracker>(c => new UIActionTracker(
            c.GetRequiredService<UIActionTracker.Options>(), c));
        services.TryAddScoped<IUpdateDelayer>(c => new UpdateDelayer(c.UIActionTracker()));

        // CommandR, command completion and invalidation
        var commander = Commander;

        // Transient operation scope & its provider
        if (!services.HasService<TransientOperationScopeProvider>()) {
            services.AddSingleton(c => new TransientOperationScopeProvider(c));
            commander.AddHandlers<TransientOperationScopeProvider>();
        }

        // Nested command logger
        if (!services.HasService<NestedOperationLogger>()) {
            services.AddSingleton(c => new NestedOperationLogger(c));
            commander.AddHandlers<NestedOperationLogger>();
        }

        // Operation completion - notifier & producer
        services.TryAddSingleton(_ => new OperationCompletionNotifier.Options());
        services.TryAddSingleton<IOperationCompletionNotifier>(c => new OperationCompletionNotifier(
            c.GetRequiredService<OperationCompletionNotifier.Options>(), c));
        services.TryAddSingleton(_ => new CompletionProducer.Options());
        services.TryAddEnumerable(ServiceDescriptor.Singleton(
            typeof(IOperationCompletionListener),
            typeof(CompletionProducer)));

        // Command completion handler performing invalidations
        services.TryAddSingleton(_ => new PostCompletionInvalidator.Options());
        if (!services.HasService<PostCompletionInvalidator>()) {
            services.AddSingleton(c => new PostCompletionInvalidator(
                c.GetRequiredService<PostCompletionInvalidator.Options>(), c));
            commander.AddHandlers<PostCompletionInvalidator>();
        }

        // Completion terminator
        if (!services.HasService<CompletionTerminator>()) {
            services.AddSingleton(_ => new CompletionTerminator());
            commander.AddHandlers<CompletionTerminator>();
        }

        // Core authentication services
        services.TryAddScoped<ISessionResolver>(c => new SessionResolver(c));
        services.TryAddScoped(c => c.GetRequiredService<ISessionResolver>().Session);

        // RPC

        // Compute system calls service + call type
        if (!Rpc.Configuration.Services.ContainsKey(typeof(IRpcComputeSystemCalls))) {
            Rpc.Service<IRpcComputeSystemCalls>().HasServer<RpcComputeSystemCalls>().HasName(RpcComputeSystemCalls.Name);
            services.AddSingleton(c => new RpcComputeSystemCalls(c));
            services.AddSingleton(c => new RpcComputeSystemCallSender(c));
            RpcComputeCallType.Register();
        }

        // Interceptor options (the instances are created by FusionProxies)
        services.TryAddSingleton(_ => new ClientComputeServiceInterceptor.Options());

        configure?.Invoke(this);
    }

    internal FusionBuilder(FusionBuilder fusion, RpcServiceMode serviceMode, bool setDefaultServiceMode)
    {
        Services = fusion.Services;
        Commander = fusion.Commander;
        Rpc = fusion.Rpc;
        ServiceMode = serviceMode;
        if (!setDefaultServiceMode)
            return;

        var dFusionTag = Services.FirstOrDefault(d => d.ServiceType == typeof(FusionTag));
        if (dFusionTag is { ImplementationInstance: FusionTag fusionTag }) {
            ServiceMode = serviceMode.Or(fusionTag.ServiceMode);
            fusionTag.ServiceMode = ServiceMode;
        }
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
            if (!(mode is RpcServiceMode.Local or RpcServiceMode.Default))
                throw new ArgumentOutOfRangeException(nameof(mode));

            return AddComputeService(serviceType, implementationType, lifetime);
        }

        mode = mode.Or(ServiceMode);
        return mode switch {
            RpcServiceMode.Local => AddComputeService(serviceType, implementationType, addCommandHandlers),
            RpcServiceMode.Server => AddServer(serviceType, implementationType, default, addCommandHandlers),
            RpcServiceMode.Hybrid => AddHybrid(serviceType, implementationType, default, addCommandHandlers),
            RpcServiceMode.HybridServer => AddHybridServer(serviceType, implementationType, default, addCommandHandlers),
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
        if (!serviceType.IsInterface)
            throw ActualLab.Internal.Errors.MustBeInterface(serviceType, nameof(serviceType));
        if (!typeof(IComputeService).IsAssignableFrom(serviceType))
            throw ActualLab.Internal.Errors.MustImplement<IComputeService>(serviceType, nameof(serviceType));

        Services.AddSingleton(serviceType, c => FusionProxies.NewClientProxy(c, serviceType));
        if (addCommandHandlers)
            Commander.AddHandlers(serviceType);
        Rpc.Service(serviceType).HasName(name);
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
        AddComputeService(serviceType, implementationType, addCommandHandlers);
        Rpc.Service(serviceType).HasServer(serviceType).HasName(name);
        return this;
    }

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public FusionBuilder AddHybrid<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>
        (Symbol name = default, bool addCommandHandlers = true)
        => AddHybrid(typeof(TService), typeof(TImplementation), name, addCommandHandlers);
    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public FusionBuilder AddHybrid(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        Symbol name = default,
        bool addCommandHandlers = true)
    {
        if (!serviceType.IsInterface)
            throw ActualLab.Internal.Errors.MustBeInterface(serviceType, nameof(serviceType));
        if (!serviceType.IsAssignableFrom(implementationType))
            throw ActualLab.Internal.Errors.MustBeAssignableTo(implementationType, serviceType, nameof(implementationType));
        if (!typeof(IComputeService).IsAssignableFrom(implementationType))
            throw ActualLab.Internal.Errors.MustImplement<IComputeService>(implementationType, nameof(implementationType));

        Services.AddSingleton(implementationType, c => FusionProxies.NewProxy(c, implementationType));
        Services.AddSingleton(serviceType, c => FusionProxies.NewHybridProxy(c, serviceType, implementationType));
        if (addCommandHandlers)
            Commander.AddHandlers(serviceType);
        Rpc.Service(serviceType).HasName(name);
        return this;
    }

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public FusionBuilder AddHybridServer<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>(
        Symbol name = default,
        bool addCommandHandlers = true)
        => AddHybridServer(typeof(TService), typeof(TImplementation), name, addCommandHandlers);
    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public FusionBuilder AddHybridServer(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        Symbol name = default,
        bool addCommandHandlers = true)
    {
        AddHybrid(serviceType, implementationType, name, addCommandHandlers);
        Rpc.Service(serviceType).HasServer(implementationType);
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
        services.AddSingleton(TransientErrorDetector.DefaultPreferNonTransient.For<IOperationReprocessor>());
        return this;
    }

    // AddClientComputeCache

    public FusionBuilder AddClientComputedCache<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCache, TOptions>(
        Func<IServiceProvider, TOptions>? optionsFactory = null)
        where TCache : class, IClientComputedCache
        where TOptions : class, new()
    {
        var services = Services;
        services.AddSingleton(optionsFactory, _ => new TOptions());
        if (services.HasService<TCache>())
            return this;

        services.AddSingleton<TCache>();
        services.AddAlias<IClientComputedCache, TCache>();
        return this;
    }

    public FusionBuilder AddSharedClientComputedCache<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCache, TOptions>(
        Func<IServiceProvider, TOptions>? optionsFactory = null)
        where TCache : ClientComputedCache
        where TOptions : class, new()
    {
        var services = Services;
        services.AddSingleton(optionsFactory, _ => new TOptions());
        if (services.HasService<TCache>())
            return this;

        services.AddSingleton<TCache>();
        services.AddSingleton(c => new SharedClientComputedCache(c.GetRequiredService<TCache>()));
        services.AddAlias<IClientComputedCache, SharedClientComputedCache>();
        return this;
    }

    // AddComputedGraphPruner

    public FusionBuilder AddComputedGraphPruner(
        Func<IServiceProvider, ComputedGraphPruner.Options>? optionsFactory = null)
    {
        var services = Services;
        services.AddSingleton(optionsFactory, _ => ComputedGraphPruner.Options.Default);
        if (services.HasService<ComputedGraphPruner>())
            return this;

        services.AddSingleton(c => new ComputedGraphPruner(
            c.GetRequiredService<ComputedGraphPruner.Options>(), c));
        services.AddHostedService(c => c.GetRequiredService<ComputedGraphPruner>());
        return this;
    }

    // Private methods

    private FusionBuilder AddComputeService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        bool addCommandHandlers = true)
        => AddComputeService(serviceType, implementationType, ServiceLifetime.Singleton, addCommandHandlers);
    private FusionBuilder AddComputeService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        ServiceLifetime lifetime,
        bool addCommandHandlers = true)
    {
        if (!serviceType.IsAssignableFrom(implementationType))
            throw ActualLab.Internal.Errors.MustBeAssignableTo(implementationType, serviceType, nameof(implementationType));
        if (!typeof(IComputeService).IsAssignableFrom(implementationType))
            throw ActualLab.Internal.Errors.MustImplement<IComputeService>(implementationType, nameof(implementationType));
        if (lifetime != ServiceLifetime.Singleton && !typeof(IHasIsDisposed).IsAssignableFrom(implementationType))
            throw ActualLab.Internal.Errors.MustImplement<IHasIsDisposed>(implementationType, nameof(implementationType));

        var descriptor = new ServiceDescriptor(serviceType, c => FusionProxies.NewProxy(c, implementationType), lifetime);
        Services.Add(descriptor);
        if (addCommandHandlers)
            Commander.AddHandlers(serviceType, implementationType);
        return this;
    }

    // Nested types

    public class FusionTag
    {
        private RpcServiceMode _serviceMode;

        public RpcServiceMode ServiceMode {
            get => _serviceMode;
            set => _serviceMode = value.OrNone();
        }

        public FusionTag(RpcServiceMode serviceMode)
            => ServiceMode = serviceMode;
    }
}
