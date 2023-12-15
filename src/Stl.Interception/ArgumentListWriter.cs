using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;

namespace ActualLab.Interception;

public abstract class ArgumentListWriter
{
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public abstract T OnStruct<T>(int index);
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public abstract object? OnObject(Type type, int index);
}
