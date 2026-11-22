using ActualLab.Serialization.Internal;

namespace ActualLab.Serialization;

/// <summary>
/// Provides static access to the default <see cref="IByteSerializer"/> and the serializer running on the current thread.
/// </summary>
public static class ByteSerializer
{
    [ThreadStatic] private static IByteSerializer? _current;

    public static IByteSerializer Default { get; set; } = MessagePackByteSerializer.Default;
    public static readonly IByteSerializer None = NoneByteSerializer.Instance;
    public static readonly IByteSerializer Null = NullByteSerializer.Instance;

    // The (untyped) serializer whose Read or Write runs on this thread, if any. Converters use it to serialize
    // nested values the same way the outer value is serialized, e.g. with the same options.
    public static IByteSerializer? Current => _current;

    // Converters of wrappers like UniSerialized<T> use it to serialize nested values with the outer serializer,
    // so they fail rather than silently pick a serializer with different settings
    public static IByteSerializer GetCurrent()
        => _current ?? throw Errors.NoCurrentSerializer(typeof(IByteSerializer), null);

    public static TSerializer GetCurrent<TSerializer>()
        where TSerializer : class, IByteSerializer
        => _current as TSerializer ?? throw Errors.NoCurrentSerializer(typeof(TSerializer), _current);

    // Nested types

    public readonly struct Scope : IDisposable
    {
        private readonly IByteSerializer? _previous;
        private readonly bool _isActive;

        // Activates root, which callers resolve as serializer.Root; default(Scope) is a no-op scope
        public Scope(IByteSerializer root)
        {
            _previous = _current;
            _current = root;
            _isActive = true;
        }

        public void Dispose()
        {
            if (_isActive)
                _current = _previous;
        }
    }
}

/// <summary>
/// Provides static access to the default typed <see cref="IByteSerializer{T}"/>.
/// </summary>
public static class ByteSerializer<T>
{
    public static readonly IByteSerializer<T> Default = ByteSerializer.Default.ToTyped<T>();
    public static readonly IByteSerializer<T> None = NoneByteSerializer<T>.Instance;
    public static readonly IByteSerializer<T> Null = NullByteSerializer<T>.Instance;
}
