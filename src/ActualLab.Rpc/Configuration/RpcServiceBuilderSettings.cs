namespace ActualLab.Rpc;

public record RpcServiceBuilderSettings(RpcBuilder Rpc)
{
    public Type BaseType { get; init; } = typeof(IRpcService);

    protected virtual void Inject()
    {

    }
}
