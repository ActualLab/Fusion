using System.Text;
using AwesomeAssertions;
using Newtonsoft.Json;
using MessagePack;
using Xunit.Abstractions;

namespace ActualLab.Testing;

/// <summary>
/// Extension methods for testing serialization round-trips through multiple
/// serializer implementations (System.Text.Json, Newtonsoft.Json, MessagePack,
/// MemoryPack, and type-decorating variants).
/// </summary>
public static class SerializationTestExt
{
    public static JsonSerializerOptions SystemJsonOptions { get; set; }
    public static JsonSerializerSettings NewtonsoftJsonSettings { get; set; }
    public static bool UseSystemJsonSerializer { get; set; } = true;
    public static bool UseNewtonsoftJsonSerializer { get; set; } = true;
    public static bool UseMessagePackSerializer { get; set; } = true;
    public static bool UseMemoryPackSerializer { get; set; } = true;
    public static bool UseNerdbankMessagePackSerializer { get; set; }

    static SerializationTestExt()
    {
        SystemJsonOptions = MemberwiseCloner.Invoke(SystemJsonSerializer.DefaultOptions);
        NewtonsoftJsonSettings = MemberwiseCloner.Invoke(NewtonsoftJsonSerializer.DefaultSettings);
        NewtonsoftJsonSettings.Formatting = Formatting.Indented;
    }

    public static T AssertPassesThroughAllSerializers<T>(this T value, ITestOutputHelper? output = null)
    {
        var v = value;
        v = v.PassThroughSystemJsonSerializer(output);
        v.Should().Be(value);
        v = v.PassThroughNewtonsoftJsonSerializer(output);
        v.Should().Be(value);
        v = v.PassThroughMessagePackByteSerializer(output);
        v.Should().Be(value);
        v = v.PassThroughMemoryPackByteSerializer(output);
        v.Should().Be(value);
        v = v.PassThroughNerdbankMessagePackByteSerializer(output);
        v.Should().Be(value);
        v = v.PassThroughTypeDecoratingTextSerializer(output);
        v.Should().Be(value);
        v = v.PassThroughTypeDecoratingByteSerializer(output);
        v.Should().Be(value);
        return v;
    }

    public static T AssertPassesThroughAllSerializers<T>(this T value, Action<T> assertion, ITestOutputHelper? output = null)
    {
        var v = value;
        v = v.PassThroughSystemJsonSerializer(output);
        assertion.Invoke(v);
        v = v.PassThroughNewtonsoftJsonSerializer(output);
        assertion.Invoke(v);
        v = v.PassThroughMessagePackByteSerializer(output);
        assertion.Invoke(v);
        v = v.PassThroughMemoryPackByteSerializer(output);
        assertion.Invoke(v);
        v = v.PassThroughNerdbankMessagePackByteSerializer(output);
        assertion.Invoke(v);
        v = v.PassThroughTypeDecoratingTextSerializer(output);
        assertion.Invoke(v);
        v = v.PassThroughTypeDecoratingByteSerializer(output);
        assertion.Invoke(v);
        v = v.PassThroughUniSerialized(output);
        assertion.Invoke(v);
        v = v.PassThroughTypeDecoratingUniSerialized(output);
        assertion.Invoke(v);
        return v;
    }

    public static T AssertPassesThroughAllSerializers<T>(this T value, Action<T, T> assertion, ITestOutputHelper? output = null)
    {
        var v = value;
        v = v.PassThroughSystemJsonSerializer(output);
        assertion.Invoke(v, value);
        v = v.PassThroughNewtonsoftJsonSerializer(output);
        assertion.Invoke(v, value);
        v = v.PassThroughMessagePackByteSerializer(output);
        assertion.Invoke(v, value);
        v = v.PassThroughMemoryPackByteSerializer(output);
        assertion.Invoke(v, value);
        v = v.PassThroughNerdbankMessagePackByteSerializer(output);
        assertion.Invoke(v, value);
        v = v.PassThroughTypeDecoratingTextSerializer(output);
        assertion.Invoke(v, value);
        v = v.PassThroughTypeDecoratingByteSerializer(output);
        assertion.Invoke(v, value);
        v = v.PassThroughUniSerialized(output);
        assertion.Invoke(v, value);
        v = v.PassThroughTypeDecoratingUniSerialized(output);
        assertion.Invoke(v, value);
        return v;
    }

    public static T2 AssertPassesThroughMemoryPackSerializer<T1, T2>(this T1 value, Action<T2, T1> assertion,
        ITestOutputHelper? output = null)
    {
        var mp1 = MemoryPackSerialized.New(value);
        output?.WriteLine($"MemoryPackSerializer: {mp1.Data.AsByteString()}");
        var mp2 = MemoryPackSerialized.New<T2>(mp1.Data);
        assertion.Invoke(mp2.Value, value);
        return mp2.Value;
    }

