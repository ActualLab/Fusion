using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ActualLab.CommandR.Diagnostics;
using ActualLab.CommandR.Interception;
using ActualLab.CommandR.Internal;
using ActualLab.Generators;
using ActualLab.Interception;
using ActualLab.Resilience;
using ActualLab.Versioning;
using ActualLab.Versioning.Providers;

namespace ActualLab.CommandR;

public readonly struct CommanderBuilder
{
    public IServiceCollection Services { get; }
    public HashSet<CommandHandler> Handlers { get; }

    [RequiresUnreferencedCode(UnreferencedCode.Commander)]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Proxies))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(CommandHandlerMethodDef))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(MethodCommandHandler<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(InterfaceCommandHandler<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(CommandServiceInterceptor))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(CommandContext<>))]
    internal CommanderBuilder(
        IServiceCollection services,
        Action<CommanderBuilder>? configure)
    {
        Services = services;
        if (services.FindInstance<HashSet<CommandHandler>>() is { } handlers) {
            // Already configured
            Handlers = handlers;
            configure?.Invoke(this);
            return;
        }

        Handlers = services.AddInstance(new HashSet<CommandHandler>(), addInFront: true);

        // Core services
        services.TryAddSingleton<UuidGenerator>(_ => new UlidUuidGenerator());
        services.TryAddSingleton<VersionGenerator<long>>(c => new ClockBasedVersionGenerator(c.Clocks().SystemClock));
        services.TryAddSingleton(_ => ChaosMaker.Default);

        // Commander, handlers, etc.
        services.AddSingleton<ICommander>(c => new Commander(c));
        services.AddSingleton(c => c.GetRequiredService<ICommander>().Hub);
        services.AddSingleton(c => new CommandHandlerRegistry(c));
        services.AddSingleton(_ => new CommandHandlerResolver.Options());
        services.AddSingleton(c => new CommandHandlerResolver(
            c.GetRequiredService<CommandHandlerResolver.Options>(), c));

        // Command services & their dependencies
        Services.AddSingleton(_ => CommandServiceInterceptor.Options.Default);
        Services.AddSingleton(c => new CommandServiceInterceptor(
            c.GetRequiredService<CommandServiceInterceptor.Options>(), c));

        // Default handlers
        services.AddSingleton(_ => new PreparedCommandHandler());
        AddHandlers<PreparedCommandHandler>();
        services.AddSingleton(c => new CommandTracer(c));
        AddHandlers<CommandTracer>();
        services.AddSingleton(_ => new LocalCommandRunner());
        AddHandlers<LocalCommandRunner>();

        configure?.Invoke(this);
    }

    // Handler discovery

    public CommanderBuilder AddHandlers<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService>
        (double? priorityOverride = null)
        => AddHandlers(typeof(TService), priorityOverride);
    public CommanderBuilder AddHandlers<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>
        (double? priorityOverride = null)
        => AddHandlers(typeof(TService), typeof(TImplementation), priorityOverride);
    public CommanderBuilder AddHandlers(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        double? priorityOverride = null)
        => AddHandlers(serviceType, serviceType, priorityOverride);
    public CommanderBuilder AddHandlers(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        double? priorityOverride = null)
    {
        if (!serviceType.IsAssignableFrom(implementationType))
            throw ActualLab.Internal.Errors.MustBeAssignableTo(implementationType, serviceType, nameof(implementationType));

        var interfaceMethods = new HashSet<MethodInfo>();

        // ICommandHandler<TCommand> interfaces
        var tInterfaces = implementationType.GetInterfaces();
        foreach (var tInterface in tInterfaces) {
            if (!tInterface.IsGenericType)
                continue;
            var gInterface = tInterface.GetGenericTypeDefinition();
            if (gInterface != typeof(ICommandHandler<>))
                continue;
            var tCommand = tInterface.GetGenericArguments().SingleOrDefault();
            if (tCommand == null)
                continue;

            var method = implementationType.GetInterfaceMap(tInterface).TargetMethods.Single();
#pragma warning disable IL2026
            var attr = MethodCommandHandler.GetAttribute(method);
#pragma warning restore IL2026
            var isFilter = attr?.IsFilter ?? false;
            var order = priorityOverride ?? attr?.Priority ?? 0;
#pragma warning disable IL2072
            AddHandler(InterfaceCommandHandler.New(serviceType, tCommand, isFilter, order));
#pragma warning restore IL2072
            interfaceMethods.Add(method);
        }

        // Methods
        var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var methods = (implementationType.IsInterface
            ? implementationType.GetAllInterfaceMethods(bindingFlags, t => !typeof(ICommandHandler).IsAssignableFrom(t))
            : implementationType.GetMethods(bindingFlags)
            ).ToList();
        foreach (var method in methods) {
            if (method.DeclaringType == typeof(object))
                continue;
            if (interfaceMethods.Contains(method))
                continue;
            if (!method.ReturnType.IsTaskOrValueTask())
                continue;
            var parameters = method.GetParameters();
            if (parameters.Length == 0)
                continue;
            if (!typeof(ICommand).IsAssignableFrom(parameters[0].ParameterType))
                continue;

#pragma warning disable IL2026
            var handler = MethodCommandHandler.TryNew(serviceType, method, priorityOverride);
#pragma warning restore IL2026
            if (handler == null)
                continue;

            AddHandler(handler);
        }

        return this;
    }

    // AddService

    public CommanderBuilder AddService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService>(
        ServiceLifetime lifetime = ServiceLifetime.Singleton,
        double? priorityOverride = null)
        where TService : class, ICommandService
        => AddService(typeof(TService), lifetime, priorityOverride);
    public CommanderBuilder AddService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TImplementation>(
        ServiceLifetime lifetime = ServiceLifetime.Singleton,
        double? priorityOverride = null)
        where TService : class
        where TImplementation : class, TService, ICommandService
        => AddService(typeof(TService), typeof(TImplementation), lifetime, priorityOverride);

    public CommanderBuilder AddService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        ServiceLifetime lifetime = ServiceLifetime.Singleton,
        double? priorityOverride = null)
        => AddService(serviceType, serviceType, lifetime, priorityOverride);
    public CommanderBuilder AddService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        ServiceLifetime lifetime = ServiceLifetime.Singleton,
        double? priorityOverride = null)
    {
        if (!serviceType.IsAssignableFrom(implementationType))
            throw ActualLab.Internal.Errors.MustBeAssignableTo(implementationType, serviceType, nameof(implementationType));
        if (!typeof(ICommandService).IsAssignableFrom(implementationType))
            throw ActualLab.Internal.Errors.MustImplement<ICommandService>(implementationType, nameof(implementationType));

        var descriptor = new ServiceDescriptor(serviceType, c => CommandServiceProxies.New(c, implementationType), lifetime);
        Services.TryAdd(descriptor);
        AddHandlers(serviceType, implementationType, priorityOverride);
        return this;
    }

    // Low-level methods

    public CommanderBuilder AddHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TCommand>
        (double priority = 0)
        where TService : class
        where TCommand : class, ICommand
        => AddHandler<TService, TCommand>(false, priority);

    public CommanderBuilder AddHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TCommand>
        (bool isFilter, double priority = 0)
        where TService : class
        where TCommand : class, ICommand
        => AddHandler(InterfaceCommandHandler.New<TService, TCommand>(isFilter, priority));

    [RequiresUnreferencedCode(UnreferencedCode.Commander)]
    public CommanderBuilder AddHandler(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        MethodInfo method,
        double? priorityOverride = null)
        => AddHandler(MethodCommandHandler.New(serviceType, method, priorityOverride));

    public CommanderBuilder AddHandler(CommandHandler handler)
    {
        Handlers.Add(handler);
        return this;
    }

    public CommanderBuilder ClearHandlers()
    {
        Handlers.Clear();
        return this;
    }

    // Filters

    public CommanderBuilder AddHandlerFilter(CommandHandlerFilter commandHandlerFilter)
    {
        Services.AddSingleton(commandHandlerFilter);
        return this;
    }

    public CommanderBuilder AddHandlerFilter<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TCommandHandlerFilter>()
        where TCommandHandlerFilter : CommandHandlerFilter
    {
        Services.AddSingleton<CommandHandlerFilter, TCommandHandlerFilter>();
        return this;
    }

    public CommanderBuilder AddHandlerFilter<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TCommandHandlerFilter>(
        Func<IServiceProvider, TCommandHandlerFilter> factory)
        where TCommandHandlerFilter : CommandHandlerFilter
    {
        Services.AddSingleton<CommandHandlerFilter, TCommandHandlerFilter>(factory);
        return this;
    }

    public CommanderBuilder AddHandlerFilter(Func<CommandHandler, Type, bool> commandHandlerFilter)
        => AddHandlerFilter(_ => new FuncCommandHandlerFilter(commandHandlerFilter));

    public CommanderBuilder AddHandlerFilter(
        Func<IServiceProvider, Func<CommandHandler, Type, bool>> commandHandlerFilterFactory)
        => AddHandlerFilter(c => {
            var filter = commandHandlerFilterFactory(c);
            return new FuncCommandHandlerFilter(filter);
        });

    public CommanderBuilder ClearHandlerFilters()
    {
        Services.RemoveAll<CommandHandlerFilter>();
        return this;
    }
}
