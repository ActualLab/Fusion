using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Internal;
using Errors = ActualLab.Rpc.Internal.Errors;

namespace ActualLab.Rpc.Serialization;

public abstract class RpcArgumentSerializer
{
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public abstract ReadOnlyMemory<byte> Serialize(ArgumentList arguments, bool allowPolymorphism, int sizeHint);
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public abstract void Deserialize(ref ArgumentList arguments, bool allowPolymorphism, ReadOnlyMemory<byte> data);

    public static Type RequireNonAbstract(Type type)
    {
        if (type.IsAbstract)
            throw Errors.CannotSerializeAbstractType(type);

        return type;
    }
}
