using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ActualLab.Fusion;
using static System.Console;

namespace Tutorial
{
    public static class Part03
    {
        #region Part03_CounterService
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
            services.AddFusion().AddService<CounterService>();
            return services.BuildServiceProvider();
        }

        #endregion

        public static async Task MutableState()
        {
            #region Part03_MutableState
            var services = CreateServices();
            var stateFactory = services.StateFactory();
            var state = stateFactory.NewMutable<int>(1);
            var computed = state.Computed;
            WriteLine($"Value: {state.Value}, Computed: {state.Computed}");
            state.Value = 2;
            WriteLine($"Value: {state.Value}, Computed: {state.Computed}");
            WriteLine($"Old computed: {computed}"); // Should be invalidated
            #endregion
        }

        public static async Task MutableStateError()
        {
            #region Part03_MutableStateError
            var services = CreateServices();
            var stateFactory = services.StateFactory();
            var state = stateFactory.NewMutable<int>();
            WriteLine($"Value: {state.Value}, Computed: {state.Computed}");
            WriteLine("Setting state.Error.");
            state.Error = new ApplicationException("Just a test");
            try {
                WriteLine($"Value: {state.Value}, Computed: {state.Computed}");
            }
            catch (ApplicationException) {
                WriteLine($"Error: {state.Error.GetType()}, Computed: {state.Computed}");
            }
            WriteLine($"LastNonErrorValue: {state.LastNonErrorValue}");
            WriteLine($"Snapshot.LastNonErrorComputed: {state.Snapshot.LastNonErrorComputed}");
            #endregion
        }

        public static async Task ComputedState()
        {
            #region Part03_LiveState
            var services = CreateServices();
            var counters = services.GetRequiredService<CounterService>();
            var stateFactory = services.StateFactory();
            WriteLine("Creating state.");
            using var state = stateFactory.NewComputed(
                new ComputedState<string>.Options() {
                    UpdateDelayer = FixedDelayer.Get(1), // 1 second update delay
                    EventConfigurator = state1 => {
                        // A shortcut to attach 3 event handlers: Invalidated, Updating, Updated
                        state1.AddEventHandler(StateEventKind.All,
                            (s, e) => WriteLine($"{DateTime.Now}: {e}, Value: {s.Value}, Computed: {s.Computed}"));
                    },
                },
                async (state, cancellationToken) => {
                    var counter = await counters.Get("a");
                    return $"counters.Get(a) -> {counter}";
                });
            WriteLine("Before state.Update(false).");
            await state.Update(); // Ensures the state gets up-to-date value
            WriteLine("After state.Update(false).");
            counters.Increment("a");
            await Task.Delay(2000);
            WriteLine($"Value: {state.Value}, Computed: {state.Computed}");
            #endregion
        }
    }
}
