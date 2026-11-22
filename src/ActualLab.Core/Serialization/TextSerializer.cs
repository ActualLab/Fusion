using ActualLab.Serialization.Internal;

namespace ActualLab.Serialization;

/// <summary>
/// Provides static access to the default <see cref="ITextSerializer"/> instances.
/// </summary>
public static class TextSerializer
{
    public static ITextSerializer Default { get; set; } = SystemJsonSerializer.Default;
    public static readonly ITextSerializer None = NoneTextSerializer.Instance;
    public static readonly ITextSerializer Null = NullTextSerializer.Instance;
}

/// <summary>
/// Provides static access to the default typed <see cref="ITextSerializer{T}"/> instances.
/// </summary>
public static class TextSerializer<T>
{
    public static readonly ITextSerializer<T> Default = TextSerializer.Default.ToTyped<T>();
    public static readonly ITextSerializer<T> None = NoneTextSerializer<T>.Instance;
    public static readonly ITextSerializer<T> Null = NullTextSerializer<T>.Instance;
}
