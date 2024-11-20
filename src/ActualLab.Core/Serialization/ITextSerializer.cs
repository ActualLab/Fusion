using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;

namespace ActualLab.Serialization;

public interface ITextSerializer : IByteSerializer
{
    public bool PreferStringApi { get; }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public object? Read(string data, Type type);
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public object? Read(ReadOnlyMemory<char> data, Type type);
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public string Write(object? value, Type type);
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public void Write(TextWriter textWriter, object? value, Type type);

    public new ITextSerializer<T> ToTyped<T>(Type? serializedType = null);
}

public interface ITextSerializer<T> : IByteSerializer<T>
{
    public bool PreferStringApi { get; }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public T Read(string data);
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public T Read(ReadOnlyMemory<char> data);
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public string Write(T value);
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public void Write(TextWriter textWriter, T value);
}
