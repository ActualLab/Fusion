using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;

namespace ActualLab.Interception;

public abstract class ArgumentListWriter
{
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public abstract T OnStruct<T>(int index);
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public abstract object? OnClass(Type type, int index);
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public abstract object? OnAny(Type type, int index, object? defaultValue);
}
