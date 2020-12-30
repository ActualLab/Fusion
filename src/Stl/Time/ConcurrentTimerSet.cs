using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Stl.Async;
using Stl.DependencyInjection;
using Stl.Mathematics;

namespace Stl.Time
{
    public sealed class ConcurrentTimerSet<TTimer> : AsyncDisposableBase
        where TTimer : notnull
    {
        public record Options : TimerSet<TTimer>.Options
        {
            private readonly int _concurrencyLevel;

            public int ConcurrencyLevel {
                get => _concurrencyLevel;
                init => _concurrencyLevel = Math.Max(1, value);
            }
        }

        private readonly TimerSet<TTimer>[] _timerSets;
        private readonly int _concurrencyLevelMask;

        public TimeSpan Quanta { get; }
        public IMomentClock Clock { get; }
        public int ConcurrencyLevel { get; }
        public int Count => _timerSets.Sum(ts => ts.Count);

        public ConcurrentTimerSet(Options? options = null, Action<TTimer>? fireHandler = null)
        {
            options ??= new();
            Quanta = options.Quanta;
            Clock = options.Clock;
            ConcurrencyLevel = (int) Bits.GreaterOrEqualPowerOf2((ulong) Math.Max(1, options.ConcurrencyLevel));
            _concurrencyLevelMask = ConcurrencyLevel - 1;
            _timerSets = new TimerSet<TTimer>[ConcurrencyLevel];
            for (var i = 0; i < _timerSets.Length; i++)
                _timerSets[i] = new TimerSet<TTimer>(options, fireHandler);
        }

        protected override async ValueTask DisposeInternalAsync(bool disposing)
        {
            foreach (var timerSet in _timerSets)
                await timerSet.DisposeAsync();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOrUpdate(TTimer timer, Moment time)
        {
            var timerSet = _timerSets[timer.GetHashCode() & _concurrencyLevelMask];
            timerSet.AddOrUpdate(timer, time);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AddOrUpdateToEarlier(TTimer timer, Moment time)
        {
            var timerSet = _timerSets[timer.GetHashCode() & _concurrencyLevelMask];
            return timerSet.AddOrUpdateToEarlier(timer, time);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AddOrUpdateToLater(TTimer timer, Moment time)
        {
            var timerSet = _timerSets[timer.GetHashCode() & _concurrencyLevelMask];
            return timerSet.AddOrUpdateToLater(timer, time);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(TTimer timer)
        {
            var timerSet = _timerSets[timer.GetHashCode() & _concurrencyLevelMask];
            return timerSet.Remove(timer);
        }
    }
}