    public static T2 AssertPassesThroughAllSerializers<T1, T2>(this T1 value, Action<T2, T1> assertion, ITestOutputHelper? output = null)
    {
        var mp1 = MemoryPackSerialized.New(value);
        output?.WriteLine($"MemoryPackSerializer: {mp1.Data.AsByteString()}");
        var mp2 = MemoryPackSerialized.New<T2>(mp1.Data);
        assertion.Invoke(mp2.Value, value);

        var msp1 = MessagePackSerialized.New(value);
        output?.WriteLine($"MessagePackSerializer: {msp1.Data.AsByteString()}");
        var msp2 = MessagePackSerialized.New<T2>(msp1.Data);
        assertion.Invoke(msp2.Value, value);

        var ns1 = NewtonsoftJsonSerialized.New(value);
        output?.WriteLine($"NewtonsoftJsonSerializer: {ns1.Data}");
        var ns2 = NewtonsoftJsonSerialized.New<T2>(ns1.Data);
        assertion.Invoke(ns2.Value, value);

        var sj1 = SystemJsonSerialized.New(value);
        output?.WriteLine($"SystemJsonSerializer: {sj1.Data}");
        var sj2 = SystemJsonSerialized.New<T2>(sj1.Data);
        assertion.Invoke(sj2.Value, value);

        return mp2.Value;
    }

    public static T PassThroughAllSerializers<T>(this T value, ITestOutputHelper? output = null)
    {
        var v = value;
        v = v.PassThroughSystemJsonSerializer(output);
        v = v.PassThroughNewtonsoftJsonSerializer(output);
        v = v.PassThroughMessagePackByteSerializer(output);
        v = v.PassThroughMemoryPackByteSerializer(output);
        v = v.PassThroughNerdbankMessagePackByteSerializer(output);
        v = v.PassThroughTypeDecoratingTextSerializer(output);
        v = v.PassThroughTypeDecoratingByteSerializer(output);
        v = v.PassThroughUniSerialized(output);
        v = v.PassThroughTypeDecoratingUniSerialized(output);
        return v;
    }

    // Serialized & TypeDecoratingSerialized

    public static T PassThroughUniSerialized<T>(this T value, ITestOutputHelper? output = null)
    {
        var v = UniSerialized.New(value);
        v = PassThroughSystemJsonSerializer(v, output);
        v = PassThroughNewtonsoftJsonSerializer(v, output);
        v = PassThroughMessagePackByteSerializer(v, output);
        v = PassThroughMemoryPackByteSerializer(v, output);
        return v.Value;
    }

    public static T PassThroughTypeDecoratingUniSerialized<T>(this T value, ITestOutputHelper? output = null)
    {
        var v = TypeDecoratingUniSerialized.New((object?)value);
        v = PassThroughSystemJsonSerializer(v, output);
        v = PassThroughNewtonsoftJsonSerializer(v, output);
        v = PassThroughMessagePackByteSerializer(v, output);
        v = PassThroughMemoryPackByteSerializer(v, output);
        return (T)v.Value!;
    }

    // TypeDecoratingTextSerializer

    public static T PassThroughTypeDecoratingTextSerializer<T>(this T value, ITestOutputHelper? output = null)
    {
        var sInner = new SystemJsonSerializer(SystemJsonOptions);
        var s = new TypeDecoratingTextSerializer(sInner);
        var json = s.Write(value, typeof(object));
        output?.WriteLine($"TypeDecoratingTextSerializer: {json}");
        var result = (T)s.Read<object>(json);

        output?.WriteLine($"PassThroughTypeDecoratingTextSerializer -> {result}");
        return result;
    }

    // TypeDecoratingByteSerializer

    public static T PassThroughTypeDecoratingByteSerializer<T>(this T value, ITestOutputHelper? output = null)
    {
        var sInner = new MemoryPackByteSerializer();
        var s = new TypeDecoratingByteSerializer(sInner);
        using var buffer = s.Write(value, typeof(object));
        var v0 = buffer.WrittenMemory.ToArray();
        output?.WriteLine($"TypeDecoratingByteSerializer: {v0.AsByteString()}");
        var result = (T)s.Read(v0, typeof(object), out _)!;

        output?.WriteLine($"PassThroughTypeDecoratingByteSerializer -> {result}");
        return result;
    }

    // System.Text.Json serializer

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

        var v2 = TypeDecoratingSystemJsonSerialized.New(value);
        output?.WriteLine($"TypeDecoratingSystemJsonSerialized: {v2.Data}");
        value = TypeDecoratingSystemJsonSerialized.New<T>(v2.Data).Value;

