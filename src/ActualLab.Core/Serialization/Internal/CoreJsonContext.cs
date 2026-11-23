#if NET8_0_OR_GREATER // Source-generated System.Text.Json metadata matters for NativeAOT only
using System.Text.Json.Serialization;
using ActualLab.Collections;
using ActualLab.Reflection;
using ActualLab.Text;
using ActualLab.Time;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Source-generated System.Text.Json metadata for primitives and the serializable types of ActualLab.Core;
/// <see cref="SystemJsonSerializer.TypeInfoResolver"/> includes it.
/// </summary>
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(byte))]
[JsonSerializable(typeof(short))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(char))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(DateTime))]
[JsonSerializable(typeof(DateTimeOffset))]
[JsonSerializable(typeof(TimeSpan))]
[JsonSerializable(typeof(byte[]))]
[JsonSerializable(typeof(int[]))]
[JsonSerializable(typeof(long[]))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Unit))]
[JsonSerializable(typeof(Moment))]
[JsonSerializable(typeof(Symbol))]
[JsonSerializable(typeof(TypeRef))]
[JsonSerializable(typeof(JsonString))]
[JsonSerializable(typeof(ExceptionInfo))]
[JsonSerializable(typeof(VersionSet))]
[JsonSerializable(typeof(PropertyBag<TypeSchema.Any>))]
[JsonSerializable(typeof(MutablePropertyBag<TypeSchema.Any>))]
internal sealed partial class CoreJsonContext : JsonSerializerContext;
#endif
