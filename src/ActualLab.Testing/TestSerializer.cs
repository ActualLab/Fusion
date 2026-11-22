using Xunit.Abstractions;

namespace ActualLab.Testing;

/// <summary>
/// A serializer <see cref="SerializationTestExt"/> passes values through;
/// register it with <see cref="SerializationTestExt.Register"/>.
/// </summary>
public sealed record TestSerializer(string Name, Func<IByteSerializer> SerializerFactory)
{
    public bool MustPassUniSerialized { get; init; } = true;
    public bool MustPassTypeDecorating { get; init; } = true;
    public bool MustPassTypeEvolution { get; init; } = true;
    // The pass-through helpers skip this serializer for values whose type it rejects
    public Func<Type, bool> CanSerialize { get; init; } = static _ => true;

    public T2 PassThrough<T1, T2>(T1 value, ITestOutputHelper? output = null)
    {
        var serializer = SerializerFactory.Invoke();
        T2 result;
        if (serializer is ITextSerializer textSerializer) {
            var text = textSerializer.Write(value, typeof(T1));
            output?.WriteLine($"{Name}: {text}");
            result = (T2)textSerializer.Read(text, typeof(T2))!;
        }
        else {
            using var buffer = serializer.Write(value, typeof(T1));
            var bytes = buffer.WrittenMemory.ToArray();
            output?.WriteLine($"{Name}: {bytes.AsByteString()}");
            result = (T2)serializer.Read(bytes, typeof(T2), out _)!;
        }
        output?.WriteLine($"{Name} -> {result}");
        return result;
    }

    public TestSerializer AsTypeDecorating()
        => new($"{Name}-TypeDecorating", () => SerializerFactory.Invoke() switch {
            ITextSerializer textSerializer => new TypeDecoratingTextSerializer(textSerializer),
            var byteSerializer => new TypeDecoratingByteSerializer(byteSerializer),
        }) {
            MustPassUniSerialized = false,
            MustPassTypeDecorating = false,
            MustPassTypeEvolution = false,
        };
}
