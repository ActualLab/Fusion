using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Serialization;

public interface ITextSerializer : IByteSerializer
{
    public bool PreferStringApi { get; }

    public object? Read(string data, Type type);
    public object? Read(ReadOnlyMemory<char> data, Type type);
    public string Write(object? value, Type type);
    public void Write(TextWriter textWriter, object? value, Type type);

    public new ITextSerializer<T> ToTyped<T>(Type? serializedType = null);
}

public interface ITextSerializer<T> : IByteSerializer<T>
{
    public bool PreferStringApi { get; }

    public T Read(string data);
    public T Read(ReadOnlyMemory<char> data);
    public string Write(T value);
    public void Write(TextWriter textWriter, T value);
}
