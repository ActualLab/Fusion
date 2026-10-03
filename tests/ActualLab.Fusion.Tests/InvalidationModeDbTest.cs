using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Fusion.Operations;
using ActualLab.Fusion.Operations.Internal;
using ActualLab.Fusion.Tests.Services;
using ActualLab.Interception;

namespace ActualLab.Fusion.Tests;

// InvalidationMode.Replicated needs a stored operation to carry its recorded calls, which is why
// its tests run on a real DbOperationScope rather than the transient one the other modes use.
public class InvalidationModeDbTest(ITestOutputHelper @out) : FusionTestBase(@out)
{
    [Fact]
    public async Task Replicated_InvalidatesLocallyAndRecordsCallsOnTheOperation()
    {
        if (MustSkip()) return;

        var kv = Services.GetRequiredService<DbInvalidationModeService>();
        var cGet = await Computed.Capture(() => kv.Get("ab"));
        var cCount = await Computed.Capture(() => kv.Count());
        var cLength = await Computed.Capture(() => kv.CountOfLength(2));

        await Services.Commander().Call(new DeferredInvalidationModeService_Set("ab", 1));

        cGet.IsConsistent().Should().BeFalse();
        cCount.IsConsistent().Should().BeFalse();
        cLength.IsConsistent().Should().BeFalse();
        // A deferred-mode handler has no "if (Invalidation.IsActive)" guard, so a replay
        // would run its mutation a second time
        kv.MutationCount.Should().Be(1);

        var calls = GetOperation().InvalidationCalls;
        // The RPC-style name carries the parameter count, CancellationToken included
        calls.Select(x => x.MethodName).Should().Equal("Get:2", "Count:1", "CountOfLength:2");
        // A recorded call must resolve on a host running a different build of the same assembly
        calls[0].ServiceType.AssemblyQualifiedName.Should().NotContain("Version=");
        calls[0].Arguments.Get<string>(0).Should().Be("ab");
        calls[2].Arguments.Get<int>(0).Should().Be(2);
    }

    [Fact]
    public async Task ADelegatingCommand_TakesTheReplicatedModeOfItsNestedCommand()
    {
        if (MustSkip()) return;

        // Regression: the outer command declares Any, so the nested Replicated handler decides -
        // before this rule the operation was deferred as Local and never left the origin host
        var kv = Services.GetRequiredService<DbInvalidationModeService>();
        var cGet = await Computed.Capture(() => kv.Get("ab"));

        await Services.Commander().Call(new DbDelegatingOverReplicated_Set("ab", 1));

        cGet.IsConsistent().Should().BeFalse();
        var operation = Services.GetRequiredService<OperationCapture>().Operations
            .Single(x => x.Command is DbDelegatingOverReplicated_Set);
        // Only Replicated records its calls onto the operation row
        operation.InvalidationCalls.Select(x => x.MethodName).Should().Equal("Get:2", "Count:1", "CountOfLength:2");
        operation.Scope!.HasStoredOperation.Should().BeTrue();
    }

    [Fact]
    public async Task Replicated_RecordedCallsSurviveTheOperationLogRow()
    {
        if (MustSkip()) return;

        await Services.Commander().Call(new DeferredInvalidationModeService_Set("ab", 1));
        var operation = GetOperation();

        var restored = new DbOperation(operation).ToModel().InvalidationCalls;

        restored.Select(x => x.MethodName).Should().Equal("Get:2", "Count:1", "CountOfLength:2");
        restored.Select(x => x.ServiceType).Should().AllBeEquivalentTo(operation.InvalidationCalls[0].ServiceType);
        // The arguments keep their exact types: they travel in RPC's own encoding, against an
        // ArgumentListType the method identity makes known on the other side
        restored[0].Arguments.Get<string>(0).Should().Be("ab");
        restored[2].Arguments.Get<int>(0).Should().Be(2);
        await Services.GetRequiredService<FusionOperationCompletionHandler>()
            .ApplyLocalInvalidations(restored);
    }

