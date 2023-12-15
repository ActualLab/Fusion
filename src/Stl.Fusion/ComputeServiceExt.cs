using ActualLab.Interception.Internal;

namespace ActualLab.Fusion;

public static class ComputeServiceExt
{
    public static bool IsClient(this IComputeService service)
        => service is InterfaceProxy;
}
