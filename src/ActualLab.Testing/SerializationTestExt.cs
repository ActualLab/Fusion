using System.Text;
using AwesomeAssertions;
using MessagePack;
using Xunit.Abstractions;

namespace ActualLab.Testing;

/// <summary>
/// Extension methods for testing serialization round-trips through every serializer in
/// <see cref="Serializers"/>: System.Text.Json and MessagePack are built in, and test projects
/// register the serializers of the format packages they reference (MemoryPack, Newtonsoft.Json, etc.).
/// </summary>
public static class SerializationTestExt
{
#if NET9_0_OR_GREATER
    private static readonly Lock Lock = new();
#else
    private static readonly object Lock = new();
#endif

    public static JsonSerializerOptions SystemJsonOptions { get; set; }
        = MemberwiseCloner.Invoke(SystemJsonSerializer.DefaultOptions);
    public static bool UseSystemJsonSerializer { get; set; } = true;
    public static bool UseMessagePackSerializer { get; set; } = true;
    public static ImmutableList<TestSerializer> Serializers { get; set; } = ImmutableList<TestSerializer>.Empty;

    // Idempotent: a serializer with the same name replaces the registered one
    public static void Register(TestSerializer serializer)
    {
        lock (Lock) {
            var serializers = Serializers.RemoveAll(x => string.Equals(x.Name, serializer.Name, StringComparison.Ordinal));
            Serializers = serializers.Add(serializer);
        }
    }

    public static T AssertPassesThroughAllSerializers<T>(this T value, ITestOutputHelper? output = null)
        => PassThroughSerializerMatrix(value, v => v.Should().Be(value), output);

    public static T AssertPassesThroughAllSerializers<T>(this T value, Action<T> assertion, ITestOutputHelper? output = null)
        => PassThroughSerializerMatrix(value, assertion, output);

    public static T AssertPassesThroughAllSerializers<T>(this T value, Action<T, T> assertion, ITestOutputHelper? output = null)
        => PassThroughSerializerMatrix(value, v => assertion.Invoke(v, value), output);

    public static T2 AssertPassesThroughAllSerializers<T1, T2>(this T1 value, Action<T2, T1> assertion, ITestOutputHelper? output = null)
    {
        var result = default(T2)!;
        foreach (var serializer in GetTypeEvolutionSerializers(typeof(T1))) {
            result = serializer.PassThrough<T1, T2>(value, output);
            assertion.Invoke(result, value);
        }
        return result;
    }

    public static T PassThroughAllSerializers<T>(this T value, ITestOutputHelper? output = null)
        => PassThroughSerializerMatrix(value, static _ => { }, output);

    // Per-serializer pass-throughs

    public static T PassThrough<T>(this T value, TestSerializer serializer, ITestOutputHelper? output = null)
        => serializer.PassThrough<T, T>(value, output);

    public static T PassThroughSystemJsonSerializer<T>(this T value, ITestOutputHelper? output = null)
    {
        if (!UseSystemJsonSerializer)
            return value;

        var s = new SystemJsonSerializer(SystemJsonOptions).ToTyped<T>();
        var json = s.Write(value);
        output?.WriteLine($"SystemJsonSerializer: {json}");
        value = s.Read(json);

        using var buffer = new ArrayPoolBuffer<byte>(ArrayPools.SharedBytePool, mustClear: false);
        s.Write(buffer, value);
        var bytes = buffer.WrittenMemory;
        var json2 = Encoding.UTF8.GetDecoder().Convert(bytes.Span);
        json2.Should().Be(json);
        var v0 = s.Read(bytes, out _);
        var json3 = s.Write(v0);
        json3.Should().Be(json);

        var v1 = SystemJsonSerialized.New(value);
        output?.WriteLine($"SystemJsonSerialized: {v1.Data}");
        value = SystemJsonSerialized.New<T>(v1.Data).Value;

        var v2 = TypeDecoratingSystemJsonSerialized.New<TypeSchema.Any, T>(value);
        output?.WriteLine($"TypeDecoratingSystemJsonSerialized: {v2.Data}");
        value = TypeDecoratingSystemJsonSerialized.New<TypeSchema.Any, T>(v2.Data).Value;

        output?.WriteLine($"PassThroughSystemJsonSerializer -> {value}");
        return value;
    }

