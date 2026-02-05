using ActualLab.CommandR.Operations;

namespace ActualLab.Fusion.Operations.Internal;

/// <summary>
/// Factory methods for Fusion operations-related exceptions.
/// </summary>
public static class Errors
{
    public static Exception OperationHasNoCommand()
        => new InvalidOperationException("Operation object has no Command.");
    public static Exception OperationHasNoCommand(string paramName)
        => new ArgumentException("Provided IOperation object has no Command.", paramName);
    public static Exception OperationScopeIsAlreadyClosed()
        => new InvalidOperationException("Operation scope is already closed (committed or rolled back).");

    public static Exception NewOperationScopeIsRequestedFromInvalidationCode()
        => new InvalidOperationException($"New {nameof(IOperationScope)} is requested from the invalidation code.");
    public static Exception WrongOperationScopeType(Type expectedScopeType, Type? actualScopeType)
        => new InvalidOperationException($"{expectedScopeType} is requested, but {actualScopeType?.GetName() ?? "null"} is used.");
}
