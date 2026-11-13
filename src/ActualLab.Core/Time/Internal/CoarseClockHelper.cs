using System.Diagnostics;
using ActualLab.Generators;

namespace ActualLab.Time.Internal;

/// <summary>
/// Provides a periodically updated coarse timestamp and random values,
/// reducing the overhead of frequent time and random number queries.
/// The update timer stops while nobody reads them and restarts on the next read.
/// </summary>
public static class CoarseClockHelper
{
    public static readonly int Frequency = 20;
    public static readonly Moment Start;

#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif
    private static readonly TimeSpan Interval;
    private static readonly Timer Timer;
    private static readonly RandomInt64Generator Rng = new();
    // Writers publish a new snapshot via Volatile.Write, and the getters dereference it right
    // away, so their plain reads are already ordered - don't "fix" them: LDAR isn't free on ARM64
    private static State _state;
    private static int _tickIndex;
    private static bool _isTimerSuspended;

    public static Moment Now {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GetState().Now;
    }

    public static long RandomInt64 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GetState().RandomInt64;
    }

    public static int RandomInt32 {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GetState().RandomInt32;
    }

    public static bool IsTimerSuspended {
        get {
            lock (StaticLock)
                return _isTimerSuspended;
        }
    }

    static CoarseClockHelper()
    {
        Start = Moment.Now;
        _state = new State(isProbe: false); // Plain: type-init completion publishes it
        Interval = TimeSpan.FromSeconds(1.0 / Frequency);
        Timer = NonCapturingTimer.Create(Update, null!, Interval, Interval);
    }

    // Private methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static State GetState()
    {
        var state = _state;
        return state.IsProbe ? ReplaceProbe() : state;
    }

    private static State ReplaceProbe()
    {
        lock (StaticLock) {
            var state = _state;
            if (!state.IsProbe)
                return state;

            state = new State(isProbe: false);
            Volatile.Write(ref _state, state);
            if (_isTimerSuspended) {
                _isTimerSuspended = false;
                _tickIndex = 0;
                Timer.Change(Interval, Interval);
            }
            return state;
        }
    }

    [DebuggerStepThrough]
    private static void Update(object? _)
    {
        lock (StaticLock) {
            if (_isTimerSuspended)
                return;

            if (_state.IsProbe) {
                _isTimerSuspended = true;
                Timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                return;
            }

            var isProbe = ++_tickIndex % Frequency == 0;
            Volatile.Write(ref _state, new State(isProbe));
        }
    }

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
        // Published once a second: the first reader replaces it with a fresh snapshot,
        // and a probe still in place on the next tick means nobody reads the clock
        public readonly bool IsProbe;

        [DebuggerStepThrough, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public State(bool isProbe)
        {
            Now = Moment.Now;
            ElapsedTicks = (Now - Start).Ticks;
            RandomInt64 = Rng.Next();
            RandomInt32 = unchecked((int)RandomInt64);
            IsProbe = isProbe;
        }
    }
}
