using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.EntityFramework.LogProcessing;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Fusion.EntityFramework.Operations.LogProcessing;
using ActualLab.Fusion.Operations.Internal;
using ActualLab.Fusion.Tests.DbModel;
using ActualLab.Fusion.Tests.Services;

namespace ActualLab.Fusion.Tests;

// The recovery half of InvalidationMode.Distributed: the origin's routing is fire-and-forget, so
// the event row stays New until it lands. These tests break the routing and let the real
// DbEventLogReader -> DbEventProcessor path finish the job.
public class DistributedInvalidationRecoveryDbTest(ITestOutputHelper @out) : FusionTestBase(@out)
{
    private static readonly TimeSpan ReplayDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RecoveryTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task RoutingFailure_IsReplayedByTheEventLogReader()
    {
        if (MustSkip()) return;

        var injector = Services.GetRequiredService<RoutingFailureInjector>();
        injector.FailAfterCount = 0; // Nothing gets routed on the origin's attempt

        var (operation, cGet, cCount) = await Mutate("ab", originRoutedApplyCount: 1);
        // The origin's routing failed, so the row it left behind is the only way out
        (await GetDbEvent(operation))!.State.Should().Be(LogEntryState.New);

        await AssertRecovered(operation, cGet, cCount);
        // One failed routed call, then the replay routes the whole list
        injector.RoutedApplyCount.Should().Be(1 + operation.InvalidationCalls.Count);
    }

    [Fact]
    public async Task PartialRoutingFailure_ReplaysTheWholeRecordList()
    {
        if (MustSkip()) return;

        var injector = Services.GetRequiredService<RoutingFailureInjector>();
        injector.FailAfterCount = 1; // The first call routes, the second one doesn't

        var (operation, cGet, cCount) = await Mutate("ab", originRoutedApplyCount: 2);
        operation.InvalidationCalls.Count.Should().BeGreaterThan(1);
        (await GetDbEvent(operation))!.State.Should().Be(LogEntryState.New);

        await AssertRecovered(operation, cGet, cCount);
        // Routing has no per-call progress marker, so the replay redoes the calls that did land -
        // applying an already-invalidated computed costs nothing
        injector.RoutedApplyCount.Should().Be(2 + operation.InvalidationCalls.Count);
    }

    protected override void ConfigureTestServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureTestServices(services, isClient);
        if (isClient)
            return;

        var fusion = services.AddFusion();
        fusion.AddService<DbDistributedInvalidationModeService>();
        services.AddSingleton<RoutingFailureInjector>();
        services.AddSingleton<FusionOperationCompletionHandler>(
            c => new InjectedFailureOperationCompletionHandler(c));
        services.AddSingleton<OperationCapture>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IOperationCompletionListener, OperationCapture>(
            c => c.GetRequiredService<OperationCapture>()));

        // The defaults make the reader wait 15s before it may claim the row, which is the right
        // window in production and far too long for a test
        services.AddSingleton(_ => new DbOperationScope<TestDbContext>.Options {
            OperationAsEventRecoveryCompletionDelay = ReplayDelay,
        });
        services.AddSingleton(_ => new DbEventLogReader<TestDbContext>.Options {
            CheckPeriod = TimeSpan.FromSeconds(0.5).ToRandom(0.1),
        });
    }

    // Private methods

    // originRoutedApplyCount: how many routed calls the origin's own attempt makes before the
    // injected failure stops it
    private async Task<(Operation Operation, Computed<int> Get, Computed<int> Count)> Mutate(
        string key, int originRoutedApplyCount)
    {
        var kv = Services.GetRequiredService<DbDistributedInvalidationModeService>();
        var injector = Services.GetRequiredService<RoutingFailureInjector>();
        await Services.Commander().Call(new DbDistributedInvalidationModeService_Set(key, 1));

        // Routing is fire-and-forget, and a call that did route invalidates the same computed the
        // capture below is about to take - so let the origin's attempt finish first
        await TestExt.When(
            () => injector.RoutedApplyCount.Should().Be(originRoutedApplyCount),
            RecoveryTimeout);

        // The origin invalidates its own copies before it routes anything, so what the replay has
        // to reach is whatever was captured after that
        var cGet = await Computed.Capture(() => kv.Get(key));
        var cCount = await Computed.Capture(() => kv.Count());
        cGet.IsConsistent().Should().BeTrue();
        cCount.IsConsistent().Should().BeTrue();
        return (GetOperation(key), cGet, cCount);
    }

    private async Task AssertRecovered(Operation operation, Computed<int> cGet, Computed<int> cCount)
    {
        await TestExt.When(() => {
            cGet.IsConsistent().Should().BeFalse();
            cCount.IsConsistent().Should().BeFalse();
        }, RecoveryTimeout);

        await TestExt.When(async () => {
            var dbEvent = await GetDbEvent(operation);
            dbEvent!.State.Should().Be(LogEntryState.Processed);
        }, RecoveryTimeout);
    }

    private Operation GetOperation(string key)
        => Services.GetRequiredService<OperationCapture>().Operations
            .Single(x => x.Command is DbDistributedInvalidationModeService_Set command && command.Key == key);

    private async Task<DbEvent?> GetDbEvent(Operation operation)
    {
        var dbHub = Services.GetRequiredService<DbHub<TestDbContext>>();
        var dbContext = await dbHub.CreateDbContext(DbShard.Single);
        await using var _ = dbContext.ConfigureAwait(false);
        var uuid = string.Concat("~op-", operation.Uuid);
        return await dbContext.Set<DbEvent>().AsQueryable().FirstOrDefaultAsync(x => x.Uuid == uuid);
    }
}
