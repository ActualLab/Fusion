using System.Text;
using FluentAssertions;
using Microsoft.Toolkit.HighPerformance.Buffers;
using Newtonsoft.Json;
using ActualLab.IO;
using Xunit.Abstractions;

namespace ActualLab.Testing;

public static class SerializationTestExt
{
    public static JsonSerializerOptions SystemJsonOptions { get; set; }
    public static JsonSerializerSettings NewtonsoftJsonSettings { get; set; }

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

    public static T PassThroughAllSerializers<T>(this T value, ITestOutputHelper? output = null)
    {
        var v = value;
        v = v.PassThroughSystemJsonSerializer(output);
        v = v.PassThroughNewtonsoftJsonSerializer(output);
        v = v.PassThroughMessagePackByteSerializer(output);
        v = v.PassThroughMemoryPackByteSerializer(output);
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
        output?.WriteLine($"TypeDecoratingByteSerializer: {JsonFormatter.Format(v0)}");
        var result = (T)s.Read<object>(v0);

        output?.WriteLine($"PassThroughTypeDecoratingByteSerializer -> {result}");
        return result;
    }

    // System.Text.Json serializer

    public static T PassThroughSystemJsonSerializer<T>(this T value, ITestOutputHelper? output = null)
    {
        var s = new SystemJsonSerializer(SystemJsonOptions).ToTyped<T>();
        var json = s.Write(value);
        output?.WriteLine($"SystemJsonSerializer: {json}");
        value = s.Read(json);

        using var buffer = new ArrayPoolBuffer<byte>();
        s.Write(buffer, value);
        var bytes = buffer.WrittenMemory;
        var json2 = Encoding.UTF8.GetDecoder().Convert(bytes.Span);
        json2.Should().Be(json);
        var v0 = s.Read(bytes);
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
        var s = new NewtonsoftJsonSerializer(NewtonsoftJsonSettings).ToTyped<T>();
        var json = s.Write(value);
        output?.WriteLine($"NewtonsoftJsonSerializer: {json}");
        value = s.Read(json);

        using var buffer = new ArrayPoolBuffer<byte>();
        s.Write(buffer, value);
        var bytes = buffer.WrittenMemory;
        var json2 = Encoding.UTF8.GetDecoder().Convert(bytes.Span);
        json2.Should().Be(json);
        var v0 = s.Read(bytes);
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
        var s = new MessagePackByteSerializer().ToTyped<T>();
        using var buffer = s.Write(value);
        var v0 = buffer.WrittenMemory.ToArray();
        output?.WriteLine($"MessagePackByteSerializer: {JsonFormatter.Format(v0)}");
        value = s.Read(v0);

        var v1 = MessagePackSerialized.New(value);
        output?.WriteLine($"MessagePackSerialized: {JsonFormatter.Format(v1.Data)}");
        value = MessagePackSerialized.New<T>(v1.Data).Value;

        var v2 = TypeDecoratingMessagePackSerialized.New(value);
        output?.WriteLine($"TypeDecoratingMessagePackSerialized: {JsonFormatter.Format(v2.Data)}");
        value = TypeDecoratingMessagePackSerialized.New<T>(v2.Data).Value;

        output?.WriteLine($"PassThroughMessagePackByteSerializer -> {value}");
        return value;
    }

    // MemoryPack serializer

    public static T PassThroughMemoryPackByteSerializer<T>(this T value, ITestOutputHelper? output = null)
    {
        var s = new MemoryPackByteSerializer().ToTyped<T>();
        using var buffer = s.Write(value);
        var v0 = buffer.WrittenMemory.ToArray();
        output?.WriteLine($"MemoryPackByteSerializer: {JsonFormatter.Format(v0)}");
        value = s.Read(v0);

        var v1 = MemoryPackSerialized.New(value);
        output?.WriteLine($"MemoryPackSerialized: {JsonFormatter.Format(v1.Data)}");
        value = MemoryPackSerialized.New<T>(v1.Data).Value;

        var v2 = TypeDecoratingMemoryPackSerialized.New(value);
        output?.WriteLine($"TypeDecoratingMemoryPackSerialized: {JsonFormatter.Format(v2.Data)}");
        value = TypeDecoratingMemoryPackSerialized.New<T>(v2.Data).Value;

        output?.WriteLine($"PassThroughMemoryPackByteSerializer -> {value}");
        return value;
    }
}
