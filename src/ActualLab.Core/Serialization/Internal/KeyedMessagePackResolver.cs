using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// The resolver of keyed MessagePack: like <see cref="DefaultMessagePackResolver"/>, but the
/// [MessagePackObject] / [DataContract] types <see cref="StringKeyMessagePackResolver"/> accepts are
/// serialized as string-keyed maps, so a peer can read them without knowing their [Key(N)] layout.
/// </summary>
public sealed class KeyedMessagePackResolver : IFormatterResolver
{
    public static readonly IFormatterResolver Instance = new KeyedMessagePackResolver();

    // Types with a formatter of their own ([MessagePackFormatter], built-in ones) keep it
    public static IEnumerable<IFormatterResolver> Resolvers { get; set; } = [
        BuiltinResolver.Instance,
        AttributeFormatterResolver.Instance,
        StringKeyMessagePackResolver.Instance,
        DefaultMessagePackResolver.Instance,
    ];

    private KeyedMessagePackResolver()
    { }

    public IMessagePackFormatter<T>? GetFormatter<T>()
        => FormatterCache<T>.Formatter;

    // Nested types

    private static class FormatterCache<T>
    {
        public static readonly IMessagePackFormatter<T>? Formatter = GetFormatter();

        private static IMessagePackFormatter<T>? GetFormatter()
        {
            foreach (var resolver in Resolvers)
                if (resolver.GetFormatter<T>() is { } formatter)
                    return formatter;
            return null;
        }
    }
}
