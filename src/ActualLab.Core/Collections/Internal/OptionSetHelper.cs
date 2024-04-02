using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;
using ActualLab.IO;

namespace ActualLab.Collections.Internal;

internal static class OptionSetHelper
{
    public static string GetToStringArgs(IReadOnlyDictionary<Symbol, object> items)
    {
        const int limit = 5;
        var count = items.Count;
        if (count == 0)
            return "";

        var args = items.Take(limit).Select(kv => $"{kv.Key}: {kv.Value}").ToDelimitedString();
        return count > limit
            ? $"{count} items: [ {args}, ... ]"
            : $"{count} items: [ {args} ]";
    }

    public static Dictionary<string, NewtonsoftJsonSerialized<object>> ToNewtonsoftJsonCompatible(
        IReadOnlyDictionary<Symbol, object> items)
        => items.ToDictionary(
            p => p.Key.Value,
            p => NewtonsoftJsonSerialized.New(p.Value),
            StringComparer.Ordinal);

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public static OptionSetItem<byte[]>[] ToByteSerializerCompatible(
        TypeDecoratingByteSerializer itemSerializer,
        IReadOnlyDictionary<Symbol, object?> items)
    {
        using var buffer = new ArrayPoolBuffer<byte>(256);
        var result = new OptionSetItem<byte[]>[items.Count];
        var index = 0;
        foreach (var (key, value) in items) {
            itemSerializer.Write(buffer, value, typeof(object));
            result[index++] = new OptionSetItem<byte[]>(key.Value,buffer.WrittenSpan.ToArray());
            buffer.Clear();
        }
        return result;
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public static IEnumerable<KeyValuePair<Symbol, object?>> FromByteSerializerCompatible(
        TypeDecoratingByteSerializer itemSerializer,
        OptionSetItem<byte[]>[] items)
    {
        foreach (var (key, value) in items) {
            var deserialized = itemSerializer.Read(value, typeof(object));
            yield return KeyValuePair.Create((Symbol)key, deserialized);
        }
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public static OptionSetItem<string>[] ToTextSerializerCompatible(
        TypeDecoratingTextSerializer itemSerializer,
        IReadOnlyDictionary<Symbol, object?> items)
    {
        var result = new OptionSetItem<string>[items.Count];
        var index = 0;
        foreach (var (key, value) in items) {
            var data = itemSerializer.Write(value, typeof(object));
            result[index++] = new OptionSetItem<string>(key.Value, data);
        }
        return result;
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public static IEnumerable<KeyValuePair<Symbol, object?>> FromTextSerializerCompatible(
        TypeDecoratingTextSerializer itemSerializer,
        OptionSetItem<string>[] items)
    {
        foreach (var (key, value) in items) {
            var deserialized = itemSerializer.Read(value, typeof(object));
            yield return KeyValuePair.Create((Symbol)key, deserialized);
        }
    }
}
