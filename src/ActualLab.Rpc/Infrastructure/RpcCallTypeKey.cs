namespace ActualLab.Rpc.Infrastructure;

public readonly struct RpcCallTypeKey(byte callTypeId, Type type) : IEquatable<RpcCallTypeKey>
{
    public readonly byte CallTypeId = callTypeId;
    public readonly Type Type = type;

    public void Deconstruct(out byte callTypeId, out Type type)
    {
        callTypeId = CallTypeId;
        type = Type;
    }

    // Equality

    public bool Equals(RpcCallTypeKey other)
        => Type == other.Type && CallTypeId == other.CallTypeId;

    public override bool Equals(object? obj)
        => obj is RpcCallTypeKey other && Equals(other);

    public override int GetHashCode()
        => Type.GetHashCode() + CallTypeId;
}
