using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Fusion.EntityFramework.Operations.LogProcessing;
using ActualLab.Fusion.Tests.DbModel;
using ActualLab.Fusion.Tests.Services;
using ActualLab.Testing.Logging;

namespace ActualLab.Fusion.Tests;

public class OperationLogReaderGapTest(ITestOutputHelper @out) : FusionTestBase(@out)
{
    private readonly CapturingLoggerProvider _loggerProvider = new();

    protected override void ConfigureTestServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureTestServices(services, isClient);
        if (isClient)
            return;

        services.AddFusion().AddService<CounterService>();
        services.AddLogging(logging => logging.AddProvider(_loggerProvider));
    }

    [Fact]
    public async Task FailedEntryBudgetTest()
    {
        var reader = CreateReader(new() {
            CheckPeriod = TimeSpan.FromMilliseconds(50).ToRandom(0.1),
            FailedEntryRetryLimit = 3,
        });
        reader.KnownEntries[7] = NewDbOperation(7);
        reader.FailingIndexes[7] = default;
        reader.RunOnReprocessExhausted(7);
        reader.HasGap(7).Should().BeTrue();

        for (var i = 0; i < 100 && reader.HasGap(7); i++) {
            await reader.RunProcessGaps();
            await Task.Delay(25);
        }
        reader.HasGap(7).Should().BeFalse();
        reader.ProcessAttempts[7].Should().Be(3);

        // Abandoned = gone for good: further checks must not retry it
        await reader.RunProcessGaps();
        reader.ProcessAttempts[7].Should().Be(3);

        var content = _loggerProvider.Content;
        content.Should().Contain("retry budget is exhausted");
        (content.Split("retry budget is exhausted").Length - 1).Should().Be(1);
    }

    [Fact]
    public async Task FailedEntryCadenceTest()
    {
        var reader = CreateReader(new() {
            CheckPeriod = TimeSpan.FromSeconds(1), // No jitter - the retry gate is exactly 1 s
            GapCheckPeriod = TimeSpan.FromMilliseconds(100),
            FailedEntryRetryLimit = 10,
        });
        reader.KnownEntries[7] = NewDbOperation(7);
        reader.FailingIndexes[7] = default;
        reader.RunOnReprocessExhausted(7);

        // Rapid wake-ups must burn at most one attempt per CheckPeriod,
        // and a budgeted entry must not ride the fast gap-poll cadence
        for (var i = 0; i < 20; i++) {
            var nextCheckAt = await reader.RunProcessGaps();
            (nextCheckAt - reader.Now).Should().BeGreaterThan(TimeSpan.FromMilliseconds(500));
        }
        reader.ProcessAttempts[7].Should().Be(1);
        reader.HasGap(7).Should().BeTrue();

        await Task.Delay(1500);
        await reader.RunProcessGaps();
        reader.ProcessAttempts[7].Should().Be(2);
        reader.HasGap(7).Should().BeTrue();
    }

    [Fact]
    public async Task PureGapCadenceTest()
    {
        var reader = CreateReader(new() { GapCheckPeriod = TimeSpan.FromMilliseconds(200) });
        reader.RunAddGap(5);

        var nextCheckAt = await reader.RunProcessGaps();
        nextCheckAt.Should().NotBe(Moment.MaxValue); // A young pure gap polls on the fast cadence
        (nextCheckAt - reader.Now).Should().BeLessThan(TimeSpan.FromSeconds(2));
        reader.HasGap(5).Should().BeTrue();
        reader.QueryCounts[5].Should().Be(1);

        // The miss advanced NextAttemptAt, so a back-to-back check doesn't re-query the gap
        reader.KnownEntries[5] = NewDbOperation(5);
        await reader.RunProcessGaps();
        reader.HasGap(5).Should().BeTrue();
        reader.QueryCounts[5].Should().Be(1);

        // Once due, the gap is re-queried and resolved
        await Task.Delay(400);
        nextCheckAt = await reader.RunProcessGaps();
        nextCheckAt.Should().Be(Moment.MaxValue);
        reader.HasGap(5).Should().BeFalse();
        reader.ProcessAttempts[5].Should().Be(1);
        reader.QueryCounts[5].Should().Be(2);
    }

    [Fact]
    public async Task BudgetedEntryHorizonExpiryTest()
    {
        var reader = CreateReader(new() {
            CheckPeriod = TimeSpan.FromMilliseconds(50).ToRandom(0.1),
            GapRetentionPeriod = TimeSpan.FromMilliseconds(200),
            FailedEntryRetryLimit = 10,
        });
        // The entry's row never appears, so its retry budget is never touched
        reader.RunOnReprocessExhausted(7);
        await reader.RunProcessGaps();
        reader.HasGap(7).Should().BeTrue();
        reader.QueryCounts[7].Should().Be(1);

        await Task.Delay(400);
        var nextCheckAt = await reader.RunProcessGaps();
        nextCheckAt.Should().Be(Moment.MaxValue);
        reader.HasGap(7).Should().BeFalse();
        reader.ProcessAttempts.Should().BeEmpty();

        var content = _loggerProvider.Content;
        content.Should().Contain("outlived the retention horizon");
        (content.Split("outlived the retention horizon").Length - 1).Should().Be(1);
    }

    [Fact]
    public async Task FullBatchGapCheckTest()
    {
        var reader = CreateReader(new());
        var realHostId = Services.GetRequiredService<DbHub<TestDbContext>>().HostId.Id;
        var dbContext = await CreateDbContext();
        await using var _ = dbContext;
        for (var i = 1; i <= 2; i++) {
            var op = NewDbOperation(i);
            op.LoggedAt = DateTime.UtcNow;
            op.HostId = realHostId; // The hosted reader replays these as local no-ops
            dbContext.Operations.Add(op);
        }
        await dbContext.SaveChangesAsync();

        reader.KnownEntries[100] = NewDbOperation(100);
        reader.RunAddGap(100);

        // A full batch must still run the gap check while returning default = "read again immediately"
        var nextCheckAt = await reader.RunProcessBatch(batchSize: 2);
        nextCheckAt.Should().Be(default(Moment));
        reader.GetNextIndex().Should().Be(3);
        reader.HasGap(100).Should().BeFalse();
        reader.ProcessAttempts[100].Should().Be(1);
    }

    [Fact]
    public void GapSetSizeLimitTest()
    {
        var reader = CreateReader(new() { GapSetSizeLimit = 2 });
        reader.RunAddGap(1);
        reader.RunAddGap(2);
        reader.RunAddGap(3);
        reader.RunOnReprocessExhausted(4);

        reader.GapCount().Should().Be(2);
        reader.HasGap(3).Should().BeFalse();
        reader.HasGap(4).Should().BeFalse();
    }

    [Fact]
    public async Task GapRetentionExpiryTest()
    {
        var reader = CreateReader(new() { GapRetentionPeriod = TimeSpan.FromMilliseconds(200) });
        reader.RunAddGap(5);
        await Task.Delay(400);

        var nextCheckAt = await reader.RunProcessGaps();
        nextCheckAt.Should().Be(Moment.MaxValue);
        reader.HasGap(5).Should().BeFalse();
        reader.ProcessAttempts.Should().BeEmpty();
    }

    [Fact]
    public async Task NotifiedWakeGapCheckTest()
    {
        var reader = CreateReader(new() {
            CheckPeriod = TimeSpan.FromSeconds(30),
            GapCheckPeriod = TimeSpan.FromSeconds(30),
        });
        reader.KnownEntries[7] = NewDbOperation(7);
        reader.FailingIndexes[7] = default;
        reader.RunOnReprocessExhausted(7); // A budgeted (failed earlier) entry
        reader.RunAddGap(5); // A pure gap
        await reader.RunProcessGaps(); // Both miss/fail and get gated for ~30 s
        reader.QueryCounts[5].Should().Be(1);
        reader.ProcessAttempts[7].Should().Be(1);

        // Not notified -> the gates hold even though the gap entry is committed now
        reader.KnownEntries[5] = NewDbOperation(5);
        await reader.RunProcessGaps();
        reader.QueryCounts[5].Should().Be(1);

        // Notified -> the young pure gap is re-checked immediately, the budgeted entry isn't
        reader.RunOnChangeNotified();
        await reader.RunProcessGaps();
        reader.HasGap(5).Should().BeFalse();
        reader.ProcessAttempts[5].Should().Be(1);
        reader.QueryCounts[5].Should().Be(2);
        reader.HasGap(7).Should().BeTrue();
        reader.ProcessAttempts[7].Should().Be(1);
    }

    [Fact]
    public async Task ProcessingDelayWarningTest()
    {
        var reader = CreateReader(new());
        var realHostId = Services.GetRequiredService<DbHub<TestDbContext>>().HostId.Id;
        reader.KnownEntries[5] = NewDbOperation(5); // LoggedAt is 10 minutes ago - way past the 1 s threshold
        reader.KnownEntries[6] = NewDbOperation(6); // Must be suppressed by ProcessingDelayWarningPeriod
        var localOp = NewDbOperation(7);
        localOp.HostId = realHostId; // Local entries must not be reported at all
        reader.KnownEntries[7] = localOp;
        reader.RunAddGap(5);
        reader.RunAddGap(6);
        reader.RunAddGap(7);
        await reader.RunProcessGaps();
        reader.GapCount().Should().Be(0);

        var content = _loggerProvider.Content;
        content.Should().Contain("(gap path)");
        (content.Split("was processed").Length - 1).Should().Be(1);
    }

    [Fact]
    public async Task ResolveFrontGapTest()
    {
        var reader = CreateReader(new());
        var counters = Services.GetRequiredService<CounterService>();
        await counters.Set("frontGap", 1);
        var computed = await Computed.Capture(() => counters.Get("frontGap"));
        computed.IsConsistent().Should().BeTrue();

        var dbContext = await CreateDbContext();
        await using var _ = dbContext;
        dbContext.Operations.Add(NewDbOperation(10));
        dbContext.Operations.Add(NewDbOperation(100));
        await dbContext.SaveChangesAsync();

        // Normal gap: entries below the cursor survive -> no sweep
        var nextIndex = await reader.RunResolveFrontGap(dbContext, 50, 100);
        nextIndex.Should().Be(50);
        computed.IsConsistent().Should().BeTrue();

        // Coverage loss: no entries below the cursor -> pessimistic full sweep
        nextIndex = await reader.RunResolveFrontGap(dbContext, 5, 10);
        nextIndex.Should().Be(10);
        reader.GetNextIndex().Should().Be(10);
        computed.IsConsistent().Should().BeFalse();
    }

    // Private methods

    private GapTestLogReader CreateReader(DbOperationLogReader<TestDbContext>.Options options)
        => new(options, Services);

    private static DbOperation NewDbOperation(long index)
        => new() {
            Index = index,
            Uuid = $"test-op-{index}",
            HostId = "test-host",
            LoggedAt = DateTime.UtcNow - TimeSpan.FromMinutes(10),
            CommandJson = "",
        };
}

