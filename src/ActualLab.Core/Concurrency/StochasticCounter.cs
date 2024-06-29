using ActualLab.Generators;
using ActualLab.OS;

namespace ActualLab.Concurrency;

[StructLayout(LayoutKind.Auto)]
public struct StochasticCounter
{
    public const int MaxPrecision = 2048;
    public static int DefaultPrecision => HardwareInfo.ProcessorCountPo2;

    private readonly int _mask;
    private volatile int _value;

    public int Value {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Interlocked.Exchange(ref _value, value);
    }

    public int Precision {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _mask + 1;
    }

    public StochasticCounter(int precision)
    {
        if (precision < 1)
            throw new ArgumentOutOfRangeException(nameof(precision));

        precision = Math.Min(MaxPrecision, precision);
        _mask = (int)Bits.GreaterOrEqualPowerOf2((ulong)precision) - 1;
    }

    // Overloads w/o random

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryIncrement(int max)
        => TryIncrement(ThreadRandom.Next(), max);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDecrement(int min)
        => TryDecrement(ThreadRandom.Next(), min);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int? Increment()
        => Increment(ThreadRandom.Next());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int? Decrement()
        => Decrement(ThreadRandom.Next());

    // Overloads with random

    public bool TryIncrement(int random, int max)
    {
        if (_value > max)
            return false;

        if (Increment(random) is { } value && value > max) {
            Interlocked.Add(ref _value, -(_mask + 1)); // Revert increment
            return false;
        }

        return true;
    }

    public bool TryDecrement(int random, int min)
    {
        if (_value < min)
            return false;

        if (Decrement(random) is { } value && value < min) {
            Interlocked.Add(ref _value, _mask + 1); // Revert decrement
            return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int? Increment(int random)
        => (random & _mask) == 0
            ? Interlocked.Add(ref _value, _mask + 1)
            : null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int? Decrement(int random)
        => (random & _mask) == 0
            ? Interlocked.Add(ref _value, -(_mask + 1))
            : null;
}
