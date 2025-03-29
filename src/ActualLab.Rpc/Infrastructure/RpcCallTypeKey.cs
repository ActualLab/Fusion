namespace ActualLab.Rpc.Infrastructure;

public readonly struct RpcCallTypeKey(byte callTypeId, Type callResultType) : IEquatable<RpcCallTypeKey>
{
    public readonly byte CallTypeId = callTypeId;
    public readonly Type CallResultType = callResultType;

    public void Deconstruct(out byte callTypeId, out Type type)
    {
        callTypeId = CallTypeId;
        type = CallResultType;
    }

    // Equality

    public bool Equals(RpcCallTypeKey other)
        => ReferenceEquals(CallResultType, other.CallResultType) && CallTypeId == other.CallTypeId;

    public override bool Equals(object? obj)
        => obj is RpcCallTypeKey other && Equals(other);

    public override int GetHashCode()
        => CallResultType.GetHashCode() + CallTypeId;
}
