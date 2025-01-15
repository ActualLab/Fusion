using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ActualLab.Fusion;
using static System.Console;

namespace Tutorial
{
    public static class Part01
    {
        #region Part01_CreateServices
        public static IServiceProvider CreateServices()
        {
            var services = new ServiceCollection();
            var fusion = services.AddFusion();
            fusion.AddService<CounterService>();
            fusion.AddService<CounterSumService>(); // We'll be using it later
            fusion.AddService<HelloService>();      // We'll be using it later
            return services.BuildServiceProvider();
        }
        #endregion

        #region Part01_CounterService
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
        #endregion

        public static async Task UseCounterService1()
        {
            #region Part01_UseCounterService1
            var counters = CreateServices().GetRequiredService<CounterService>();
            WriteLine(await counters.Get("a"));
            WriteLine(await counters.Get("b"));
            #endregion
        }

        public static async Task UseCounterService2()
        {
            #region Part01_UseCounterService2
            var counters = CreateServices().GetRequiredService<CounterService>();
            WriteLine(await counters.Get("a"));
            WriteLine(await counters.Get("a"));
            #endregion
        }

        public static async Task UseCounterService3()
        {
            #region Part01_UseCounterService3
            var counters = CreateServices().GetRequiredService<CounterService>();
            WriteLine(await counters.Get("a"));
            counters.Increment("a");
            WriteLine(await counters.Get("a"));
            #endregion
        }

        #region Part01_CounterSumService
        public class CounterSumService : IComputeService
        {
            public CounterService Counters { get; }

            public CounterSumService(CounterService counters) => Counters = counters;

            [ComputeMethod]
            public virtual async Task<int> Sum(string key1, string key2)
            {
                WriteLine($"{nameof(Sum)}({key1}, {key2})");
                return await Counters.Get(key1) + await Counters.Get(key2);
            }
        }
        #endregion

        public static async Task UseCounterSumService1()
        {
            #region Part01_UseCounterSumService1
            var services = CreateServices();
            var counterSum = services.GetRequiredService<CounterSumService>();
            WriteLine(await counterSum.Sum("a", "b"));
            WriteLine(await counterSum.Sum("a", "b"));
            #endregion
        }

        public static async Task UseCounterSumService2()
        {
            #region Part01_UseCounterSumService2
            var services = CreateServices();
            var counterSum = services.GetRequiredService<CounterSumService>();
            WriteLine("Nothing is cached (yet):");
            WriteLine(await counterSum.Sum("a", "b"));
            WriteLine("Only Get(a) and Get(b) outputs are cached:");
            WriteLine(await counterSum.Sum("b", "a"));
            WriteLine("Everything is cached:");
            WriteLine(await counterSum.Sum("a", "b"));
            #endregion
        }

        public static async Task UseCounterSumService3()
        {
            #region Part01_UseCounterSumService3
            var services = CreateServices();
            var counters = services.GetRequiredService<CounterService>();
            var counterSum = services.GetRequiredService<CounterSumService>();
            WriteLine(await counterSum.Sum("a", "b"));
            counters.Increment("a");
            WriteLine(await counterSum.Sum("a", "b"));
            #endregion
        }

        #region Part01_HelloService
        public class HelloService : IComputeService
        {
            [ComputeMethod]
            public virtual async Task<string> Hello(string name)
            {
                WriteLine($"+ {nameof(Hello)}({name})");
                await Task.Delay(1000);
                WriteLine($"- {nameof(Hello)}({name})");
                return $"Hello, {name}!";
            }
        }
        #endregion

        public static async Task UseHelloService1()
        {
            #region Part01_UseHelloService1
            var hello = CreateServices().GetRequiredService<HelloService>();
            var t1 = Task.Run(() => hello.Hello("Alice"));
            var t2 = Task.Run(() => hello.Hello("Bob"));
            var t3 = Task.Run(() => hello.Hello("Bob"));
            var t4 = Task.Run(() => hello.Hello("Alice"));
            await Task.WhenAll(t1, t2, t3, t4);
            WriteLine(t1.Result);
            WriteLine(t2.Result);
            WriteLine(t3.Result);
            WriteLine(t4.Result);
            #endregion
        }
    }
}
