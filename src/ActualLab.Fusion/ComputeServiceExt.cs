using ActualLab.Interception.Internal;

namespace ActualLab.Fusion;

public static class ComputeServiceExt
{
    public static bool IsLocal(this IComputeService service)
        => service is not InterfaceProxy;
}
