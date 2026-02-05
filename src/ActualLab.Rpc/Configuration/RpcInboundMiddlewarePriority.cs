namespace ActualLab.Rpc;

/// <summary>
/// Well-known priority constants for ordering <see cref="IRpcMiddleware"/> instances in the pipeline.
/// </summary>
public static class RpcInboundMiddlewarePriority
{
    public const double Initial = 10_000;
    public const double ArgumentValidation = 1_000;
    public const double Final = 0;
}
