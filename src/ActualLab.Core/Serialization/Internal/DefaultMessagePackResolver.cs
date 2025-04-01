using System.Diagnostics.CodeAnalysis;
using ActualLab.Api.Internal;
using Cysharp.Serialization.MessagePack;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace ActualLab.Serialization.Internal;

public class DefaultMessagePackResolver : IFormatterResolver
{
    public static readonly IFormatterResolver Instance = new DefaultMessagePackResolver();

    public static IEnumerable<IFormatterResolver> Resolvers { get; set; } = new [] {
        StandardResolver.Instance,
    };

    public static readonly Dictionary<Type, Type> Formatters = new() {
        { typeof(Unit), typeof(UnitMessagePackFormatter) },
        { typeof(Ulid), typeof(UlidMessagePackFormatter) },
        { typeof(Option<>), typeof(OptionMessagePackFormatter<>) },
        { typeof(ApiOption<>), typeof(ApiOptionMessagePackFormatter<>) },
        { typeof(ApiNullable<>), typeof(ApiNullableMessagePackFormatter<>) },
        { typeof(ApiNullable8<>), typeof(ApiNullable8MessagePackFormatter<>) },
        { typeof(ApiArray<>), typeof(ApiArrayMessagePackFormatter<>) },
    };

    private DefaultMessagePackResolver()
    { }

    public IMessagePackFormatter<T>? GetFormatter<T>()
        => FormatterCache<T>.Formatter;

    [UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "We assume MessagePack formatters are preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "We assume MessagePack formatters are preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume MessagePack formatters are preserved")]
    private static object? ResolveFormatter(Type type)
    {
        Type? formatterType;
        if (!type.IsGenericType)
            return Formatters.TryGetValue(type, out formatterType)
                ? formatterType.CreateInstance()
                : null;

        var gtd = type.GetGenericTypeDefinition();
        return Formatters.TryGetValue(gtd, out formatterType)
            ? formatterType.MakeGenericType(type.GetGenericArguments()).CreateInstance()
            : null;
    }

    private static class FormatterCache<T>
    {
        public static readonly IMessagePackFormatter<T>? Formatter;

        static FormatterCache()
        {
            foreach (var resolver in Resolvers) {
                var formatter = resolver.GetFormatter<T>();
                if (formatter != null) {
                    Formatter = formatter;
                    return;
                }
            }

            Formatter = (IMessagePackFormatter<T>?)ResolveFormatter(typeof(T));
        }
    }
}
