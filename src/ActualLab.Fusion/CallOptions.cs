namespace ActualLab.Fusion;

#pragma warning disable MA0062, CA2217

/// <summary>
/// Defines flags controlling how a compute method call is performed.
/// </summary>
[Flags]
public enum CallOptions
{
    GetExisting = 1,
    Invalidate = 2 + GetExisting,
    Capture = 4,
    InboundRpc = 8,
    CaptureInvalidation = 16 + GetExisting, // Capture the invalidating call into DeferredInvalidationContext
    RouteInvalidation = 32 + Invalidate, // Invalidate on the host that owns the value; routed like an ordinary call
}
#pragma warning restore MA0062
