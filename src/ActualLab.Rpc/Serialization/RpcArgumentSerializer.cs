using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Internal;

namespace ActualLab.Rpc.Serialization;

public abstract class RpcArgumentSerializer
{
    public static RpcArgumentSerializer Default { get; set; } = new RpcByteArgumentSerializer(ByteSerializer.Default);

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public abstract TextOrBytes Serialize(ArgumentList arguments, bool allowPolymorphism);
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public abstract void Deserialize(ref ArgumentList arguments, bool allowPolymorphism, TextOrBytes data);
}
