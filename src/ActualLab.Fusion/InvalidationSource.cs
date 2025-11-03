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
    public static readonly InvalidationSource Cancellation = new("<Cancellation>");
    public static readonly InvalidationSource InitialState = new("<InitialState>");
    public static readonly InvalidationSource ComputedOnTimeoutNoInvalidationSource = new("Computed.OnTimeout: no InvalidationSource");
    public static readonly InvalidationSource ComputedTrySetOutputNoInvalidationSource = new("Computed.TrySetOutput: no InvalidationSource");
    public static readonly InvalidationSource ComputedStartAutoInvalidationCancellationError = new("Computed.StartAutoInvalidation: Error is OperationCancelledException");
    public static readonly InvalidationSource ComputedRegistryRegister = new("ComputedRegistry.Register: invalidation on replacement");
    public static readonly InvalidationSource StateProduce = new("State.ProduceComputed");
    public static readonly InvalidationSource StateInitialize = new("State.Initialize");
    public static readonly InvalidationSource MutableStateCreateComputed = new("MutableState.CreateComputed");
    public static readonly InvalidationSource ComputedSourceProduce = new("ComputedSource.ProduceComputed");
    public static readonly InvalidationSource StateExtRecompute = new("StateExt.Recompute");
    public static readonly InvalidationSource ComputedSourceExtRecompute = new("ComputedSourceExt.Recompute");

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

    // ReSharper disable once ConvertToPrimaryConstructor
    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    public InvalidationSource(Computed value)
        => Value = Invalidation.TrackingMode is InvalidationTrackingMode.WholeChain
            ? value
            : value.InvalidationSource.Value;

    // ReSharper disable once ConvertToPrimaryConstructor
    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    public InvalidationSource(string value)
        => Value = value;

    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    public InvalidationSource(string? file, string? member, int line = 0)
        => Value = Invalidation.TrackingMode is InvalidationTrackingMode.None
            ? Unknown // CodeLocation.Format is a ConcurrentDictionary lookup, so we want to save on that
            : CodeLocation.Format(file, member, line);

    // ToString and related methods

    public override string ToString()
        => Value?.ToString() ?? "";

    public string ToString(InvalidationSourceFormat format)
    {
        return format switch {
            InvalidationSourceFormat.Default => ToString(),
            InvalidationSourceFormat.Origin => Origin.ToString(),
            InvalidationSourceFormat.WholeChain => ToChainString(this),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

        static string ToChainString(InvalidationSource source) {
            if (source.IsNone)
                return "";

            var sb = StringBuilderExt.Acquire();
            sb.Append(source.ToString());
            source = source.Source;
            while (!source.IsNone) {
                sb.Append(" <- ").Append(source.ToString());
                source = source.Source;
            }
            return sb.ToStringAndRelease();
        }
    }

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
    public override int GetHashCode() => Value?.GetHashCode() ?? 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(InvalidationSource other) => Equals(Value, other.Value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(InvalidationSource left, InvalidationSource right) => left.Equals(right);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(InvalidationSource left, InvalidationSource right) => !left.Equals(right);
}
