namespace ActualLab.Fusion.Blazor.Internal;

/// <summary>
/// Defines how computed state computation is dispatched relative to the Blazor dispatcher.
/// </summary>
public enum ComputedStateDispatchMode
{
    None,
    Dispatch,
    DispatchWithExecutionContextFlow,
}
