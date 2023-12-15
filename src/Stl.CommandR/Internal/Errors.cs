namespace ActualLab.CommandR.Internal;

public static class Errors
{
    public static Exception CommandHandlerSetMustBeRegisteredAsInstance()
        => new InvalidOperationException("HashSet<CommandHandler> should be registered as instance.");
    public static Exception CommandHandlerSetIsNotRegistered()
        => new InvalidOperationException("HashSet<CommandHandler> instance is not registered.");
    public static Exception CommandResultTypeMismatch(Type expectedType, Type actualType)
        => new InvalidOperationException($"Command result type mismatch: expected '{expectedType}', got '{actualType}'");

    public static Exception NoCurrentCommandContext()
        => new InvalidOperationException("CommandContext.Current is null - no command is running.");
    public static Exception DirectCommandHandlerCallsAreNotAllowed()
        => new NotSupportedException(
            "Direct command handler calls on command service proxies are not allowed. Use ICommander.Call(...) instead.");

    public static Exception NoHandlerFound(Type commandType)
        => new InvalidOperationException($"No handler is found for command '{commandType}'.");
    public static Exception NoFinalHandlerFound(Type commandType)
        => new InvalidOperationException($"No final handler is found for command '{commandType}'.");
    public static Exception MultipleNonFilterHandlers(Type commandType)
        => new InvalidOperationException($"Multiple non-filter handlers are found for '{commandType}'.");

    public static Exception InvalidCommandHandlerMethod(MethodInfo method)
        => new InvalidOperationException($"Invalid command handler method: {method}.");
    public static Exception CommandHandlerMethodMustBeInstanceMethod(MethodInfo method)
        => new InvalidOperationException($"Command handler method must be instance method (non-static): '{method}'.");
    public static Exception CommandHandlerMethodMustReturnTask(MethodInfo method)
        => new InvalidOperationException($"Command handler method must return Task or Task<T>: {method}.");
    public static Exception WrongCommandHandlerMethodArgumentCount(MethodInfo method)
        => new InvalidOperationException($"Command handler method must have at least 2 arguments: command and CancellationToken.");
    public static Exception WrongCommandHandlerMethodArguments(MethodInfo method)
        => new InvalidOperationException($"Wrong command handler method arguments: {method}.");
    public static Exception WrongInterceptedCommandHandlerMethodSignature(MethodInfo method)
        => new InvalidOperationException(
            "Intercepted command handler method must be " +
            "public or protected, virtual, and have exactly 2 arguments: " +
            $"command and CancellationToken: {method}.");
    public static Exception OnlyInterceptedCommandHandlersAllowed(Type type)
        => new InvalidOperationException(
            $"Type '{type}' is registered as a service with intercepted command handlers, " +
            "so it can't declare regular (e.g. interface) command handlers.");

    public static Exception CommandMustImplementICommandOfTResult(Type commandType)
        => new InvalidOperationException($"Command type '{commandType}' must implement {typeof(ICommand<>)}.");

    public static Exception BackendCommandMustBeStartedOnBackend()
        => new InvalidOperationException("Backend command must be started on backend.");

    public static Exception LocalCommandHasNoHandler()
        => new InvalidOperationException("LocalCommand.Handler is null.");
}
