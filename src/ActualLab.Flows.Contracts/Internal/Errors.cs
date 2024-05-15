namespace ActualLab.Flows.Internal;

public static class Errors
{
    public static Exception StepNotFound(Type flowType, string stepName)
        => new InvalidOperationException($"Flow '{flowType.GetName()}' has no implementation for step '{stepName}'.");

    public static Exception UnsupportedEvent(Type flowType, string stepName, Type eventType)
        => new InvalidOperationException(
            $"Flow '{flowType.GetName()}' has no implementation for step '{stepName}' which accepts {eventType.GetName()} event.");
}
