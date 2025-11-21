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

        {
            WriteLine("Automatic Caching:");
            #region Part01_Automatic_Caching
            await counters.Get("a"); // Prints: Get(a) = 0
            await counters.Get("a"); // Prints nothing -- it's a cache hit; the result is 0
            #endregion
        }

        {
            WriteLine($"{Environment.NewLine}Automatic Dependency Tracking:");
            #region Part01_Automatic_Dependency_Tracking
            await counters.Sum("a", "b"); // Prints: Get(b) = 0, Sum(a, b) = 0 -- Get(b) was called from Sum(a, b)
            await counters.Sum("a", "b"); // Prints nothing -- it's a cache hit; the result is 0
            await counters.Get("b");      // Prints nothing -- it's a cache hit; the result is 0
            #endregion
        }

        {
            WriteLine($"{Environment.NewLine}Invalidation:");
            #region Part01_Invalidation
            counters.Increment("a"); // Prints: Increment(a) + invalidates Get(a) call result
            await counters.Get("a"); // Prints: Get(a) = 1
            await counters.Get("b"); // Prints nothing -- Get(b) call wasn't invalidated, so it's a cache hit
            #endregion
        }

        {
            WriteLine($"{Environment.NewLine}Cascading Invalidation:");
            #region Part01_Cascading_Invalidation
            counters.Increment("a"); // Prints: Increment(a)

            // Increment(a) invalidated Get(a), but since invalidations are cascading,
            // and Sum(a, b) depends on Get(a), it's also invalidated.
            // That's why Sum(a, b) is going to be recomputed on the next call, as well as Get(a),
            // which is called by Sum(a, b).
            await counters.Sum("a", "b"); // Prints: Get(a) = 2, Sum(a, b) = 2
            await counters.Sum("a", "b"); // Prints nothing, it's a cache hit; the result is 0

            // Even though we expect Sum(a, b) == Sum(b, a), Fusion doesn't know that.
            // Remember, "cache key" for any compute method call is (service, method, args...),
            // and arguments are different in this case: (a, b) != (b, a).
            // So Fusion will have to compute Sum(b, a) from scratch.
            // But note that Get(a) and Get(b) calls it makes are still resolved from cache.
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
            WriteLine(computedForSumAB.IsConsistent()); // False, invalidation is always cascading

            // Manually update computedForSumAB
            var newComputedForSumAB = await computedForSumAB.Update();
            // Prints:
            // Get(a) = 2, we invalidated it, so it was of Sum(a, b)
            // Sum(a, b) = 2, Update() call above actually triggered this call

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
        }

        {
            WriteLine($"{Environment.NewLine}Reactive updates:");
            #region Part01_Reactive_Updates
            _ = Task.Run(async () => {
                // This is going to be our update loop
                for (var i = 0; i <= 3; i++) {
                    await Task.Delay(1000);
                    counters.Increment("a");
                }
            });

            var clock = Stopwatch.StartNew();
            var computed = await Computed.Capture(() => counters.Sum("a", "b"));
            WriteLine($"{clock.Elapsed:g}s: {computed}, Value = {computed.Value}");
            for (var i = 0; i <= 3; i++) {
                await computed.WhenInvalidated();
                computed = await computed.Update();
                WriteLine($"{clock.Elapsed:g}s: {computed}, Value = {computed.Value}");
            }
            #endregion
        }

        {
            WriteLine($"{Environment.NewLine}Computed<T>.When() and .Changes() methods:");
            #region Part01_When_And_Changes_Methods
            _ = Task.Run(async () => {
                // This is going to be our update loop
                for (var i = 0; i <= 5; i++) {
                    await Task.Delay(333);
                    counters.Increment("a");
                }
            });

            var clock = Stopwatch.StartNew();
            var computed = await Computed.Capture(() => counters.Sum("a", "b"));

            // Computed<T>.When(..) example:
            computed = await computed.When(x => x >= 10); // ~= .Changes().When(predicate).First()

            // Computed<T>.Changes() example:
            IAsyncEnumerable<Computed<int>> changes = computed.Changes();

            _ = Task.Run(async () => {
                await foreach (var (value, error) in changes) // Computed<T> deconstruction example
                    WriteLine($"{clock.Elapsed:g}s: Value = {value}, Error = {error}");
            });
            await Task.Delay(5000); // Wait for the changes to be processed
            #endregion
        }

        {
            WriteLine($"{Environment.NewLine}MutableState:");
            #region Part01_MutableState
            var stateFactory = sp.StateFactory(); // Same as sp.GetRequiredService<IStateFactory>()
            var state = stateFactory.NewMutable(1);
            var oldComputed = state.Computed;

            WriteLine($"Value: {state.Value}, Computed: {state.Computed}");
            // Value: 1, Computed: StateBoundComputed<Int32>(MutableState<Int32>-Hash=39252654 v.d2, State: Consistent)

            state.Value = 2;
            WriteLine($"Value: {state.Value}, Computed: {state.Computed}");
            // Value: 2, Computed: StateBoundComputed<Int32>(MutableState<Int32>-Hash=39252654 v.h2, State: Consistent)

            WriteLine($"Old computed: {oldComputed}"); // Should be invalidated
            // Old computed: StateBoundComputed<Int32>(MutableState<Int32>-Hash=39252654 v.d2, State: Invalidated)

            state.Error = new ApplicationException("Just a test");
            try {
                WriteLine($"Value: {state.Value}, Computed: {state.Computed}");
                // Accessing state.Value throws ApplicationException
            }
            catch (ApplicationException) {
                WriteLine($"Error: {state.Error.GetType()}, Computed: {state.Computed}");
            }
            WriteLine($"LastNonErrorValue: {state.LastNonErrorValue}");
            // LastNonErrorValue: 2
            WriteLine($"Snapshot.LastNonErrorComputed: {state.Snapshot.LastNonErrorComputed}");
            // Snapshot.LastNonErrorComputed: StateBoundComputed<Int32>(MutableState<Int32>-Hash=39252654 v.h2, State: Invalidated)
            #endregion
        }

        {
            WriteLine($"{Environment.NewLine}ComputedState:");
            #region Part01_ComputedState
            var stateFactory = sp.StateFactory();
            var clock = Stopwatch.StartNew();

            // We'll use this state as a dependency for the computed state
            var mutableState = stateFactory.NewMutable("x");

            // ComputedState<T> instances must be disposed, otherwise they'll never stop recomputing!
            using var computedState = stateFactory.NewComputed(
                new ComputedState<string>.Options() {
                    InitialValue = "<initial>",
                    UpdateDelayer = FixedDelayer.Get(1), // 1 second update delay
                    // You can attach event handlers later as well. EventConfigurator allows setting them up
                    // right on construction, i.e., before any of these events can occur.
                    EventConfigurator = state => {
                        // A shortcut to attach 3 event handlers: Invalidated, Updating, Updated
                        state.AddEventHandler(
                            StateEventKind.All,
                            (s, e) => WriteLine($"{clock.Elapsed:g}s: {e}, Value: {s.Value}, Computed: {s.Computed}"));
                    },
                },
                // This lambda describes how the computed state is computed –
                // essentially, it's a compute method written as a lambda.
                async (state, cancellationToken) => {
                    // We intentionally delay the computation here to show how the initial value works
                    await Task.Delay(100, cancellationToken);
                    var counter = await counters.Get("a");
                    // state.Use() is required to track the state usage inside a compute method
                    var mutableValue = await mutableState.Use(cancellationToken);
                    return $"({counter}, {mutableValue})";
                });

            WriteLine($"{clock.Elapsed:g}s: CREATED, Value: {computedState.Value}, Computed: {computedState.Computed}");
            await computedState.Update(); // This ensures the very first value is computed
            WriteLine($"{clock.Elapsed:g}s: UPDATED, Value: {computedState.Value}, Computed: {computedState.Computed}");

            counters.Increment("a");
            await Task.Delay(2000);
            mutableState.Value = "y";
            await Task.Delay(2000);

            /* The output - pay attention to timestamps:
            0:00:00.0080204s: Invalidated, Value: <initial>, Computed: StateBoundComputed<String>(FuncComputedStateEx<String>-Hash=27401660 v.st, State: Invalidated)
            0:00:00.0126295s: Updating, Value: <initial>, Computed: StateBoundComputed<String>(FuncComputedStateEx<String>-Hash=27401660 v.st, State: Invalidated)
            0:00:00.0161148s: CREATED, Value: <initial>, Computed: StateBoundComputed<String>(FuncComputedStateEx<String>-Hash=27401660 v.st, State: Invalidated)
            0:00:00.1297889s: Updated, Value: (6, x), Computed: StateBoundComputed<String>(FuncComputedStateEx<String>-Hash=27401660 v.10t, State: Consistent)
            0:00:00.1305231s: UPDATED, Value: (6, x), Computed: StateBoundComputed<String>(FuncComputedStateEx<String>-Hash=27401660 v.10t, State: Consistent)
            Increment(a)
            0:00:00.1308741s: Invalidated, Value: (6, x), Computed: StateBoundComputed<String>(FuncComputedStateEx<String>-Hash=27401660 v.10t, State: Invalidated)
            0:00:01.1392269s: Updating, Value: (6, x), Computed: StateBoundComputed<String>(FuncComputedStateEx<String>-Hash=27401660 v.10t, State: Invalidated)
            Get(a) = 7
            0:00:01.2481635s: Updated, Value: (7, x), Computed: StateBoundComputed<String>(FuncComputedStateEx<String>-Hash=27401660 v.14t, State: Consistent)
            0:00:02.1347489s: Invalidated, Value: (7, x), Computed: StateBoundComputed<String>(FuncComputedStateEx<String>-Hash=27401660 v.14t, State: Invalidated)
            0:00:03.1433923s: Updating, Value: (7, x), Computed: StateBoundComputed<String>(FuncComputedStateEx<String>-Hash=27401660 v.14t, State: Invalidated)
            0:00:03.2524918s: Updated, Value: (7, y), Computed: StateBoundComputed<String>(FuncComputedStateEx<String>-Hash=27401660 v.gq, State: Consistent)
            */

            #endregion
        }
    }
}
