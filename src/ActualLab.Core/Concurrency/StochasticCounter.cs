using ActualLab.Generators;
using ActualLab.OS;

namespace ActualLab.Concurrency;

[StructLayout(LayoutKind.Auto)]
public struct StochasticCounter
{
    public const int MaxPrecision = 2048;
    public static int DefaultPrecision => HardwareInfo.ProcessorCountPo2;

    private volatile int _value;

    public int Value {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Interlocked.Exchange(ref _value, value);
    }

    public readonly int Mask;
    public readonly int Precision;

    public StochasticCounter(int precisionHint)
    {
        if (precisionHint < 1)
            throw new ArgumentOutOfRangeException(nameof(precisionHint));

        precisionHint = Math.Min(MaxPrecision, precisionHint);
        Mask = (int)Bits.GreaterOrEqualPowerOf2((ulong)precisionHint) - 1;
        Precision = Mask + 1;
    }

    // Overloads w/o random

    public bool TryIncrement(int max)
        => TryIncrement(RandomShared.Next(), max);

    public bool TryDecrement(int min)
        => TryDecrement(RandomShared.Next(), min);

    public int? Increment()
        => Increment(RandomShared.Next());

    public int? Decrement()
        => Decrement(RandomShared.Next());

    // Overloads with random

    public bool TryIncrement(int random, int max)
    {
        if (_value > max)
            return false;

        if (Increment(random) is { } value && value > max) {
            Interlocked.Add(ref _value, -Precision); // Revert increment
            return false;
        }

        return true;
    }

    public bool TryDecrement(int random, int min)
    {
        if (_value < min)
            return false;

        if (Decrement(random) is { } value && value < min) {
            Interlocked.Add(ref _value, Precision); // Revert decrement
            return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int? Increment(int random)
        => (random & Mask) == 0
            ? Interlocked.Add(ref _value, Precision)
            : null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int? Decrement(int random)
        => (random & Mask) == 0
            ? Interlocked.Add(ref _value, -Precision)
            : null;
}
