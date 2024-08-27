namespace ActualLab.Internal;

public static class Errors
{
    public static Exception MustBeClass(Type type, string? argumentName = null)
    {
        var message = $"'{type}' must be reference type (class).";
        return argumentName.IsNullOrEmpty()
            ? new InvalidOperationException(message)
            : new ArgumentOutOfRangeException(argumentName, message);
    }

    public static Exception MustBeInterface(Type type, string? argumentName = null)
    {
        var message = $"'{type}' must be interface type.";
        return argumentName.IsNullOrEmpty()
            ? new InvalidOperationException(message)
            : new ArgumentOutOfRangeException(argumentName, message);
    }

    public static Exception MustImplement<TExpected>(Type type, string? argumentName = null)
        => MustImplement(type, typeof(TExpected), argumentName);
    public static Exception MustImplement(Type type, Type expectedType, string? argumentName = null)
    {
        var message = $"'{type}' must implement '{expectedType}'.";
        return argumentName.IsNullOrEmpty()
            ? new InvalidOperationException(message)
            : new ArgumentOutOfRangeException(argumentName, message);
    }

    public static Exception MustNotImplement<TExpected>(Type type, string? argumentName = null)
        => MustNotImplement(type, typeof(TExpected), argumentName);
    public static Exception MustNotImplement(Type type, Type expectedType, string? argumentName = null)
    {
        var message = $"'{type}' must not implement '{expectedType}'.";
        return argumentName.IsNullOrEmpty()
            ? new InvalidOperationException(message)
            : new ArgumentOutOfRangeException(argumentName, message);
    }

    public static Exception MustBeAssignableTo<TExpected>(Type type, string? argumentName = null)
        => MustBeAssignableTo(type, typeof(TExpected), argumentName);
    public static Exception MustBeAssignableTo(Type type, Type mustBeAssignableToType, string? argumentName = null)
    {
        var message = $"'{type}' must be assignable to '{mustBeAssignableToType}'.";
        return argumentName.IsNullOrEmpty()
            ? new InvalidOperationException(message)
            : new ArgumentOutOfRangeException(argumentName, message);
    }

    public static Exception ImplementationNotFound(Type type)
        => new InvalidOperationException($"No implementation is found for type '{type}'.");

    public static Exception ExpressionDoesNotSpecifyAMember(string expression)
        => new ArgumentException($"Expression '{expression}' does not specify a member.");
    public static Exception UnexpectedMemberType(string memberType)
        => new InvalidOperationException($"Unexpected member type: {memberType}");

    public static Exception InvalidListFormat()
        => new FormatException("Invalid list format.");

    public static Exception CircularDependency<T>(T item)
        => new InvalidOperationException($"Circular dependency on {item} found.");

    public static Exception OptionIsNone()
        => new InvalidOperationException("Option is None.");
    public static Exception ApiOptionIsNone()
        => new InvalidOperationException("ApiOption is None.");
    public static Exception IsNone<T>()
        => new InvalidOperationException($"{typeof(T).GetName()} is None.");

    public static Exception TaskIsNotCompleted()
        => new InvalidOperationException("Task is expected to be completed at this point, but it's not.");
    public static Exception TaskIsFaultedButNoExceptionAvailable()
        => new InvalidOperationException("Task hasn't completed successfully but has no Exception.");

    public static Exception AsyncStateIsFinal()
        => new InvalidOperationException("AsyncState is expected to be non-final at this point, but it's final.");

    public static Exception PathIsRelative(string? paramName)
        => new ArgumentException("Path is relative.", paramName);

    public static Exception AlreadyDisposed()
        => new ObjectDisposedException("unknown", "The object is already disposed.");
    public static Exception AlreadyDisposed(Type type)
        => new ObjectDisposedException(type.GetName(), "The object is already disposed.");
    public static Exception AlreadyDisposed<T>()
        => AlreadyDisposed(typeof(T));

    public static Exception AlreadyDisposedOrDisposing()
        => new ObjectDisposedException("unknown", "The object is already disposed or disposing.");
    public static Exception AlreadyDisposedOrDisposing(Type type)
        => new ObjectDisposedException(type.GetName(), "The object is already disposed or disposing.");
    public static Exception AlreadyDisposedOrDisposing<T>()
        => AlreadyDisposedOrDisposing(typeof(T));

    public static Exception AlreadyStopped()
        => new InvalidOperationException("The process or task is already stopped.");

    public static Exception KeyAlreadyExists()
        => new InvalidOperationException("Specified key already exists.");
    public static Exception CollectionIsEmpty()
        => new InvalidOperationException("Collection is empty.");
    public static Exception CollectionIsFull()
        => new InvalidOperationException("Collection has reached its capacity.");

    public static Exception AlreadyInvoked(string methodName)
        => new InvalidOperationException($"'{methodName}' can be invoked just once.");
    public static Exception NotInvoked(string methodName)
        => new InvalidOperationException($"'{methodName}' must be invoked first.");
    public static Exception AlreadyInitialized(string? propertyName = null)
        => new InvalidOperationException(propertyName == null
            ? "Already initialized."
            : $"Property {propertyName} is already initialized.");
    public static Exception NotInitialized(string? propertyName = null)
        => new InvalidOperationException(propertyName == null
            ? "Not initialized."
            : $"Property {propertyName} is not initialized.");

    public static Exception AlreadyReadOnly<T>()
        => new InvalidOperationException($"{typeof(T).GetName()} is already transitioned to read-only state.");
    public static Exception AlreadyLocked()
        => new InvalidOperationException("The lock is already acquired by one of callers of the current method.");
    public static Exception AlreadyUsed()
        => new InvalidOperationException("The object was already used somewhere else.");
    public static Exception ProviderAlreadyRegistered<T>()
        => new InvalidOperationException($"The provider for {typeof(T).GetName()} is already registered.");

    public static Exception NoDefaultConstructor(Type type)
        => new InvalidOperationException($"Type '{type}' doesn't have a default constructor.");
    public static Exception NotSupported(string message)
        => new NotSupportedException(message);

    public static Exception UnprocessedBatchItem()
        => new InvalidOperationException("This batch item wasn't processed.");

    public static Exception InternalError(string message)
        => new InternalError(message);

    public static Exception Constraint(string message)
        => new InvalidOperationException(message);
    public static Exception Constraint<TTarget>(string message)
        => Constraint(typeof(TTarget), message);
    public static Exception Constraint(Type target, string message)
        => Constraint(target.GetName(), message);
    public static Exception Constraint(string target, string message)
        => Constraint($"Invalid {target}: {message}");

    public static Exception Format(string message)
        => new FormatException(message);
    public static Exception Format<TTarget>(string? value = null)
        => Format(typeof(TTarget), value);
    public static Exception Format(Type target, string? value = null)
        => Format(target.GetName(), value);
    public static Exception Format(string target, string? value)
#pragma warning disable IL2026 // We format string as JSON here, so no reflection needed
        => Format($"Invalid {target} format: {(value == null ? "null" : JsonFormatter.Format(value))}");
#pragma warning restore IL2026

    public static Exception Invalid7BitEncoded<TValue>()
        => Format($"Invalid 7-bit encoded {typeof(TValue).GetName()}");
}
