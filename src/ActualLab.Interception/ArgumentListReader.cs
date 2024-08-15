using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;

namespace ActualLab.Interception;

public abstract class ArgumentListReader
{
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public abstract void OnStruct<T>(T item, int index);
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public abstract void OnClass(Type type, object? item, int index);
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public abstract void OnAny(Type type, object? item, int index);
}
