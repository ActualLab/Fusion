namespace ActualLab.Rpc;

/// <summary>
/// Base settings class for customizing <see cref="RpcServiceBuilder"/> behavior.
/// </summary>
public record RpcServiceBuilderSettings(RpcBuilder Rpc)
{
    public Type BaseType { get; init; } = typeof(IRpcService);

    protected virtual void Inject()
    {

    }
}
