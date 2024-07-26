using ActualLab.Interception;

namespace ActualLab.Rpc.Infrastructure;

public record RpcInterceptorOptions : Interceptor.Options
{
    public static RpcInterceptorOptions Default { get; set; } = new();
}
