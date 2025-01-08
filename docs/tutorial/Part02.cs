using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ActualLab.Fusion;
using static System.Console;

namespace Tutorial
{
    public static class Part02
    {
        #region Part02_CounterService
        public class CounterService : IComputeService
        {
            private readonly ConcurrentDictionary<string, int> _counters = new ConcurrentDictionary<string, int>();

            [ComputeMethod]
            public virtual async Task<int> Get(string key)
            {
                WriteLine($"{nameof(Get)}({key})");
                return _counters.TryGetValue(key, out var value) ? value : 0;
            }

            public void Increment(string key)
            {
                WriteLine($"{nameof(Increment)}({key})");
                _counters.AddOrUpdate(key, k => 1, (k, v) => v + 1);
                using (Invalidation.Begin())
                    _ = Get(key);
            }
        }

        public static IServiceProvider CreateServices()
        {
            var services = new ServiceCollection();
            var fusion = services.AddFusion();
            fusion.AddService<CounterService>();
            return services.BuildServiceProvider();
        }
        #endregion

        public static async Task CaptureComputed()
        {
            #region Part02_CaptureComputed
            var counters = CreateServices().GetRequiredService<CounterService>();
            var computed = await Computed.Capture(() => counters.Get("a"));
            WriteLine($"Computed: {computed}");
            WriteLine($"- IsConsistent(): {computed.IsConsistent()}");
            WriteLine($"- Value:          {computed.Value}");
            #endregion
        }

        public static async Task InvalidateComputed1()
        {
            #region Part02_InvalidateComputed1
            var counters = CreateServices().GetRequiredService<CounterService>();
            var computed = await Computed.Capture(() => counters.Get("a"));
            WriteLine($"computed: {computed}");
            WriteLine("computed.Invalidate()");
            computed.Invalidate();
            WriteLine($"computed: {computed}");
            var newComputed = await computed.Update();
            WriteLine($"newComputed: {newComputed}");
            #endregion
        }

        public static async Task InvalidateComputed2()
        {
            #region Part02_InvalidateComputed2
            var counters = CreateServices().GetRequiredService<CounterService>();
            var computed = await Computed.Capture(() => counters.Get("a"));
            WriteLine($"computed: {computed}");
            WriteLine("using (Invalidation.Begin()) counters.Get(\"a\"))");
            using (Invalidation.Begin()) // <- This line
                _ = counters.Get("a");
            WriteLine($"computed: {computed}");
            var newComputed = await Computed.Capture(() => counters.Get("a")); // <- This line
            WriteLine($"newComputed: {newComputed}");
            #endregion
        }

        public static async Task IncrementCounter()
        {
            #region Part02_IncrementCounter
            var counters = CreateServices().GetRequiredService<CounterService>();

            _ = Task.Run(async () => {
                for (var i = 0; i <= 5; i++) {
                    await Task.Delay(1000);
                    counters.Increment("a");
                }
            });

            var computed = await Computed.Capture(() => counters.Get("a"));
            WriteLine($"{DateTime.Now}: {computed.Value}");
            for (var i = 0; i < 5; i++) {
                await computed.WhenInvalidated();
                computed = await computed.Update();
                WriteLine($"{DateTime.Now}: {computed.Value}");
            }
            #endregion
        }
    }
}
