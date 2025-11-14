namespace ActualLab.Rpc.Infrastructure;

public abstract class RpcCall(RpcMethodDef? methodDef)
{
    public object Lock {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this;
    }

    public abstract string DebugTypeName { get; }

    public RpcMethodDef MethodDef = methodDef ?? throw new ArgumentNullException(nameof(methodDef));

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