// A test double: Process and GetGapEntries are diverted to in-memory state,
// so gap/budget machinery can be driven directly, without the DB and the worker loop.
public class GapTestLogReader(
    DbOperationLogReader<TestDbContext>.Options settings,
    IServiceProvider services
    ) : DbOperationLogReader<TestDbContext>(settings, services)
{
    private const string Shard = DbShard.Single;

    public ConcurrentDictionary<long, DbOperation> KnownEntries { get; } = new();
    public ConcurrentDictionary<long, Unit> FailingIndexes { get; } = new();
    public ConcurrentDictionary<long, int> ProcessAttempts { get; } = new();
    public ConcurrentDictionary<long, int> QueryCounts { get; } = new();
    public Moment Now => SystemClock.Now;

    public Task<Moment> RunProcessGaps(CancellationToken cancellationToken = default)
        => ProcessGaps(Shard, cancellationToken);

    public Task<Moment> RunProcessBatch(int batchSize, CancellationToken cancellationToken = default)
        => ProcessBatch(Shard, batchSize, cancellationToken);

    public void RunAddGap(long index)
        => AddGap(Shard, index, SystemClock.Now);

    public void RunOnReprocessExhausted(long index)
        => OnReprocessExhausted(Shard, index, CancellationToken.None);

    public void RunOnChangeNotified()
        => OnChangeNotified(Shard);

    public Task<long> RunResolveFrontGap(
        TestDbContext dbContext, long nextIndex, long firstIndex, CancellationToken cancellationToken = default)
        => ResolveFrontGap(Shard, dbContext, nextIndex, firstIndex, cancellationToken);

    public long? GetNextIndex()
        => NextIndexes.TryGetValue(Shard, out var nextIndex) ? nextIndex : null;

    public bool HasGap(long index)
    {
        if (!Gaps.TryGetValue(Shard, out var shardGaps))
            return false;

        lock (shardGaps)
            return shardGaps.Entries.ContainsKey(index);
    }

    public int GapCount()
    {
        if (!Gaps.TryGetValue(Shard, out var shardGaps))
            return 0;

        lock (shardGaps)
            return shardGaps.Entries.Count;
    }

    protected override Task Process(string shard, DbOperation entry, CancellationToken cancellationToken)
    {
        ProcessAttempts.AddOrUpdate(entry.Index, 1, static (_, count) => count + 1);
        return FailingIndexes.ContainsKey(entry.Index)
            ? Task.FromException(new InvalidOperationException($"Simulated failure for entry #{entry.Index}."))
            : Task.CompletedTask;
    }

    protected override Task<List<DbOperation>> GetGapEntries(
        string shard, long[] indexes, CancellationToken cancellationToken)
    {
        var result = new List<DbOperation>();
        foreach (var index in indexes) {
            QueryCounts.AddOrUpdate(index, 1, static (_, count) => count + 1);
            if (KnownEntries.TryGetValue(index, out var entry))
                result.Add(entry);
        }
        return Task.FromResult(result);
    }
}
