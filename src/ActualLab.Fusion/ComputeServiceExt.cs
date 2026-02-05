using ActualLab.Interception.Internal;

namespace ActualLab.Fusion;

/// <summary>
/// Extension methods for <see cref="IComputeService"/>.
/// </summary>
public static class ComputeServiceExt
{
    public static bool IsLocal(this IComputeService service)
        => service is not InterfaceProxy;
}
