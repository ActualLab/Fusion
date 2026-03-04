using ActualLab.CommandR.Operations;
using ActualLab.Fusion.Testing;
using ActualLab.Fusion.Tests.Services;
using ActualLab.Versioning;

namespace ActualLab.Fusion.Tests.OperationEvents;

public class SqliteEventTest : DbEventTestBase
{
    public SqliteEventTest(ITestOutputHelper @out) : base(@out)
        => DbType = FusionTestDbType.Sqlite;
}

public class PostgreSqlEventTest : DbEventTestBase
{
    public PostgreSqlEventTest(ITestOutputHelper @out) : base(@out)
    {
        DbType = FusionTestDbType.PostgreSql;
        UseRedisOperationLogChangeTracking = false;
    }
}

public class MariaDbEventTest : DbEventTestBase
{
    public MariaDbEventTest(ITestOutputHelper @out) : base(@out)
        => DbType = FusionTestDbType.MariaDb;
}

public class SqlServerEventTest : DbEventTestBase
{
    public SqlServerEventTest(ITestOutputHelper @out) : base(@out)
        => DbType = FusionTestDbType.SqlServer;
}

public class InMemoryEventTest : DbEventTestBase
{
    public InMemoryEventTest(ITestOutputHelper @out) : base(@out)
        => DbType = FusionTestDbType.InMemory;
}

