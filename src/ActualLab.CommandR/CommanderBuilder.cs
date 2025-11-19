using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ActualLab.CommandR.Diagnostics;
using ActualLab.CommandR.Interception;
using ActualLab.CommandR.Internal;
using ActualLab.CommandR.Rpc;
using ActualLab.CommandR.Trimming;
using ActualLab.Generators;
using ActualLab.Interception;
using ActualLab.Interception.Trimming;
using ActualLab.Resilience;
using ActualLab.Trimming;
using ActualLab.Versioning;
using ActualLab.Versioning.Providers;

namespace ActualLab.CommandR;

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume all command handling code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2062", Justification = "We assume all command handling code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "We assume all command handling code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "We assume all command handling code is preserved")]
public readonly struct CommanderBuilder
{
    public IServiceCollection Services { get; }
    public HashSet<CommandHandler> Handlers { get; }

    static CommanderBuilder()
    {
        CommanderModuleInitializer.Touch();
        CodeKeeper.AddFakeAction(static () => {
            CodeKeeper.KeepStatic(typeof(Proxies));

            // Configuration
            CodeKeeper.Keep<CommandHandlerMethodDef>();
            CodeKeeper.Keep<MethodCommandHandler<ICommand>>();
            CodeKeeper.Keep<InterfaceCommandHandler<ICommand>>();

            // Interceptors
            CodeKeeper.Keep<CommanderProxyCodeKeeper>();
            CodeKeeper.Keep<CommandServiceInterceptor>();

            // Stuff that might be forgotten
            var c = CodeKeeper.Get<ProxyCodeKeeper>();
            c.KeepAsyncMethod<Unit, ICommand<Unit>, CancellationToken>();
        });
    }

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
        services.TryAddSingleton<VersionGenerator<long>>(c => new ClockBasedVersionGenerator(c.Clocks().SystemClock));
        services.TryAddSingleton(_ => ChaosMaker.Default);

        // Commander, handlers, etc.
        services.AddSingleton<ICommander>(c => new Commander(c));
        services.AddSingleton(c => c.GetRequiredService<ICommander>().Hub);
        services.AddSingleton(c => new CommandHandlerRegistry(c));
        services.AddSingleton(_ => new CommandHandlerResolver.Options());
        services.AddSingleton(c => new CommandHandlerResolver(
            c.GetRequiredService<CommandHandlerResolver.Options>(), c));

        // Command services and their dependencies
        Services.AddSingleton(_ => CommandServiceInterceptor.Options.Default);
        Services.AddSingleton(c => new CommandServiceInterceptor(
            c.GetRequiredService<CommandServiceInterceptor.Options>(), c));

        // Default handlers
        services.AddSingleton(_ => new PreparedCommandHandler());
        AddHandlers<PreparedCommandHandler>();
        services.AddSingleton(c => new CommandTracer(c));
        AddHandlers<CommandTracer>();
        services.AddSingleton(c => new RpcCommandHandler(c));
        AddHandlers<RpcCommandHandler>();
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
            if (tCommand is null)
                continue;

            var method = implementationType.GetInterfaceMap(tInterface).TargetMethods.Single();
            var attr = MethodCommandHandler.GetAttribute(method);
            var isFilter = attr?.IsFilter ?? false;
            var order = priorityOverride ?? attr?.Priority ?? 0;
            AddHandler(InterfaceCommandHandler.New(serviceType, tCommand, isFilter, order));
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

            var handler = MethodCommandHandler.TryNew(serviceType, method, priorityOverride);
            if (handler is null)
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
        if (!typeof(ICommandService).IsAssignableFrom(implementationType))
            throw ActualLab.Internal.Errors.MustImplement<ICommandService>(implementationType, nameof(implementationType));
        if (!serviceType.IsAssignableFrom(implementationType))
            throw ActualLab.Internal.Errors.MustBeAssignableTo(implementationType, serviceType, nameof(implementationType));

        var descriptor = new ServiceDescriptor(serviceType,
            c => c.CommanderHub().NewProxy(c, implementationType),
            lifetime);
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
