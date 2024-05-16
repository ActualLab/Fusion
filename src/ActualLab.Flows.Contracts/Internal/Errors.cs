namespace ActualLab.Flows.Internal;

public static class Errors
{
    public static Exception NoStepImplementation(Type flowType, string step)
        => new InvalidOperationException($"Flow '{flowType.GetName()}' has no implementation for step '{step}'.");

    public static Exception NoEvent(Type flowType, string step, Type eventType)
        => new InvalidOperationException(
            $"Flow '{flowType.GetName()}' requires {eventType.GetName()} event on step '{step}'.");

    public static Exception UnsupportedEvent(Type flowType, string step, Type eventType)
        => new InvalidOperationException(
            $"Flow '{flowType.GetName()}' has no implementation for step '{step}' which accepts {eventType.GetName()} event.");
}
