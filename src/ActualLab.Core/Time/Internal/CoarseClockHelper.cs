using System.Diagnostics;
using ActualLab.Generators;

namespace ActualLab.Time.Internal;

public static class CoarseClockHelper
{
    public static readonly int Frequency = 20;
    public static readonly Moment Start;

    // ReSharper disable once NotAccessedField.Local
    private static readonly Timer Timer;
    private static readonly RandomInt64Generator Rng = new();
    private static volatile State _state;

    public static Moment Now {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _state.Now;
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
        var state = new State();
        _state = state; // Just to suppress .NET Standard warning
        Interlocked.Exchange(ref _state, state);
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
        public readonly Moment Now;
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public readonly long ElapsedTicks;
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public readonly long RandomInt64;
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public readonly int RandomInt32;

        [DebuggerStepThrough, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public State()
        {
            Now = Moment.Now;
            ElapsedTicks = (Now - Start).Ticks;
            RandomInt64 = Rng.Next();
            RandomInt32 = unchecked((int)RandomInt64);
        }
    }
}
