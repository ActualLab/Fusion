using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ActualLab.Fusion;
using static System.Console;

namespace Tutorial
{
    public static class Part05
    {
        #region Part05_Service1
        public class Service1 : IComputeService
        {
            [ComputeMethod]
            public virtual async Task<string> Get(string key)
            {
                WriteLine($"{nameof(Get)}({key})");
                return key;
            }
        }

        public static IServiceProvider CreateServices()
        {
            var services = new ServiceCollection();
            services.AddFusion()
                .AddService<Service1>()
                .AddService<Service2>() // We'll use Service2 & other services later
                .AddService<Service3>();
            return services.BuildServiceProvider();
        }
        #endregion

        public static async Task Caching1()
        {
            #region Part05_Caching1
            var service = CreateServices().GetRequiredService<Service1>();
            // var computed = await Computed.Capture(() => counters.Get("a"));
            WriteLine(await service.Get("a"));
            WriteLine(await service.Get("a"));
            GC.Collect();
            WriteLine("GC.Collect()");
            WriteLine(await service.Get("a"));
            WriteLine(await service.Get("a"));
            #endregion
        }

        public static async Task Caching2()
        {
            #region Part05_Caching2
            var service = CreateServices().GetRequiredService<Service1>();
            var computed = await Computed.Capture(() => service.Get("a"));
            WriteLine(await service.Get("a"));
            WriteLine(await service.Get("a"));
            GC.Collect();
            WriteLine("GC.Collect()");
            WriteLine(await service.Get("a"));
            WriteLine(await service.Get("a"));
            #endregion
        }

        #region Part05_Service2
        public class Service2 : IComputeService
        {
            [ComputeMethod]
            public virtual async Task<string> Get(string key)
            {
                WriteLine($"{nameof(Get)}({key})");
                return key;
            }

            [ComputeMethod]
            public virtual async Task<string> Combine(string key1, string key2)
            {
                WriteLine($"{nameof(Combine)}({key1}, {key2})");
                return await Get(key1) + await Get(key2);
            }
        }
        #endregion

        public static async Task Caching3()
        {
            #region Part05_Caching3
            var service = CreateServices().GetRequiredService<Service2>();
            var computed = await Computed.Capture(() => service.Combine("a", "b"));
            WriteLine("computed = Combine(a, b) completed");
            WriteLine(await service.Combine("a", "b"));
            WriteLine(await service.Get("a"));
            WriteLine(await service.Get("b"));
            WriteLine(await service.Combine("a", "c"));
            GC.Collect();
            WriteLine("GC.Collect() completed");
            WriteLine(await service.Get("a"));
            WriteLine(await service.Get("b"));
            WriteLine(await service.Combine("a", "c"));
            #endregion
        }

        public static async Task Caching4()
        {
            #region Part05_Caching4
            var service = CreateServices().GetRequiredService<Service2>();
            var computed = await Computed.Capture(() => service.Get("a"));
            WriteLine("computed = Get(a) completed");
            WriteLine(await service.Combine("a", "b"));
            GC.Collect();
            WriteLine("GC.Collect() completed");
            WriteLine(await service.Combine("a", "b"));
            #endregion
        }

        #region Part05_Service3
        public class Service3 : IComputeService
        {
            [ComputeMethod]
            public virtual async Task<string> Get(string key)
            {
                WriteLine($"{nameof(Get)}({key})");
                return key;
            }

            [ComputeMethod(MinCacheDuration = 0.3)] // KeepAliveTime was added
            public virtual async Task<string> Combine(string key1, string key2)
            {
                WriteLine($"{nameof(Combine)}({key1}, {key2})");
                return await Get(key1) + await Get(key2);
            }
        }
        #endregion

        public static async Task Caching5()
        {
            #region Part05_Caching5
            var service = CreateServices().GetRequiredService<Service3>();
            WriteLine(await service.Combine("a", "b"));
            WriteLine(await service.Get("a"));
            WriteLine(await service.Get("x"));
            GC.Collect();
            WriteLine("GC.Collect()");
            WriteLine(await service.Combine("a", "b"));
            WriteLine(await service.Get("a"));
            WriteLine(await service.Get("x"));
            await Task.Delay(1000);
            GC.Collect();
            WriteLine("Task.Delay(...) and GC.Collect()");
            WriteLine(await service.Combine("a", "b"));
            WriteLine(await service.Get("a"));
            WriteLine(await service.Get("x"));
            #endregion
        }
    }
}
