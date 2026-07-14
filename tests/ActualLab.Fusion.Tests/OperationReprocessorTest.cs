using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Fusion.Tests.DbModel;
using ActualLab.Resilience;
using MessagePack;

namespace ActualLab.Fusion.Tests;

public class SqliteReprocessorTest : OperationReprocessorTestBase
{
    public SqliteReprocessorTest(ITestOutputHelper @out) : base(@out)
        => DbType = FusionTestDbType.Sqlite;
}

public class InMemoryReprocessorTest : OperationReprocessorTestBase
{
    public InMemoryReprocessorTest(ITestOutputHelper @out) : base(@out)
        => DbType = FusionTestDbType.InMemory;
}

public abstract class OperationReprocessorTestBase(ITestOutputHelper @out) : FusionTestBase(@out)
{
    protected override void ConfigureTestServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureTestServices(services, isClient);
        var fusion = services.AddFusion();
        if (!isClient)
            fusion.AddService<ReprocessTester>();
    }

    [Fact]
    public async Task UuidIsPreservedAcrossRetriesTest()
    {
        var tester = Services.GetRequiredService<ReprocessTester>();
        tester.FailBeforeCommit = 2;

        var attempt = await Services.Commander().Call(new ReprocessTester_Run());

        attempt.Should().Be(3); // Succeeded on the 3rd attempt (2 transient failures first)
        tester.ExecutionCount.Should().Be(3);
        tester.ObservedUuids.Distinct().Should().HaveCount(1); // The same Uuid was reused on every retry
    }

    [Fact]
    public async Task NoRetryAfterManualCommitTest()
    {
        // Item 12: a scope committed manually must never re-enter the pipeline, even if the
        // handler throws a transient error afterwards - re-execution would double-apply its effects.
        var tester = Services.GetRequiredService<ReprocessTester>();
        tester.CommitThenThrow = true;

        await Assert.ThrowsAnyAsync<Exception>(
            () => Services.Commander().Call(new ReprocessTester_Run()));

        tester.ExecutionCount.Should().Be(1); // Committed-then-threw -> no retry
    }
}

public class ReprocessTester(IServiceProvider services) : DbServiceBase<TestDbContext>(services), IComputeService
{
    private int _executionCount;

    public int FailBeforeCommit { get; set; }
    public bool CommitThenThrow { get; set; }
    public int ExecutionCount => _executionCount;
    public List<string> ObservedUuids { get; } = [];

    [CommandHandler]
    public virtual async Task<int> OnRun(ReprocessTester_Run command, CancellationToken cancellationToken = default)
    {
        if (Invalidation.IsActive)
            return 0;

        var dbContext = await DbHub.CreateOperationDbContext(cancellationToken).ConfigureAwait(false);
        await using var _ = dbContext.ConfigureAwait(false);

        var commandContext = CommandContext.GetCurrent();
        var attempt = Interlocked.Increment(ref _executionCount);
        lock (ObservedUuids)
            ObservedUuids.Add(commandContext.Operation.Uuid);

        if (CommitThenThrow) {
            var scope = DbOperationScope<TestDbContext>.TryGet(commandContext)!;
            await scope.Commit(cancellationToken).ConfigureAwait(false);
            throw new ReprocessTestException();
        }
        if (attempt <= FailBeforeCommit)
            throw new ReprocessTestException();

        return attempt;
    }
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record ReprocessTester_Run : ICommand<int>;

public class ReprocessTestException() : Exception("A transient test error."), ITransientException;
