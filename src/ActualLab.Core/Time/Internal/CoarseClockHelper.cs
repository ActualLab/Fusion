using System.Diagnostics;
using ActualLab.Generators;

namespace ActualLab.Time.Internal;

/// <summary>
/// Provides a periodically updated coarse timestamp and random values,
/// reducing the overhead of frequent time and random number queries.
/// </summary>
public static class CoarseClockHelper
{
    public static readonly int Frequency = 20;
    public static readonly Moment Start;

    // ReSharper disable once NotAccessedField.Local
    private static readonly Timer Timer;
    private static readonly RandomInt64Generator Rng = new();
    // Update publishes a new snapshot via Interlocked.Exchange; the getters below dereference it
    // immediately, so their plain reads are already ordered - don't "fix" them to Volatile.Read,
    // it's a real LDAR on ARM64 and this is one of the hottest paths in the framework
    private static State _state;

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
        _state = new State(); // Plain: type-init completion publishes it
        var interval = TimeSpan.FromSeconds(1.0 / Frequency);
        Timer = NonCapturingTimer.Create(Update, null!, interval, interval);
    }

    [DebuggerStepThrough]
    private static void Update(object? _)
        => Interlocked.Exchange(ref _state, new State());

    // Nested types

    /// <summary>
    /// Captures the current time and a random value in a single snapshot.
    /// </summary>
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
