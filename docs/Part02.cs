using static System.Console;

// ReSharper disable once CheckNamespace
namespace Tutorial02;

#region Part02_Service1
public partial class Service1 : IComputeService
{
    [ComputeMethod]
    public virtual async Task<string> Get(string key)
    {
        WriteLine($"{nameof(Get)}({key})");
        return key;
    }
}
#endregion

#region Part02_Service2
public partial class Service2 : IComputeService
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

#region Part02_Service3
public partial class Service3 : IComputeService
{
    [ComputeMethod]
    public virtual async Task<string> Get(string key)
    {
        WriteLine($"{nameof(Get)}({key})");
        return key;
    }

    [ComputeMethod(MinCacheDuration = 0.3)] // MinCacheDuration was added
    public virtual async Task<string> Combine(string key1, string key2)
    {
        WriteLine($"{nameof(Combine)}({key1}, {key2})");
        return await Get(key1) + await Get(key2);
    }
}
#endregion

public static class Part02
{
    public static IServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddFusion()
            .AddService<Service1>()
            .AddService<Service2>() // We'll use Service2 & other services later
            .AddService<Service3>();
        return services.BuildServiceProvider();
    }

    public static async Task Caching1()
    {
        #region Part02_Caching1
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
        #region Part02_Caching2
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

    public static async Task Caching3()
    {
        #region Part02_Caching3
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
        #region Part02_Caching4
        var service = CreateServices().GetRequiredService<Service2>();
        var computed = await Computed.Capture(() => service.Get("a"));
        WriteLine("computed = Get(a) completed");
        WriteLine(await service.Combine("a", "b"));
        GC.Collect();
        WriteLine("GC.Collect() completed");
        WriteLine(await service.Combine("a", "b"));
        #endregion
    }

    public static async Task Caching5()
    {
        #region Part02_Caching5
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

    public static async Task Run()
    {
        WriteLine("Caching1:");
        await Caching1();
        WriteLine();
        WriteLine("Caching2:");
        await Caching2();
        WriteLine();
        WriteLine("Caching3:");
        await Caching3();
        WriteLine();
        WriteLine("Caching4:");
        await Caching4();
        WriteLine();
        WriteLine("Caching5:");
        await Caching5();
    }
}