        output?.WriteLine($"PassThroughSystemJsonSerializer -> {value}");
        return value;
    }

    // Newtonsoft.Json serializer

    public static T PassThroughNewtonsoftJsonSerializer<T>(this T value, ITestOutputHelper? output = null)
    {
        if (!UseNewtonsoftJsonSerializer)
            return value;

        var s = new NewtonsoftJsonSerializer(NewtonsoftJsonSettings).ToTyped<T>();
        var json = s.Write(value);
        output?.WriteLine($"NewtonsoftJsonSerializer: {json}");
        value = s.Read(json);

        using var buffer = new ArrayPoolBuffer<byte>(ArrayPools.SharedBytePool, mustClear: false);
        s.Write(buffer, value);
        var bytes = buffer.WrittenMemory;
        var json2 = Encoding.UTF8.GetDecoder().Convert(bytes.Span);
        json2.Should().Be(json);
        var v0 = s.Read(bytes, out _);
        var json3 = s.Write(v0);
        json3.Should().Be(json);

        var v1 = NewtonsoftJsonSerialized.New(value);
        output?.WriteLine($"NewtonsoftJsonSerialized: {v1.Data}");
        value = NewtonsoftJsonSerialized.New<T>(v1.Data).Value;

        output?.WriteLine($"PassThroughNewtonsoftJsonSerializer -> {value}");
        return value;
    }

    // MessagePack serializer

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
        var json1 = MessagePackSerializer.ConvertToJson(v0, MessagePackByteSerializer.DefaultOptions);
        output?.WriteLine($"MessagePackSerialized: {json1} as {v1.Data.AsByteString()}");
        value = MessagePackSerialized.New<T>(v1.Data).Value;

        var v2 = TypeDecoratingMessagePackSerialized.New(value);
        output?.WriteLine($"TypeDecoratingMessagePackSerialized: {v2.Data.AsByteString()}");
        value = TypeDecoratingMessagePackSerialized.New<T>(v2.Data).Value;

        output?.WriteLine($"PassThroughMessagePackByteSerializer -> {value}");
        return value;
    }

    // MemoryPack serializer

    public static T PassThroughMemoryPackByteSerializer<T>(this T value, ITestOutputHelper? output = null)
    {
        if (!UseMemoryPackSerializer)
            return value;

        var s = new MemoryPackByteSerializer().ToTyped<T>();
        using var buffer = s.Write(value);
        var v0 = buffer.WrittenMemory.ToArray();
        output?.WriteLine($"MemoryPackByteSerializer: {v0.AsByteString()}");
        value = s.Read(v0, out _);

        var v1 = MemoryPackSerialized.New(value);
        output?.WriteLine($"MemoryPackSerialized: {v1.Data.AsByteString()}");
        value = MemoryPackSerialized.New<T>(v1.Data).Value;

        var v2 = TypeDecoratingMemoryPackSerialized.New(value);
        output?.WriteLine($"TypeDecoratingMemoryPackSerialized: {v2.Data.AsByteString()}");
        value = TypeDecoratingMemoryPackSerialized.New<T>(v2.Data).Value;

        output?.WriteLine($"PassThroughMemoryPackByteSerializer -> {value}");
        return value;
    }

    public static T PassThroughNerdbankMessagePackByteSerializer<T>(this T value, ITestOutputHelper? output = null)
    {
        if (!UseNerdbankMessagePackSerializer)
            return value;

#if NET8_0_OR_GREATER
        var s = new NerdbankMessagePackByteSerializer().ToTyped<T>();
        using var buffer = s.Write(value);
        var v0 = buffer.WrittenMemory.ToArray();
        var json0 = MessagePackSerializer.ConvertToJson(v0, MessagePackByteSerializer.DefaultOptions);
        output?.WriteLine($"NerdbankMessagePackByteSerializer: {json0} as {v0.AsByteString()}");
        value = s.Read(v0, out _);

        var v1 = NerdbankMessagePackSerialized.New(value);
        var json1 = MessagePackSerializer.ConvertToJson(v1.Data, MessagePackByteSerializer.DefaultOptions);
        output?.WriteLine($"NerdbankMessagePackSerialized: {json1} as {v1.Data.AsByteString()}");
        value = NerdbankMessagePackSerialized.New<T>(v1.Data).Value;

        var v2 = TypeDecoratingNerdbankMessagePackSerialized.New(value);
        output?.WriteLine($"TypeDecoratingNerdbankMessagePackSerialized: {v2.Data.AsByteString()}");
        value = TypeDecoratingNerdbankMessagePackSerialized.New<T>(v2.Data).Value;

        output?.WriteLine($"PassThroughNerdbankMessagePackByteSerializer -> {value}");
        return value;
#else
        return value;
#endif
    }
}