public abstract class DbEventTestBase(ITestOutputHelper @out) : FusionTestBase(@out)
{
    protected override void ConfigureTestServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureTestServices(services, isClient);
        var fusion = services.AddFusion();
        if (!isClient) {
            fusion.AddService<EventQueue>();
            fusion.AddService<EventCatcher>();
        }
    }

    [Fact]
    public async Task FailStrategyTest()
    {
        var c = Services.GetRequiredService<EventCatcher>();
        await Enqueue(E("a1.0"), E("a1.1"));
        await ComputedTest.When(async ct => {
            var events = await c.Events.Use(ct);
            events.Should().BeEquivalentTo("a1.1");
        });

        await Assert.ThrowsAnyAsync<Exception>(
            () => Enqueue(E("a1.1"), E("a2.1")));

        await Enqueue(E("a2.0"), E("a3.0"));
        await ComputedTest.When(async ct => {
            var events = await c.Events.Use(ct);
            events.Should().BeEquivalentTo("a1.1", "a2.0", "a3.0");
        });

        await Assert.ThrowsAnyAsync<Exception>(
            () => Enqueue(E("a1.1")));
        await Assert.ThrowsAnyAsync<Exception>(
            () => Enqueue(E("a0.1"), E("a1.1")));
        await Assert.ThrowsAnyAsync<Exception>(
            () => Enqueue(E("a0.1"), E("a1.1"), E("a2.1")));
        await ComputedTest.When(async ct => {
            var events = await c.Events.Use(ct);
            events.Should().BeEquivalentTo("a1.1", "a2.0", "a3.0");
        });
    }

    [Fact]
    public async Task SkipStrategyTest()
    {
        var c = Services.GetRequiredService<EventCatcher>();

        await Enqueue(ES("a1.0"), ES("a1.1"), ES("a1.2"));
        await ComputedTest.When(async ct => {
            var events = await c.Events.Use(ct);
            events.Should().BeEquivalentTo("a1.2");
        });

        await Task.WhenAll(new [] {
            Enqueue(0.0, ES("a2.0")),
            Enqueue(0.2, ES("a2.1")),
            Enqueue(0.4, ES("a2.2a"), ES("a2.2b")),
            Enqueue(0.6, ES("a2.3a"), ES("a2.3b"), ES("a2.3c"))
        });
        await ComputedTest.When(async ct => {
            var events = await c.Events.Use(ct);
            events.Should().BeEquivalentTo("a1.2", "a2.0");
        });

        await Enqueue(ES("a3.0"));
        await ComputedTest.When(async ct => {
            var events = await c.Events.Use(ct);
            events.Should().BeEquivalentTo("a1.2", "a2.0", "a3.0");
        });
    }

    [Fact]
    public async Task UpdateStrategyTest()
    {
        var c = Services.GetRequiredService<EventCatcher>();

        var clock = SystemClock.Instance;
        for (var round = 0; round < 3; round++) {
            WriteLine($"Round {round}:");
            c.Events.Set(ImmutableList<string>.Empty);
            var when = clock.Now + TimeSpan.FromSeconds(3);
            var lastId = "";
            for (var count = 1; count <= 3; count++) {
                WriteLine($"- Count {count}...");
                c.Events.Set(ImmutableList<string>.Empty);
                var updateEvents = Enumerable.Range(0, count).Select(i => EU($"a{round}.{i}", when)).ToArray();
                var skipEvents = Enumerable.Range(0, count).Select(i => ES($"a{round}.{i}s", when)).ToArray();
                await Enqueue(skipEvents);
                await Enqueue(skipEvents.Concat(updateEvents).ToArray());
                await Enqueue(skipEvents);
                lastId = ((EventCatcher_Event)updateEvents.Last().Value!).Id;
            }
            WriteLine($"- Enqueue to trigger: {(when - clock.Now).ToShortString()}");

            await ComputedTest.When(async ct => {
                var events = await c.Events.Use(ct);
                events.Count.Should().Be(1);
            }, TimeSpan.FromSeconds(15));
            WriteLine($"- Processing delay: {(clock.Now - when).ToShortString()}");

            c.Events.Value[0].Should().Be(lastId);
        }
    }

    [Fact]
    public async Task DelayedEventsTimingTest()
    {
        var c = Services.GetRequiredService<EventCatcher>();
        var clock = SystemClock.Instance;
        var now = clock.Now;
        var eventCount = 5;
        var maxJitter = TimeSpan.FromSeconds(1);

        // Schedule 5 events with staggered delays: now+1s, now+2s, ..., now+5s
        var events = new OperationEvent[eventCount];
        var expectedAt = new Moment[eventCount];
        for (var i = 0; i < eventCount; i++) {
            var id = $"d{i}";
            expectedAt[i] = now + TimeSpan.FromSeconds(i + 1);
            events[i] = E(id, expectedAt[i]);
        }
        await Enqueue(events);

        // Wait for all events to be processed
        var expectedIds = events.Select(e => ((EventCatcher_Event)e.Value!).Id).ToArray();
        await ComputedTest.When(async ct => {
            var processed = await c.Events.Use(ct);
            processed.Count.Should().Be(eventCount);
        }, TimeSpan.FromSeconds(eventCount + 10));

        // Assert each event was processed close to its scheduled time
        for (var i = 0; i < eventCount; i++) {
            var id = expectedIds[i];
            c.ProcessedAt.TryGetValue(id, out var processedAt).Should().BeTrue();
            var jitter = processedAt - expectedAt[i];
            WriteLine($"Event {id}: expected={expectedAt[i]}, " +
                $"processed={processedAt}, jitter={jitter.ToShortString()}");
            jitter.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero,
                $"event {id} should not be processed before its DelayUntil");
            jitter.Should().BeLessThan(maxJitter,
                $"event {id} should be processed within {maxJitter.TotalSeconds}s of its DelayUntil");
        }
    }

    // Private methods

    private OperationEvent ES(string id)
        => E(id, KeyConflictStrategy.Skip);

    private OperationEvent ES(string id, Moment delayUntil)
        => E(id, delayUntil, KeyConflictStrategy.Skip);

    private OperationEvent EU(string id)
        => E(id, KeyConflictStrategy.Update);

    private OperationEvent EU(string id, Moment delayUntil)
        => E(id, delayUntil, KeyConflictStrategy.Update);

    private OperationEvent E(string id, KeyConflictStrategy conflictStrategy = KeyConflictStrategy.Fail)
        => new(GetUuid(id), new EventCatcher_Event(id)) {
            UuidConflictStrategy = conflictStrategy,
        };

    private OperationEvent E(string id, Moment delayUntil, KeyConflictStrategy conflictStrategy = KeyConflictStrategy.Fail)
        => new(GetUuid(id), new EventCatcher_Event(id)) {
            DelayUntil = delayUntil,
            UuidConflictStrategy = conflictStrategy,
        };

    private Task Enqueue(params OperationEvent[] events)
        => Services.Commander().Call(new EventQueue_Add(events));

    private async Task Enqueue(double delay, params OperationEvent[] events)
    {
        await Task.Delay(TimeSpan.FromSeconds(delay));
        await Services.Commander().Call(new EventQueue_Add(events));
    }

    private string GetUuid(string id)
    {
        var uuidEndIndex = id.IndexOf('.');
        return uuidEndIndex < 0
            ? id
            : id.Substring(0, uuidEndIndex);
    }
}
