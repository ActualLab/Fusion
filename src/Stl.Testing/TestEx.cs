using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions.Execution;
using Stl.Async;
using Stl.Collections;

namespace Stl.Testing
{
    public static class TestEx
    {
        public static readonly IEnumerable<TimeSpan> DefaultCheckIntervals = Intervals.Fixed(TimeSpan.FromMilliseconds(50)); 

        public static Task WhenMet(Action condition, 
            TimeSpan waitDuration) 
            => WhenMet(condition, null, waitDuration);

        public static async Task WhenMet(Action condition,
            IEnumerable<TimeSpan>? checkIntervals,
            TimeSpan waitDuration)
        {
            using var cts = new CancellationTokenSource(waitDuration);
            await WhenMet(condition, checkIntervals, cts.Token);
        }

        public static async Task WhenMet(Action condition,
            IEnumerable<TimeSpan>? checkIntervals,
            CancellationToken cancellationToken)
        {
            foreach (var timeout in (checkIntervals ?? DefaultCheckIntervals)) {
                using (var scope = new AssertionScope()) {
                    condition.Invoke();
                    if (!scope.HasFailures())
                        return;
                    if (!cancellationToken.IsCancellationRequested)
                        scope.Discard();
                }
                await Task.Delay(timeout, cancellationToken).SuppressCancellation();
            }
        }
    }
}
