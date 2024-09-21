namespace ActualLab.Rpc.Infrastructure;

public abstract class RpcCall(RpcMethodDef methodDef)
{
    protected Lock Lock = new();

    public abstract string DebugTypeName { get; }

    public readonly RpcMethodDef MethodDef = methodDef;

    public RpcHub Hub {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MethodDef.Hub;
    }

    public RpcServiceDef ServiceDef {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MethodDef.Service;
    }

    public long Id;
    public readonly bool NoWait = methodDef.NoWait; // Copying it here just b/c it's frequently accessed
}
