using MessagePack;

namespace ActualLab.Time;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
#if NET8_0_OR_GREATER
[MessagePackObject(true, SuppressSourceGeneration = true)]
#else
[MessagePackObject(true)]
#endif
public partial record RetryDelaySeq(
    [property: DataMember, MemoryPackOrder(0)] TimeSpan Min,
    [property: DataMember, MemoryPackOrder(1)] TimeSpan Max,
    [property: DataMember, MemoryPackOrder(2)] double Spread)
{
    public const double DefaultSpread = 0.1;
    public const double DefaultMultiplier = 1.41421356237; // Math.Sqrt(2)

    public static RetryDelaySeq Fixed(double delayInSeconds, double spread = DefaultSpread)
        => Fixed(TimeSpan.FromSeconds(delayInSeconds), spread);
    public static RetryDelaySeq Fixed(TimeSpan delay, double spread = DefaultSpread)
        => new(delay, delay, spread, 1);

    public static RetryDelaySeq Exp(double minInSeconds, double maxInSeconds, double spread = DefaultSpread, double multiplier = DefaultMultiplier)
        => new (TimeSpan.FromSeconds(minInSeconds), TimeSpan.FromSeconds(maxInSeconds), spread, multiplier);
    public static RetryDelaySeq Exp(TimeSpan min, TimeSpan max, double spread = DefaultSpread, double multiplier = DefaultMultiplier)
        => new (min, max, spread, multiplier);

    [DataMember, MemoryPackOrder(3)]
    public double Multiplier { get; init; } = DefaultMultiplier;

    public virtual TimeSpan this[int failureCount] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GetDelay(failureCount);
    }

    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
    public RetryDelaySeq(
        TimeSpan min, TimeSpan max,
        double spread = DefaultSpread,
        double multiplier = DefaultMultiplier)
        : this(min, max, spread)
        => Multiplier = multiplier;

    public virtual TimeSpan GetDelay(int failureCount)
    {
        if (Min <= TimeSpan.Zero)
            throw new InvalidOperationException(
                $"{nameof(RetryDelaySeq)}.{nameof(Min)} must be greater than zero.");

        if (failureCount <= 0)
            return TimeSpan.Zero;
        if (Multiplier <= 1d) // Fixed, i.e. no exponential component
            return Min.ToRandom(Spread).Next().Positive();

        try {
            var multiplier = Math.Pow(Multiplier, failureCount - 1);
            return TimeSpanExt.Min(Max, Min.MultiplyBy(multiplier)).ToRandom(Spread).Next().Positive();
        }
        catch (OverflowException) {
            return Max;
        }
    }

    public IEnumerable<TimeSpan> Delays(int start = 0, int count = int.MaxValue)
    {
        for (var tryIndex = start; count > 0; tryIndex++, count--)
            yield return this[tryIndex];
    }

    // Conversion

    public override string ToString()
        => Multiplier <= 1d
            ? $"{nameof(RetryDelaySeq)}.{nameof(Fixed)}({Min.ToShortString()} ± {Spread:P0}])"
            : $"{nameof(RetryDelaySeq)}.{nameof(Exp)}({Min.ToShortString()} ... {Max.ToShortString()} ± {Spread:P0}, x {Multiplier:F2}])";
}
