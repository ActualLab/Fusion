using System.Text;

namespace ActualLab.Fusion;


public readonly struct InvalidationSource :
    ICanBeNone<InvalidationSource>,
    IEnumerable<InvalidationSource>,
    IEquatable<InvalidationSource>
{
    public static InvalidationSource None {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default;
    }

    public static readonly InvalidationSource Unknown = new("<Unknown>");

    public object? Value { get; }
    public bool IsNone => Value is null;
    public InvalidationSource Source => Value is Computed c ? c.InvalidationSource : default;

    public InvalidationSource Origin {
        get {
            var source = this;
            while (true) {
                var next = source.Source;
                if (next.IsNone)
                    return source;

                source = next;
            }
        }
    }

    public InvalidationSource OriginPreferComputed {
        get {
            var source = this;
            while (true) {
                var next = source.Source.Value as Computed;
                if (next is null)
                    return source;

                source = new(next);
            }
        }
    }

    // Constructor-like methods

    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InvalidationSource ForCurrentLocation(
        [CallerFilePath] string? file = null,
        [CallerMemberName] string? member = null,
        [CallerLineNumber] int line = 0)
        => new(file, member, line);

    // Constructors

    // ReSharper disable once ConvertToPrimaryConstructor
    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    public InvalidationSource(object? value)
        => Value = value;

    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    public InvalidationSource(string? file, string? member, int line = 0)
        => Value = CodeLocation.Format(file, member, line);

    // ToString and related methods

    public override string ToString()
        => Value?.ToString() ?? "<None>";

    public string ToString(bool wholeChain)
    {
        if (!wholeChain)
            return ToString();

        var sb = StringBuilderExt.Acquire();
        AppendChain(sb);
        return sb.ToStringAndRelease();
    }

    public void AppendChain(StringBuilder sb)
    {
        var source = this;
        sb.Append(this);
        source = source.Source;
        while (!source.IsNone) {
            sb.Append(" <- ").Append(this);
            source = source.Source;
        }
    }

    // OrXxx

    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    public InvalidationSource Or(InvalidationSource noneReplacement)
        => new(Value ?? noneReplacement.Value);

    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    public InvalidationSource OrUnknown()
        => new(Value ?? Unknown.Value);

    // IEnumerable implementation
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public IEnumerator<InvalidationSource> GetEnumerator()
    {
        var source = this;
        while (!IsNone) {
            yield return source;
            source = source.Source;
        }
    }

    // Equality
    public override bool Equals(object? obj) => obj is InvalidationSource other && Equals(other);
    public bool Equals(InvalidationSource other) => Equals(Value, other.Value);
    public override int GetHashCode() => Value?.GetHashCode() ?? 0;
    public static bool operator ==(InvalidationSource left, InvalidationSource right) => left.Equals(right);
    public static bool operator !=(InvalidationSource left, InvalidationSource right) => !left.Equals(right);
}
