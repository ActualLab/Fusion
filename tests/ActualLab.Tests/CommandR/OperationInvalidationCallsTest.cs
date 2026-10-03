using ActualLab.CommandR.Operations;
using ActualLab.Interception;
using ActualLab.Reflection;

namespace ActualLab.Tests.CommandR;

// Operation's invalidation-call API, which - unlike its event API - needs no operation scope:
// a recorded call is just data, so it can be shaped before or after any scope exists.
public class OperationInvalidationCallsTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void AddInvalidationCallAppendsAndReturnsTheSameOperation()
    {
        var operation = NewOperation();
        var call = NewCall("Get", 1);

        operation.AddInvalidationCall(call).Should().BeSameAs(operation);
        operation.InvalidationCalls.Should().Equal(call);
    }

    [Fact]
    public void AddInvalidationCallsKeepsTheOrderItWasGiven()
    {
        var operation = NewOperation();
        var first = NewCall("Get", 1);
        var second = NewCall("Count", 0);

        // An array binds to the params ReadOnlySpan overload
        operation.AddInvalidationCalls(first, second);

        operation.InvalidationCalls.Should().Equal(first, second);
    }

    [Fact]
    public void AddInvalidationCallsWithNothingToAddIsANoOp()
    {
        var operation = NewOperation();
        var before = operation.InvalidationCalls;

        operation.AddInvalidationCalls();
        operation.AddInvalidationCalls(Array.Empty<ServiceCall>());

        // The same instance, not merely an equal one: the empty case must not rebuild the list
        operation.InvalidationCalls.Should().BeSameAs(before);
    }

    [Fact]
    public void AddInvalidationCallsTakesAnEnumerable()
    {
        var operation = NewOperation();
        var calls = new List<ServiceCall> { NewCall("Get", 1), NewCall("Count", 0) };

        operation.AddInvalidationCalls(calls.Where(_ => true));

        operation.InvalidationCalls.Should().Equal(calls);
    }

    [Fact]
    public void AddInvalidationCallsAppendsToWhatIsAlreadyThere()
    {
        var operation = NewOperation();
        var first = NewCall("Get", 1);
        var second = NewCall("Count", 0);
        var third = NewCall("Get", 2);

        operation.AddInvalidationCall(first);
        operation.AddInvalidationCalls(second);
        operation.AddInvalidationCalls([third]);

        operation.InvalidationCalls.Should().Equal(first, second, third);
    }

    [Fact]
    public void RemoveInvalidationCallReportsWhetherItRemovedAnything()
    {
        var operation = NewOperation();
        var call = NewCall("Get", 1);
        var absent = NewCall("Count", 0);
        operation.AddInvalidationCall(call);

        operation.RemoveInvalidationCall(absent).Should().BeFalse();
        operation.InvalidationCalls.Should().Equal(call);

        operation.RemoveInvalidationCall(call).Should().BeTrue();
        operation.InvalidationCalls.Should().BeEmpty();
    }

    [Fact]
    public void RemoveInvalidationCallsClearsEverything()
    {
        var operation = NewOperation();
        operation.AddInvalidationCalls(NewCall("Get", 1), NewCall("Count", 0));

        operation.RemoveInvalidationCalls();

        operation.InvalidationCalls.Should().BeEmpty();
    }

    [Fact]
    public void RemoveInvalidationCallsTakesAPredicate()
    {
        var operation = NewOperation();
        var get = NewCall("Get", 1);
        var count = NewCall("Count", 0);
        operation.AddInvalidationCalls(get, count);

        operation.RemoveInvalidationCalls(x => x.MethodName.StartsWith("Get", StringComparison.Ordinal));

        operation.InvalidationCalls.Should().Equal(count);
    }

    [Fact]
    public void AnOperationStartsWithNoInvalidationCalls()
        => NewOperation().InvalidationCalls.Should().BeEmpty();

    // Private methods

    private static Operation NewOperation()
        => new("test-uuid", "test-host");

    private static ServiceCall NewCall(string name, int value)
    {
        var type = typeof(Target);
        var method = MethodInfoExt.GetByRpcStyleName(type, $"{name}:{(name == "Count" ? 1 : 2)}");
        var arguments = name == "Count"
            ? ArgumentList.New(CancellationToken.None)
            : ArgumentList.New(value, CancellationToken.None);
        return ServiceCall.New(type, method, arguments);
    }

    // Nested types

    public class Target
    {
        public virtual int Get(int key, CancellationToken cancellationToken = default) => key;
        public virtual int Count(CancellationToken cancellationToken = default) => 0;
    }
}
