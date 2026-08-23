using ActualLab.Reflection;

namespace ActualLab.CommandR.Operations;

// This type lives in ActualLab.CommandR only because Operation does: it's produced and consumed
// exclusively by the Fusion layer, which references CommandR rather than the other way around.

/// <summary>
/// A recorded compute-method call to invalidate: everything needed to reproduce
/// the invalidation on this or another host.
/// </summary>
[DataContract]
public sealed record InvalidationCall(
    [property: DataMember(Order = 0)] TypeRef ServiceType,
    [property: DataMember(Order = 1)] string MethodName,
    [property: DataMember(Order = 2)] object?[] Arguments)
{
    public override string ToString()
        => $"{nameof(InvalidationCall)}({ServiceType.TypeName}.{MethodName}, {Arguments.Length} argument(s))";

    // Value equality over Arguments - the compiler-generated one compares the array by reference,
    // which would make the harvest-time dedupe useless.
    public bool Equals(InvalidationCall? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (other is null
            || !ServiceType.Equals(other.ServiceType)
            || !string.Equals(MethodName, other.MethodName, StringComparison.Ordinal)
            || Arguments.Length != other.Arguments.Length)
            return false;

        for (var i = 0; i < Arguments.Length; i++)
            if (!Equals(Arguments[i], other.Arguments[i]))
                return false;

        return true;
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(ServiceType);
        hashCode.Add(MethodName, StringComparer.Ordinal);
        foreach (var argument in Arguments)
            hashCode.Add(argument);
        return hashCode.ToHashCode();
    }
}