    public static T PassThroughMessagePackByteSerializer<T>(this T value, ITestOutputHelper? output = null)
    {
        if (!UseMessagePackSerializer)
            return value;

        var s = new MessagePackByteSerializer().ToTyped<T>();
        using var buffer = s.Write(value);
        var v0 = buffer.WrittenMemory.ToArray();
        var json0 = MessagePackSerializer.ConvertToJson(v0, MessagePackByteSerializer.DefaultOptions);
        output?.WriteLine($"MessagePackByteSerializer: {json0} as {v0.AsByteString()}");
        value = s.Read(v0, out _);

        var v1 = MessagePackSerialized.New(value);
        output?.WriteLine($"MessagePackSerialized: {v1.Data.AsByteString()}");
        value = MessagePackSerialized.New<T>(v1.Data).Value;

        var v2 = TypeDecoratingMessagePackSerialized.New<TypeSchema.Any, T>(value);
        output?.WriteLine($"TypeDecoratingMessagePackSerialized: {v2.Data.AsByteString()}");
        value = TypeDecoratingMessagePackSerialized.New<TypeSchema.Any, T>(v2.Data).Value;

        output?.WriteLine($"PassThroughMessagePackByteSerializer -> {value}");
        return value;
    }

    public static T PassThroughUniSerialized<T>(this T value, ITestOutputHelper? output = null)
    {
        var v = UniSerialized.New(value);
        v = v.PassThroughSystemJsonSerializer(output);
        v = v.PassThroughMessagePackByteSerializer(output);
        foreach (var serializer in Serializers.Where(x => x.MustPassUniSerialized && x.CanSerialize(typeof(T))))
            v = serializer.PassThrough<UniSerialized<T>, UniSerialized<T>>(v, output);
        return v.Value;
    }

    public static T PassThroughTypeDecoratingUniSerialized<T>(this T value, ITestOutputHelper? output = null)
    {
        var v = TypeDecoratingUniSerialized.New<TypeSchema.Any, object?>(value);
        v = v.PassThroughSystemJsonSerializer(output);
        v = v.PassThroughMessagePackByteSerializer(output);
        foreach (var serializer in Serializers.Where(x => x.MustPassUniSerialized && x.CanSerialize(typeof(T))))
            v = serializer.PassThrough<TypeDecoratingUniSerialized<TypeSchema.Any, object?>,
                TypeDecoratingUniSerialized<TypeSchema.Any, object?>>(v, output);
        return (T)v.Value!;
    }

    public static T PassThroughTypeDecoratingSerializers<T>(this T value, ITestOutputHelper? output = null)
    {
        var textSerializer = new TypeDecoratingTextSerializer(new SystemJsonSerializer(SystemJsonOptions));
        var json = textSerializer.Write(value, typeof(object));
        output?.WriteLine($"TypeDecoratingTextSerializer: {json}");
        value = (T)textSerializer.Read<object>(json);

        foreach (var serializer in Serializers.Where(x => x.MustPassTypeDecorating && x.CanSerialize(typeof(T))))
            value = serializer.AsTypeDecorating().PassThrough<T, T>(value, output);
        return value;
    }

    // Private methods

    private static T PassThroughSerializerMatrix<T>(T value, Action<T> assertion, ITestOutputHelper? output)
    {
        var v = value;
        v = v.PassThroughSystemJsonSerializer(output);
        assertion.Invoke(v);
        v = v.PassThroughMessagePackByteSerializer(output);
        assertion.Invoke(v);
        foreach (var serializer in Serializers.Where(x => x.CanSerialize(typeof(T)))) {
            v = serializer.PassThrough<T, T>(v, output);
            assertion.Invoke(v);
        }
        v = v.PassThroughTypeDecoratingSerializers(output);
        assertion.Invoke(v);
        v = v.PassThroughUniSerialized(output);
        assertion.Invoke(v);
        v = v.PassThroughTypeDecoratingUniSerialized(output);
        assertion.Invoke(v);
        return v;
    }

    private static IEnumerable<TestSerializer> GetTypeEvolutionSerializers(Type type)
    {
        if (UseMessagePackSerializer)
            yield return new TestSerializer("MessagePack", () => MessagePackByteSerializer.Default);
        if (UseSystemJsonSerializer)
            yield return new TestSerializer("SystemJson", () => new SystemJsonSerializer(SystemJsonOptions));
        foreach (var serializer in Serializers.Where(x => x.MustPassTypeEvolution && x.CanSerialize(type)))
            yield return serializer;
    }
}