    [Fact]
    public async Task Replicated_StoresTheOperation()
    {
        if (MustSkip()) return;

        await Services.Commander().Call(new DeferredInvalidationModeService_Set("ab", 1));

        // The row is what carries the calls to the other hosts, so "auto" has to keep it
        GetOperation().Scope!.HasStoredOperation.Should().BeTrue();
    }

    [Fact]
    public async Task Local_DoesNotStoreTheOperation()
    {
        if (MustSkip()) return;

        await Services.Commander().Call(new DbLocalInvalidationModeService_Set("ab", 1));

        var operation = Services.GetRequiredService<OperationCapture>().Operations
            .Single(x => x.Command is DbLocalInvalidationModeService_Set);
        // Nothing reads this operation on the other hosts: its handler isn't replayed and
        // it carries no invalidation calls
        operation.Scope!.HasStoredOperation.Should().BeFalse();
    }

    [Fact]
    public async Task Local_AppliesAManuallyRecordedCallOnTheOriginToo()
    {
        if (MustSkip()) return;

        // Regression: ApplyDeferredInvalidations returned as soon as it had run the Local blocks,
        // so a call the handler added by hand was applied on every other host but not on this one
        var kv = Services.GetRequiredService<DbLocalPlusManualCallService>();
        var cGet = await Computed.Capture(() => kv.Get("ab"));
        var cCount = await Computed.Capture(() => kv.Count());

        await Services.Commander().Call(new DbLocalPlusManualCall_Set("ab", 1));

        cGet.IsConsistent().Should().BeFalse();   // named by the deferred block
        cCount.IsConsistent().Should().BeFalse(); // named by hand
    }

    protected override void ConfigureTestServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureTestServices(services, isClient);
        if (isClient)
            return;

        var fusion = services.AddFusion();
        fusion.AddService<DbInvalidationModeService>();
        fusion.AddService<DbLocalInvalidationModeService>();
        fusion.AddService<DbLocalPlusManualCallService>();
        fusion.AddService<DbDelegatingOverReplicatedService>();
        fusion.AddService<IDbProtectedInvalidationService, DbProtectedInvalidationService>();
        services.AddSingleton<OperationCapture>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IOperationCompletionListener, OperationCapture>(
            c => c.GetRequiredService<OperationCapture>()));
    }

    [Fact]
    public async Task Replicated_RecordsAProtectedComputeMethod()
    {
        if (MustSkip()) return;

        var kv = Services.GetRequiredService<IDbProtectedInvalidationService>();
        await Services.Commander().Call(new DbProtectedInvalidationService_Set("ab", 1));

        var operation = Services.GetRequiredService<OperationCapture>().Operations
            .Single(x => x.Command is DbProtectedInvalidationService_Set);
        var calls = operation.InvalidationCalls;
        calls.Select(x => x.MethodName).Should().Equal("Get:2", "GetInternal:2");
        // Get is on the interface the service is registered as, so that's what identifies it;
        // GetInternal is protected, and only the implementation declares it
        calls[0].ServiceType.Resolve().Should().Be(typeof(IDbProtectedInvalidationService));
        calls[1].ServiceType.Resolve().Should().Be(typeof(DbProtectedInvalidationService));

        // Both have to survive the operation log row and apply on the host that reads it
        var restored = new DbOperation(operation).ToModel().InvalidationCalls;
        var cGet = await Computed.Capture(() => kv.Get("ab"));
        var cInternal = await Computed.Capture(() => kv.GetInternalAsPublic("ab"));
        await Services.GetRequiredService<FusionOperationCompletionHandler>()
            .ApplyLocalInvalidations(restored);

        cGet.IsConsistent().Should().BeFalse();
        cInternal.IsConsistent().Should().BeFalse();
    }

    // Private methods

    private Operation GetOperation()
        => Services.GetRequiredService<OperationCapture>().Operations
            .Single(x => x.Command is DeferredInvalidationModeService_Set);
}
