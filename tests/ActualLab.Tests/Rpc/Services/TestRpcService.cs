using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using ActualLab.Rpc;

namespace ActualLab.Tests.Rpc;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
public partial record HelloCommand(
    [property: DataMember, MemoryPackOrder(0)] string Name,
    [property: DataMember, MemoryPackOrder(1)] TimeSpan Delay
) : ICommand<string>
{
    public HelloCommand(string name) : this(name, default) { }
}

public interface ITestRpcService : ICommandService
{
    Task<int?> Div(int? a, int b);
    Task<int?> Add(int? a, int b);
    Task<TimeSpan> Delay(TimeSpan duration, CancellationToken cancellationToken = default);
    Task<int> GetCancellationCount();

    Task<int> PolymorphArg(ITuple argument, CancellationToken cancellationToken = default);
    Task<ITuple> PolymorphResult(int argument, CancellationToken cancellationToken = default);

    ValueTask<RpcNoWait> MaybeSet(string key, string? value);
    ValueTask<string?> Get(string key);

    Task<RpcStream<int>> StreamInt32(int count, int failAt = -1, RandomTimeSpan delay = default);
    Task<RpcStream<ITuple>> StreamTuples(int count, int failAt = -1, RandomTimeSpan delay = default);
    Task<int> Count(RpcStream<int> items, CancellationToken cancellationToken = default);
    Task CheckLag(RpcStream<Moment> items, int expectedCount, CancellationToken cancellationToken = default);

    [CommandHandler]
    Task<string> OnHello(HelloCommand command, CancellationToken cancellationToken = default);
}

public interface ITestRpcServiceClient : ITestRpcService, IRpcService
{
    Task<int> NoSuchMethod(int i1, int i2, int i3, int i4, CancellationToken cancellationToken = default);
}

public class TestRpcService(IServiceProvider services) : ITestRpcService
{
    private volatile int _cancellationCount;
    private readonly ConcurrentDictionary<string, string> _values = new();

    private IMomentClock SystemClock { get; } = services.Clocks().SystemClock;
    private ILogger Log { get; } = services.LogFor<TestRpcService>();

    public Task<int?> Div(int? a, int b)
        => Task.FromResult(a / b);

    public Task<int?> Add(int? a, int b)
        => Task.FromResult(a + b);

    public async Task<TimeSpan> Delay(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        try {
            await Task.Delay(duration, cancellationToken);
            return duration;
        }
        catch (OperationCanceledException) {
            Interlocked.Increment(ref _cancellationCount);
            throw;
        }
    }

    public Task<int> GetCancellationCount()
        => Task.FromResult(_cancellationCount);

    public Task<int> PolymorphArg(ITuple argument, CancellationToken cancellationToken = default)
        => Task.FromResult(argument.Length);

    public Task<ITuple> PolymorphResult(int argument, CancellationToken cancellationToken = default)
        => Task.FromResult((ITuple)Tuple.Create(argument));

    public ValueTask<RpcNoWait> MaybeSet(string key, string? value)
    {
        if (value == null)
            _values.Remove(key, out _);
        else
            _values[key] = value;
        return default;
    }

    public ValueTask<string?> Get(string key)
        => new(_values.GetValueOrDefault(key));

    public Task<RpcStream<int>> StreamInt32(int count, int failAt = -1, RandomTimeSpan delay = default)
    {
        var seq = Enumerate(count, failAt, delay);
        return Task.FromResult(RpcStream.New(seq));
    }

    public Task<RpcStream<ITuple>> StreamTuples(int count, int failAt = -1, RandomTimeSpan delay = default)
    {
        var seq = Enumerate(count, failAt, delay)
            .Select(x => (x & 2) == 0 ? (ITuple)new Tuple<int>(x) : new Tuple<long>(x));
        return Task.FromResult(RpcStream.New(seq));
    }

    public Task<int> Count(RpcStream<int> items, CancellationToken cancellationToken = default)
        => items.CountAsync(cancellationToken).AsTask();

    public async Task CheckLag(RpcStream<Moment> items, int expectedCount, CancellationToken cancellationToken = default)
    {
        var count = 0;
        var deltas = new List<TimeSpan>();
        await foreach (var item in items.ConfigureAwait(false)) {
            deltas.Add(SystemClock.Now - item);
            count++;
        }
        Log.LogInformation("CheckLag: {CountDelta}, {Deltas}",
            count - expectedCount,
            deltas.Select(x => x.ToShortString()).ToDelimitedString());
        count.Should().Be(expectedCount);
        if (count > 0)
            deltas.Should().AllSatisfy(x => x.Should().BeLessThan(TimeSpan.FromMilliseconds(100)));
    }

    public virtual async Task<string> OnHello(HelloCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Delay > TimeSpan.Zero)
            await Task.Delay(command.Delay, cancellationToken);

        if (Equals(command.Name, "error"))
            throw new ArgumentOutOfRangeException(nameof(command));

        return $"Hello, {command.Name}!";
    }

    // Private methods

    private static async IAsyncEnumerable<int> Enumerate(
        int count,
        int failAt,
        RandomTimeSpan delay,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var hasDelay = delay != default;
        var minDelay = TimeSpan.FromMilliseconds(1);
        for (var i = 0; i < count; i++) {
            if (i == failAt)
                throw new InvalidOperationException("Fail!");

            yield return i;
            if (!hasDelay)
                continue;

            var duration = delay.Next();
            if (duration >= minDelay)
                await Task.Delay(duration, cancellationToken);
        }
    }
}
