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
}
#pragma warning restore MA0062
