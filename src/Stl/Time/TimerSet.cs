using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Stl.Async;
using Stl.Collections;

namespace Stl.Time
{
    public sealed class TimerSet<TTimer> : AsyncProcessBase
        where TTimer : notnull
    {
        public record Options
        {
            private readonly TimeSpan _quanta = TimeSpan.FromSeconds(1);

            // ReSharper disable once StaticMemberInGenericType
            public static TimeSpan MinQuanta { get; } = TimeSpan.FromMilliseconds(10);

            public TimeSpan Quanta {
                get => _quanta;
                init => _quanta = TimeSpanEx.Max(MinQuanta, value);
            }

            public IMomentClock Clock { get; init; } = CoarseCpuClock.Instance;
        }

        private readonly Action<TTimer>? _fireHandler;
        private readonly RadixHeapSet<TTimer> _timers = new(45);
        private readonly Moment _start;
        private readonly object _lock = new object();
        private int minPriority = 0;

        public TimeSpan Quanta { get; }
        public IMomentClock Clock { get; }
        public int Count {
            get {
                lock (_lock) return _timers.Count;
            }
        }

        public TimerSet(Options? options = null, Action<TTimer>? fireHandler = null)
        {
            options ??= new();
            Quanta = options.Quanta;
            Clock = options.Clock;
            _fireHandler = fireHandler;
            _start = Clock.Now;
            RunAsync().Ignore();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOrUpdate(TTimer timer, Moment time)
        {
            lock (_lock) {
                var priority = GetPriority(time);
                _timers.AddOrUpdate(priority, timer);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AddOrUpdateToEarlier(TTimer timer, Moment time)
        {
            lock (_lock) {
                var priority = GetPriority(time);
                return _timers.AddOrUpdateToLower(priority, timer);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AddOrUpdateToLater(TTimer timer, Moment time)
        {
            lock (_lock) {
                var priority = GetPriority(time);
                return _timers.AddOrUpdateToHigher(priority, timer);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(TTimer timer)
        {
            lock (_lock) {
                return _timers.Remove(timer, out var _);
            }
        }

        // Protected & private methods

        protected override async Task RunInternalAsync(CancellationToken cancellationToken)
        {
            var dueAt = _start + Quanta;
            for (;; dueAt += Quanta) {
                cancellationToken.ThrowIfCancellationRequested();
                if (dueAt > Clock.Now)
                    // We intentionally don't pass CancellationToken here:
                    // the delay is supposed to be short & we want to save on
                    // CancellationToken registration/unregistration.
                    await Clock.DelayAsync(dueAt, default).ConfigureAwait(false);
                IReadOnlyDictionary<TTimer, long> minSet;
                lock (_lock) {
                    minSet = _timers.ExtractMinSet(minPriority);
                    ++minPriority;
                }
                if (_fireHandler != null && minSet.Count != 0) {
                    foreach (var (timer, _) in minSet) {
                        try {
                            _fireHandler!.Invoke(timer);
                        }
                        catch {
                            // Intended suppression
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long GetPriority(Moment time)
        {
            var priority = (time - _start).Ticks / Quanta.Ticks;
            return Math.Max(minPriority, priority);
        }
    }
}
