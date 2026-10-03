using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.EntityFramework.LogProcessing;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Fusion.Operations;
using ActualLab.Fusion.Operations.Internal;
using ActualLab.Fusion.Tests.DbModel;
using ActualLab.Fusion.Tests.Services;
using ActualLab.Interception;

namespace ActualLab.Fusion.Tests;

// InvalidationMode.Distributed pays for its invalidation once rather than on every host: the calls
// travel in an event row that a single host claims, and the origin applies and routes them itself.
public class DistributedInvalidationModeDbTest(ITestOutputHelper @out) : FusionTestBase(@out)
{
    [Fact]
    public async Task Distributed_InvalidatesOnTheOriginAndRecordsCalls()
    {
        if (MustSkip()) return;

        var kv = Services.GetRequiredService<DbDistributedInvalidationModeService>();
        var cGet = await Computed.Capture(() => kv.Get("ab"));
        var cCount = await Computed.Capture(() => kv.Count());

        await Services.Commander().Call(new DbDistributedInvalidationModeService_Set("ab", 1));

        // The origin applies before the mutating call returns - exactly like Local
        cGet.IsConsistent().Should().BeFalse();
        cCount.IsConsistent().Should().BeFalse();
        kv.MutationCount.Should().Be(1);

        var calls = GetOperation().InvalidationCalls;
        calls.Select(x => x.MethodName).Should().Equal("Get:2", "Count:1");
        calls[0].Arguments.Get<string>(0).Should().Be("ab");
    }

    [Fact]
    public async Task Distributed_StoresAnEventInsteadOfAnOperation()
    {
        if (MustSkip()) return;

        await Services.Commander().Call(new DbDistributedInvalidationModeService_Set("ab", 1));

        var scope = GetOperation().Scope!;
        scope.StoreMode.Should().Be(OperationStoreMode.Event);
        // An operation row would be read by every host only to find calls it must not apply itself
        scope.HasStoredOperation.Should().BeFalse();
    }

    [Fact]
    public async Task Distributed_EventRowCarriesTheCallsAndTheCommand()
    {
        if (MustSkip()) return;

        await Services.Commander().Call(new DbDistributedInvalidationModeService_Set("ab", 1));
        var operation = GetOperation();

        var dbEvent = await GetDbEvent(operation);
        dbEvent.Should().NotBeNull();
        var value = dbEvent!.ToModel().Value;
        var invalidationEvent = value.Should().BeOfType<OperationCompletion>().Subject;
        invalidationEvent.InvalidationCalls.Select(x => x.MethodName).Should().Equal("Get:2", "Count:1");
        // The command is there so the host application can route the event the same way
        invalidationEvent.Command.Should().BeOfType<DbDistributedInvalidationModeService_Set>();
    }

    [Fact]
    public async Task Distributed_CompletesTheEventRowOnceTheOriginIsDone()
    {
        if (MustSkip()) return;

        await Services.Commander().Call(new DbDistributedInvalidationModeService_Set("ab", 1));

        // Routing is fire-and-forget, so the completion lands shortly after the command returns.
        // The origin routes everything itself, so the recovery replay must never have to run.
        var operation = GetOperation();
        await TestExt.When(async () => {
            var dbEvent = await GetDbEvent(operation);
            dbEvent!.State.Should().Be(LogEntryState.Processed);
        }, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DeferredInvalidationEvent_ReplaysTheRecordedCalls()
    {
        if (MustSkip()) return;

        await Services.Commander().Call(new DbDistributedInvalidationModeService_Set("ab", 1));
        var calls = GetOperation().InvalidationCalls;

        var kv = Services.GetRequiredService<DbDistributedInvalidationModeService>();
        var cGet = await Computed.Capture(() => kv.Get("ab"));
        var cCount = await Computed.Capture(() => kv.Count());
        cGet.IsConsistent().Should().BeTrue();

        // What the host that claims the event row does with it
        await Services.Commander().Call(OperationCompletion.New(null, calls.ToArray()));

        cGet.IsConsistent().Should().BeFalse();
        cCount.IsConsistent().Should().BeFalse();
    }

    protected override void ConfigureTestServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureTestServices(services, isClient);
        if (isClient)
            return;

        var fusion = services.AddFusion();
        fusion.AddService<DbDistributedInvalidationModeService>();
        services.AddSingleton<OperationCapture>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IOperationCompletionListener, OperationCapture>(
            c => c.GetRequiredService<OperationCapture>()));
    }

    // Private methods

    private Operation GetOperation()
        => Services.GetRequiredService<OperationCapture>().Operations
            .Single(x => x.Command is DbDistributedInvalidationModeService_Set);

    private async Task<DbEvent?> GetDbEvent(Operation operation)
    {
        var dbHub = Services.GetRequiredService<DbHub<TestDbContext>>();
        var dbContext = await dbHub.CreateDbContext(DbShard.Single);
        await using var _ = dbContext.ConfigureAwait(false);
        var uuid = string.Concat("~op-", operation.Uuid);
        return await dbContext.Set<DbEvent>().AsQueryable().FirstOrDefaultAsync(x => x.Uuid == uuid);
    }
}
