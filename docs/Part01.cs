using System.Diagnostics;
using static System.Console;

namespace Docs;

#region Part01_Declare_Service
public class CounterService : IComputeService // This is a tagging interface any compute service must "implement"
{
    private readonly ConcurrentDictionary<string, int> _counters = new();

    [ComputeMethod] // Indicates this is a compute method
    public virtual async Task<int> Get(string key) // Must be virtual & async
    {
        var value = _counters.GetValueOrDefault(key, 0);
        WriteLine($"Get({key}) = {value}");
        return value;
    }

    [ComputeMethod] // Indicates this is a compute method
    public virtual async Task<int> Sum(string key1, string key2) // Must be virtual & async
    {
        var value1 = await Get(key1);
        var value2 = await Get(key2);
        var sum = value1 + value2;
        WriteLine($"Sum({key1}, {key2}) = {sum}");
        return sum;
    }

    // This is a regular method, so there are no special requirements
    public void Increment(string key)
    {
        WriteLine($"Increment({key})");
        _counters.AddOrUpdate(key, k => 1, (k, v) => v + 1);
        using (Invalidation.Begin())  {
            // Any call to a compute method inside this block means "invalidate the value for that call"
            _ = Get(key); // So here we invalidate the value of this.Get(...) call with the `key` argument
        }
    }
}
#endregion

public static class Part01
{
    public static async Task Run()
    {
        #region Part01_Register_Services
        var services = new ServiceCollection();
        var fusion = services.AddFusion(); // You can also use services.AddFusion(fusion => ...) pattern
        fusion.AddComputeService<CounterService>();
        var sp = services.BuildServiceProvider();
        
        // And that's how we get our first compute service:
        var counters = sp.GetRequiredService<CounterService>();
        #endregion

        WriteLine("Automatic Caching:");
        #region Part01_Automatic_Caching
        await counters.Get("a"); // Prints: Get(a) = 0
        await counters.Get("a"); // Prints nothing -- it's a cache hit; the result is 0
        await counters.Get("b"); // Prints: Get(b) = 0
        await counters.Get("b"); // Prints nothing -- it's a cache hit; the result is 0
        #endregion

        WriteLine($"{Environment.NewLine}Automatic Dependency Tracking:");
        #region Part01_Automatic_Dependency_Tracking
        await counters.Sum("a", "b"); // Prints: Sum(a, b) = 0
        await counters.Sum("a", "b"); // Prints nothing -- it's a cache hit; the result is 0
        #endregion

        WriteLine($"{Environment.NewLine}Invalidation:");
        #region Part01_Invalidation
        counters.Increment("a"); // Prints: Increment(a) + invalidates Get(a) call result
        await counters.Get("a"); // Prints: Get(a) = 1
        await counters.Get("b"); // Prints nothing -- Get(b) call wasn't invalidated, so it's a cache hit
        #endregion

        WriteLine($"{Environment.NewLine}Cascading Invalidation:");
        #region Part01_Cascading_Invalidation
        counters.Increment("a"); // Prints: Increment(a)
        await counters.Sum("a", "b"); // Prints: Get(a) = 2, Sum(a, b) = 2
        await counters.Sum("a", "b"); // Prints nothing - it's a cache hit; the result is 0
        await counters.Sum("b", "a"); // Prints: Sum(b, a) = 2 -- Get(b) and Get(a) results are already cached
        #endregion

        WriteLine($"{Environment.NewLine}Accessing Computed Values:");
        #region Part01_Accessing_Computed_Values
        var computedForGetA = await Computed.Capture(() => counters.Get("a"));
        WriteLine(computedForGetA.IsConsistent()); // True
        WriteLine(computedForGetA.Value);          // 2

        var computedForSumAB = await Computed.Capture(() => counters.Sum("a", "b"));
        WriteLine(computedForSumAB.IsConsistent()); // True
        WriteLine(computedForSumAB.Value);          // 2

        // Adding invalidation handler; you can also use WhenInvalidated
        computedForSumAB.Invalidated += _ => WriteLine("Sum(a, b) is invalidated");

        // Manually invalidate computedForGetA, i.e. the result of counters.Get("a") call
        computedForGetA.Invalidate(); // Prints: Sum(a, b) is invalidated
        WriteLine(computedForGetA.IsConsistent());  // False
        WriteLine(computedForSumAB.IsConsistent()); // False - invalidation is always cascading

        // Manually update computedForSumAB
        var newComputedForSumAB = await computedForSumAB.Update();
        // Prints:
        // Get(a) = 2 - we invalidated it, so it was of Sum(a, b)
        // Sum(a, b) = 2 - .Update() call above actually triggered this call

        WriteLine(newComputedForSumAB.IsConsistent()); // True
        WriteLine(newComputedForSumAB.Value); // 2

        // Calling .Update() for consistent Computed<T> returns the same instance
        WriteLine(computedForSumAB == newComputedForSumAB); // False
        WriteLine(newComputedForSumAB == await computedForSumAB.Update()); // True

        // Since `Computed<T>` are almost immutable,
        // the outdated computed instance is still usable:
        WriteLine(computedForSumAB.IsConsistent()); // False
        WriteLine(computedForSumAB.Value); // 2
        #endregion

        WriteLine($"{Environment.NewLine}Reactive updates:");
        #region Part01_Reactive_Updates
        _ = Task.Run(async () => {
            // This is going to be our update loop
            for (var i = 0; i <= 5; i++) {
                await Task.Delay(1000);
                counters.Increment("a");
            }
        });

        var stopwatch = Stopwatch.StartNew();
        var computed = await Computed.Capture(() => counters.Sum("a", "b"));
        WriteLine($"{stopwatch.Elapsed.TotalSeconds:F1}s: {computed}, Value = {computed.Value}");
        for (var i = 0; i < 5; i++) {
            await computed.WhenInvalidated();
            computed = await computed.Update();
            WriteLine($"{stopwatch.Elapsed.TotalSeconds:F1}s: {computed}, Value = {computed.Value}");
        }
        #endregion
    }
}
