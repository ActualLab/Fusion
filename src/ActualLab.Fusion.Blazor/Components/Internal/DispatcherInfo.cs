using Microsoft.AspNetCore.Components;

namespace ActualLab.Fusion.Blazor.Internal;

/// <summary>
/// Detects whether the Blazor dispatcher supports ExecutionContext flow,
/// which is needed for propagating <see cref="Computed.Current"/> into dispatched callbacks.
/// </summary>
public static class DispatcherInfo
{
    // MAUI's Blazor doesn't flow ExecutionContext into InvokeAsync,
    // so we must detect & flow it in case it doesn't flow, otherwise
    // Computed.Current won't be available there.
    private static volatile int _executionContextFlow;

    public static bool IsExecutionContextFlowSupported(ComponentBase anyComponent)
    {
        var executionContextFlow = (ExecutionContextFlow)_executionContextFlow;
        switch (executionContextFlow) {
        case ExecutionContextFlow.Supported:
            return true;
        case ExecutionContextFlow.Unsupported:
            return false;
        }

        var dispatcher = anyComponent.GetDispatcher();
        var dispatcherTypeName = dispatcher.GetType().Name;

        // NullRendered is used in WASM,
        // RendererSynchronizationContextDispatcher is used in Blazor Server
        var isFlowSupported = dispatcherTypeName is "NullDispatcher" or "RendererSynchronizationContextDispatcher";
        executionContextFlow = isFlowSupported
            ? ExecutionContextFlow.Supported
            : ExecutionContextFlow.Unsupported;
        Interlocked.Exchange(ref _executionContextFlow, (int)executionContextFlow);
        return isFlowSupported;
    }

    // Nested types

    private enum ExecutionContextFlow
    {
        Unknown = 0,
        Supported,
        Unsupported,
    }
}
