using Microsoft.Extensions.DependencyInjection.Extensions;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Fusion.Testing;
using ActualLab.Fusion.Tests.DbModel;
using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.Operations.Internal;
using ActualLab.Fusion.Tests.Services;

namespace ActualLab.Fusion.Tests;

public class BytesDbLogEntryFormatTest(ITestOutputHelper @out) : DbLogEntryFormatDbTestBase(@out)
{
    public override DataFormat Format => DataFormat.Bytes;
}

public class TextDbLogEntryFormatTest(ITestOutputHelper @out) : DbLogEntryFormatDbTestBase(@out)
{
    public override DataFormat Format => DataFormat.Text;
}

// The unit-level round-trip lives in DbLogEntrySerializerTest; this one drives both formats
// through the real thing - DbOperationScope writing the row inside the mutation's transaction,
// EF mapping the columns, and the row being read back and applied.
public abstract class DbLogEntryFormatDbTestBase : FusionTestBase
{
    public abstract DataFormat Format { get; }

    protected DbLogEntryFormatDbTestBase(ITestOutputHelper @out) : base(@out)
        => LogEntryFormat = Format;

    [Fact]
    public async Task AReplicatedOperationRoundTripsThroughItsRow()
    {
        if (MustSkip()) return;

        // The registered serializer is the one this test asked for
        Services.GetRequiredService<DbLogEntrySerializer>().Format.Should().Be(Format);

        var kv = Services.GetRequiredService<DbInvalidationModeService>();
        var cGet = await Computed.Capture(() => kv.Get("ab"));

        await Services.Commander().Call(new DeferredInvalidationModeService_Set("ab", 1));

        // The local pass ran
        cGet.IsConsistent().Should().BeFalse();

        var operation = Services.GetRequiredService<OperationCapture>().Operations
            .Single(x => x.Command is DeferredInvalidationModeService_Set);
        operation.Scope!.HasStoredOperation.Should().BeTrue();

        // What a reading host does: pull the row, decode it, apply the recorded calls
        var dbOperation = await GetDbOperation(operation.Uuid);
        AssertPayloadColumns(dbOperation);

        var restored = dbOperation.ToModel(Services.GetRequiredService<DbLogEntrySerializer>());
        restored.Command.Should().BeOfType<DeferredInvalidationModeService_Set>();
        restored.InvalidationCalls.Select(x => x.MethodName)
            .Should().Equal("Get:2", "Count:1", "CountOfLength:2");

        var cAgain = await Computed.Capture(() => kv.Get("ab"));
        await Services.GetRequiredService<FusionOperationCompletionHandler>()
            .ApplyLocalInvalidations(restored.InvalidationCalls);
        cAgain.IsConsistent().Should().BeFalse();
    }

    [Fact]
    public async Task AnEventRoundTripsThroughItsRow()
    {
        if (MustSkip()) return;

        var catcher = Services.GetRequiredService<EventCatcher>();
        await Services.Commander().Call(new EventQueue_Add(new EventQueue_Item("fmt-1", new EventCatcher_Event("fmt-1"), default, default)));

        // The event log reader has to decode the row this format wrote. Events is a reactive
        // state, so When(...) re-evaluates as the reader makes progress.
        await ComputedTest.When(async ct => {
            var events = await catcher.Events.Use(ct);
            events.Should().Contain("fmt-1");
        }, TimeSpan.FromSeconds(10));
    }

    protected override void ConfigureTestServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureTestServices(services, isClient);
        if (isClient)
            return;

        var fusion = services.AddFusion();
        fusion.AddService<DbInvalidationModeService>();
        fusion.AddService<EventQueue>();
        fusion.AddService<EventCatcher>();
        services.AddSingleton<OperationCapture>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IOperationCompletionListener, OperationCapture>(
            c => c.GetRequiredService<OperationCapture>()));
    }

    // Private methods

    private void AssertPayloadColumns(DbOperation dbOperation)
    {
        if (Format is DataFormat.Bytes) {
            dbOperation.CommandData.Should().NotBeNullOrEmpty();
            dbOperation.InvalidationCallsData.Should().NotBeNullOrEmpty();
            dbOperation.CommandJson.Should().BeNull();
            dbOperation.InvalidationCallsJson.Should().BeNull();
        }
        else {
            dbOperation.CommandJson.Should().NotBeNullOrEmpty();
            dbOperation.InvalidationCallsJson.Should().NotBeNullOrEmpty();
            dbOperation.CommandData.Should().BeNull();
            dbOperation.InvalidationCallsData.Should().BeNull();
        }
    }

    private async Task<DbOperation> GetDbOperation(string uuid)
    {
        var dbHub = Services.GetRequiredService<DbHub<TestDbContext>>();
        var dbContext = await dbHub.CreateDbContext(DbShard.Single);
        await using var _ = dbContext.ConfigureAwait(false);
        return await dbContext.Set<DbOperation>().AsQueryable().SingleAsync(x => x.Uuid == uuid);
    }
}
