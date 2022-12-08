using Castle.DynamicProxy;
using Castle.DynamicProxy.Generators;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stl.Conversion;
using Stl.Extensibility;
using Stl.Fusion.Authentication;
using Stl.Fusion.Bridge;
using Stl.Fusion.Bridge.Interception;
using Stl.Fusion.Interception;
using Stl.Fusion.Internal;
using Stl.Fusion.Multitenancy;
using Stl.Fusion.Operations.Internal;
using Stl.Fusion.Operations.Reprocessing;
using Stl.Fusion.UI;
using Stl.Multitenancy;
using Stl.Versioning.Providers;

namespace Stl.Fusion;

public readonly struct FusionBuilder
{
    private class AddedTag { }
    private static readonly ServiceDescriptor AddedTagDescriptor = new(typeof(AddedTag), new AddedTag());

    public IServiceCollection Services { get; }

    internal FusionBuilder(
        IServiceCollection services, 
        Action<FusionBuilder>? configure)
    {
        Services = services;
        if (Services.Contains(AddedTagDescriptor)) {
            configure?.Invoke(this);
            return;
        }

        // We want above Contains call to run in O(1), so...
        Services.Insert(0, AddedTagDescriptor);
        Services.AddCommander();

        // Common services
        Services.AddOptions();
        Services.AddConverters();
        Services.TryAddSingleton(MomentClockSet.Default);
        Services.TryAddSingleton(c => c.GetRequiredService<MomentClockSet>().SystemClock);
        Services.TryAddSingleton(LTagVersionGenerator.Default);
        Services.TryAddSingleton(ClockBasedVersionGenerator.DefaultCoarse);

        // Compute services & their dependencies
        Services.TryAddSingleton(ComputeServiceProxyGenerator.Default);
        Services.TryAddSingleton<IComputedOptionsProvider, ComputedOptionsProvider>();
        Services.TryAddSingleton<IMatchingTypeFinder>(_ => new MatchingTypeFinder());
        Services.TryAddSingleton<IArgumentHandlerProvider, ArgumentHandlerProvider>();
        Services.TryAddSingleton(TransientErrorDetector.DefaultPreferTransient.For<IComputed>());
        Services.TryAddSingleton(new ComputeMethodInterceptor.Options());
        Services.TryAddSingleton<ComputeMethodInterceptor>();
        Services.TryAddSingleton(new ComputeServiceInterceptor.Options());
        Services.TryAddSingleton<ComputeServiceInterceptor>();

        // States
        Services.TryAddSingleton(c => new MixedModeService<IStateFactory>.Singleton(new StateFactory(c), c));
        Services.TryAddScoped(c => new MixedModeService<IStateFactory>.Scoped(new StateFactory(c), c));
        Services.TryAddTransient(c => c.GetRequiredMixedModeService<IStateFactory>());
        Services.TryAddSingleton(typeof(MutableState<>.Options));
        Services.TryAddTransient(typeof(IMutableState<>), typeof(MutableState<>));

        // Update delayer & UI action tracker
        Services.TryAddScoped<UIActionTracker, UIActionTracker>();
        Services.TryAddScoped<IUpdateDelayer>(c => new UpdateDelayer(c.UIActionTracker()));
        Services.TryAddSingleton<UIActionTracker.Options>();

        // CommandR, command completion and invalidation
        var commander = Services.AddCommander();
        Services.TryAddSingleton<AgentInfo>();
        Services.TryAddSingleton<InvalidationInfoProvider>();

        // Transient operation scope & its provider
        if (!Services.HasService<TransientOperationScopeProvider>()) {
            Services.AddSingleton<TransientOperationScopeProvider>();
            commander.AddHandlers<TransientOperationScopeProvider>();
        }

        // Nested command logger
        if (!Services.HasService<NestedCommandLogger>()) {
            Services.AddSingleton<NestedCommandLogger>();
            commander.AddHandlers<NestedCommandLogger>();
        }

        // Operation completion - notifier & producer
        Services.TryAddSingleton<OperationCompletionNotifier.Options>();
        Services.TryAddSingleton<IOperationCompletionNotifier, OperationCompletionNotifier>();
        Services.TryAddSingleton<CompletionProducer.Options>();
        Services.TryAddEnumerable(ServiceDescriptor.Singleton(
            typeof(IOperationCompletionListener),
            typeof(CompletionProducer)));

        // Command completion handler performing invalidations
        Services.TryAddSingleton<PostCompletionInvalidator.Options>();
        if (!Services.HasService<PostCompletionInvalidator>()) {
            Services.AddSingleton<PostCompletionInvalidator>();
            commander.AddHandlers<PostCompletionInvalidator>();
        }

        // Completion terminator
        if (!Services.HasService<CompletionTerminator>()) {
            Services.AddSingleton<CompletionTerminator>();
            commander.AddHandlers<CompletionTerminator>();
        }

        // Core multitenancy services
        Services.TryAddSingleton<ITenantRegistry<Unit>, SingleTenantRegistry<Unit>>();
        Services.TryAddSingleton<DefaultTenantResolver<Unit>.Options>();
        Services.TryAddSingleton<ITenantResolver<Unit>, DefaultTenantResolver<Unit>>();
        // And make it default
        Services.TryAddSingleton<ITenantRegistry>(c => c.GetRequiredService<ITenantRegistry<Unit>>());
        Services.TryAddSingleton<ITenantResolver>(c => c.GetRequiredService<ITenantResolver<Unit>>());

        configure?.Invoke(this);
    }

    static FusionBuilder()
    {
        var nonReplicableAttributeTypes = new HashSet<Type>() {
            typeof(AsyncStateMachineAttribute),
            typeof(ComputeMethodAttribute),
        };
        foreach (var type in nonReplicableAttributeTypes)
            if (!AttributesToAvoidReplicating.Contains(type))
                AttributesToAvoidReplicating.Add(type);
    }

    // AddPublisher, AddReplicator

    public FusionBuilder AddPublisher(
        Func<IServiceProvider, PublisherOptions>? optionsFactory = null)
    {
        if (optionsFactory != null)
            Services.AddSingleton(optionsFactory);
        else 
            Services.TryAddSingleton<PublisherOptions>();

        Services.TryAddSingleton<IPublisher, Publisher>();
        return this;
    }

    public FusionBuilder AddReplicator(
        Func<IServiceProvider, ReplicatorOptions>? optionsFactory = null)
    {
        if (optionsFactory != null)
            Services.AddSingleton(optionsFactory);
        else 
            Services.TryAddSingleton<ReplicatorOptions>();
        if (Services.HasService<IReplicator>())
            return this;

        // ReplicaServiceProxyGenerator
        Services.TryAddSingleton(ReplicaServiceProxyGenerator.Default);
        Services.TryAddSingleton(new ReplicaMethodInterceptor.Options());
        Services.TryAddSingleton<ReplicaMethodInterceptor>();
        Services.TryAddSingleton(new ReplicaServiceInterceptor.Options());
        Services.TryAddSingleton<ReplicaServiceInterceptor>();
        // Replicator
        Services.TryAddSingleton<IReplicator, Replicator>();
        return this;
    }

    // AddComputeService

    public FusionBuilder AddComputeService<TService>(
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TService : class
        => AddComputeService(typeof(TService), lifetime);
    public FusionBuilder AddComputeService<TService, TImplementation>(
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TService : class
        where TImplementation : class, TService
        => AddComputeService(typeof(TService), typeof(TImplementation), lifetime);

    public FusionBuilder AddComputeService(
        Type serviceType,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        => AddComputeService(serviceType, serviceType, lifetime);
    public FusionBuilder AddComputeService(
        Type serviceType, Type implementationType,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        if (!serviceType.IsAssignableFrom(implementationType))
            throw new ArgumentOutOfRangeException(nameof(implementationType));
        if (Services.Any(d => d.ServiceType == serviceType))
            return this;

        object Factory(IServiceProvider c)
        {
            // We should try to validate it here because if the type doesn't
            // have any virtual methods (which might be a mistake), no calls
            // will be intercepted, so no error will be thrown later.
            var interceptor = c.GetRequiredService<ComputeServiceInterceptor>();
            interceptor.ValidateType(implementationType);
            var proxyGenerator = c.GetRequiredService<ComputeServiceProxyGenerator>();
            var proxyType = proxyGenerator.GetProxyType(implementationType);
            return c.Activate(proxyType, new object[] { new IInterceptor[] { interceptor } });
        }

        var descriptor = new ServiceDescriptor(serviceType, Factory, lifetime);
        Services.Add(descriptor);
        Services.AddCommander().AddHandlers(serviceType, implementationType);
        return this;
    }

    // AddAuthentication

    public FusionAuthenticationBuilder AddAuthentication()
        => new(this, null);

    public FusionBuilder AddAuthentication(Action<FusionAuthenticationBuilder> configure) 
        => new FusionAuthenticationBuilder(this, configure).Fusion;

    // AddOperationReprocessor

    public FusionBuilder AddOperationReprocessor(
        Func<IServiceProvider, OperationReprocessorOptions>? optionsFactory = null)
        => AddOperationReprocessor<OperationReprocessor>(optionsFactory);

    public FusionBuilder AddOperationReprocessor<TOperationReprocessor>(
        Func<IServiceProvider, OperationReprocessorOptions>? optionsFactory = null)
        where TOperationReprocessor : class, IOperationReprocessor
    {
        if (optionsFactory != null)
            Services.AddSingleton(optionsFactory);
        else
            Services.TryAddSingleton<OperationReprocessorOptions>();

        if (!Services.HasService<IOperationReprocessor>()) {
            Services.AddSingleton(TransientErrorDetector.DefaultPreferNonTransient.For<IOperationReprocessor>());
            Services.AddTransient<TOperationReprocessor>();
            Services.AddTransient<IOperationReprocessor>(c => c.GetRequiredService<TOperationReprocessor>());
            Services.AddCommander().AddHandlers<TOperationReprocessor>();
        }
        return this;
    }

    // AddComputedGraphPruner

    public FusionBuilder AddComputedGraphPruner(
        Func<IServiceProvider, ComputedGraphPruner.Options>? optionsFactory = null)
    {
        if (optionsFactory != null)
            Services.AddSingleton(optionsFactory);
        else
            Services.TryAddSingleton<ComputedGraphPruner.Options>();

        Services.TryAddSingleton<ComputedGraphPruner>();
        Services.AddHostedService<ComputedGraphPruner>();
        return this;
    }
}
