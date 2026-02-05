using ActualLab.CommandR.Internal;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.CommandR.Configuration;

/// <summary>
/// Defines a command handler that invokes a specific method via reflection.
/// </summary>
public interface IMethodCommandHandler : ICommandHandler
{
    public Type ServiceType { get; }
    public MethodInfo Method { get; }
    public ParameterInfo[] Parameters { get; }
    public Type[] ParameterTypes { get; }
}

/// <summary>
/// A <see cref="CommandHandler"/> that invokes a command by calling a specific
/// method on a resolved service instance.
/// </summary>
public sealed record MethodCommandHandler<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TCommand>
    ([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type ServiceType,
    MethodInfo Method, bool IsFilter = false, double Priority = 0
    ) : CommandHandler<TCommand>($"{ServiceType.GetName(true, true)}.{Method.Name}", IsFilter, Priority),
        IMethodCommandHandler
    where TCommand : class, ICommand
{
    public ParameterInfo[] Parameters => field ??= Method.GetParameters();
    public Type[] ParameterTypes => field ??= Parameters.Select(p => p.ParameterType).ToArray();

    public override Type GetHandlerServiceType()
        => ServiceType;

    public override object GetHandlerService(ICommand command, CommandContext context)
        => context.Services.GetRequiredService(ServiceType);

    public override Task Invoke(
        ICommand command, CommandContext context,
        CancellationToken cancellationToken)
    {
        var services = context.Services;
        var service = GetHandlerService(command, context);
        var parameters = Parameters;
        var arguments = new object[parameters.Length];
        arguments[0] = command;
        // ReSharper disable once HeapView.BoxingAllocation
        arguments[^1] = cancellationToken;
        for (var i = 1; i < parameters.Length - 1; i++) {
            var p = parameters[i];
            var value = GetParameterValue(p, context, services);
            arguments[i] = value;
        }

        // Pass setup to the RpcInterceptor
        var rpcOutboundCallSetup = context.Items.KeylessGet<RpcOutboundCallSetup>();
        var rpcOutboundCallSetupScope = rpcOutboundCallSetup?.Activate() ?? default;
        try {
            return (Task)Method.Invoke(service, arguments)!;
        }
        catch (TargetInvocationException tie) {
            if (tie.InnerException is not null)
                throw tie.InnerException;

            throw;
        }
        finally {
            if (rpcOutboundCallSetup is not null)
                rpcOutboundCallSetupScope.Dispose();
        }
    }

    public override string ToString() => base.ToString();

    // This record relies on reference-based equality
    public bool Equals(MethodCommandHandler<TCommand>? other) => ReferenceEquals(this, other);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);

    private static object GetParameterValue(ParameterInfo parameter, CommandContext context, IServiceProvider services)
    {
        if (parameter.ParameterType == typeof(CommandContext))
            return context;
        if (parameter.HasDefaultValue)
            return services.GetService(parameter.ParameterType) ?? parameter.DefaultValue!;
        return services.GetRequiredService(parameter.ParameterType);
    }
}

/// <summary>
/// Factory methods for creating <see cref="MethodCommandHandler{TCommand}"/> instances
/// from methods annotated with <see cref="CommandHandlerAttribute"/>.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume all command handling code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume all command handling code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "We assume all command handling code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume all command handling code is preserved")]
public static class MethodCommandHandler
{
    private static readonly MethodInfo CreateMethod =
        typeof(MethodCommandHandler)
            .GetMethod(nameof(Create), BindingFlags.Static | BindingFlags.NonPublic)!;

    public static CommandHandler New(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        MethodInfo method, double? priorityOverride = null)
        => TryNew(serviceType, method, priorityOverride)
            ?? throw Errors.InvalidCommandHandlerMethod(method);

    public static CommandHandler? TryNew(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        MethodInfo method,
        double? priorityOverride = null)
    {
        var attr = GetAttribute(method);
        if (attr is null)
            return null;

        var isFilter = attr.IsFilter;
        var order = priorityOverride ?? attr.Priority;

        if (method.IsStatic)
            throw Errors.CommandHandlerMethodMustBeInstanceMethod(method);

        var tHandlerResult = method.ReturnType;
        if (!typeof(Task).IsAssignableFrom(tHandlerResult))
            throw Errors.CommandHandlerMethodMustReturnTask(method);

        var parameters = method.GetParameters();
        if (parameters.Length < 2)
            throw Errors.WrongCommandHandlerMethodArgumentCount(method);

        // Checking command parameter
        var pCommand = parameters[0];
        if (!typeof(ICommand).IsAssignableFrom(pCommand.ParameterType))
            throw Errors.WrongCommandHandlerMethodArguments(method);
        if (tHandlerResult.IsGenericType && tHandlerResult.GetGenericTypeDefinition() == typeof(Task<>)) {
            var tHandlerResultTaskArgument = tHandlerResult.GetGenericArguments().Single();
            var tGenericCommandType = typeof(ICommand<>).MakeGenericType(tHandlerResultTaskArgument);
            if (!tGenericCommandType.IsAssignableFrom(pCommand.ParameterType))
                throw Errors.WrongCommandHandlerMethodArguments(method);
        }

        // Checking CancellationToken parameter
        var pCancellationToken = parameters[^1];
        if (typeof(CancellationToken) != pCancellationToken.ParameterType)
            throw Errors.WrongCommandHandlerMethodArguments(method);

        return (CommandHandler)CreateMethod
            .MakeGenericMethod(pCommand.ParameterType)
            .Invoke(null, [serviceType, method, isFilter, order])!;
    }

    public static CommandHandlerAttribute? GetAttribute(MethodInfo method)
        => method.GetAttribute<CommandHandlerAttribute>(true, true);

    private static MethodCommandHandler<TCommand> Create<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TCommand>(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType,
        MethodInfo method, bool isFilter, double priority)
        where TCommand : class, ICommand
        => new(serviceType, method, isFilter, priority);
}
