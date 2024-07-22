using System.Diagnostics;
using ActualLab.Generators;

namespace ActualLab.Time.Internal;

public static class CoarseClockHelper
{
    public static readonly int Frequency = 20;
    public static readonly Moment Start;
    public static readonly long StartEpochOffsetTicks;

    // ReSharper disable once NotAccessedField.Local
    private static readonly Timer Timer;
    private static readonly Stopwatch Stopwatch;
    private static readonly RandomInt64Generator Rng = new();
    private static volatile State _state;

    public static long ElapsedTicks {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _state.ElapsedTicks;
    }

    public static long NowEpochOffsetTicks {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => StartEpochOffsetTicks + _state.ElapsedTicks;
    }

    public static Moment Now {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(NowEpochOffsetTicks);
    }

    public static Moment SystemNow {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _state.SystemNow;
    }

    public static long RandomInt64 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _state.RandomInt64;
    }

    public static int RandomInt32 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _state.RandomInt32;
    }

    static CoarseClockHelper()
    {
        Start = Moment.Now;
        StartEpochOffsetTicks = Start.EpochOffset.Ticks;
        Stopwatch = Stopwatch.StartNew();
        Interlocked.Exchange(ref _state, new State());
        var interval = TimeSpan.FromSeconds(1.0 / Frequency);
        Timer = NonCapturingTimer.Create(Update, null!, interval, interval);
    }

    [DebuggerStepThrough]
    private static void Update(object? _)
        => Interlocked.Exchange(ref _state, new State());

    // Nested types

    private sealed class State
    {
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public readonly Moment SystemNow;
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public readonly long ElapsedTicks;
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public readonly long RandomInt64;
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public readonly int RandomInt32;

        [DebuggerStepThrough, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public State()
        {
            SystemNow = Moment.Now;
            ElapsedTicks = Stopwatch.Elapsed.Ticks;
            RandomInt64 = Rng.Next();
            RandomInt32 = unchecked((int)RandomInt64);
        }
    }
}
