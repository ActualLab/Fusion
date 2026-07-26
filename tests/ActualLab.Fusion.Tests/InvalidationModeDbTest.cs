using Microsoft.Extensions.DependencyInjection.Extensions;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Fusion.Operations;
using ActualLab.Fusion.Operations.Internal;
using ActualLab.Fusion.Tests.Services;

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

        await Services.Commander().Call(new InvalidationModeService_Set("ab", 1));

        cGet.IsConsistent().Should().BeFalse();
        cCount.IsConsistent().Should().BeFalse();
        cLength.IsConsistent().Should().BeFalse();
        // A deferred-mode handler has no "if (Invalidation.IsActive)" guard, so a replay
        // would run its mutation a second time
        kv.MutationCount.Should().Be(1);

        var calls = GetOperation().InvalidationCalls;
        calls.Select(x => x.MethodName).Should().Equal("Get", "Count", "CountOfLength");
        // A recorded call must resolve on a host running a different build of the same assembly
        calls[0].ServiceType.AssemblyQualifiedName.Should().NotContain("Version=");
        calls[0].Arguments.Should().Equal("ab");
        calls[1].Arguments.Should().BeEmpty();
        calls[2].Arguments.Should().Equal(2);
    }

    [Fact]
    public async Task Replicated_RecordedCallsSurviveTheOperationLogRow()
    {
        if (MustSkip()) return;

        await Services.Commander().Call(new InvalidationModeService_Set("ab", 1));
        var operation = GetOperation();

        var restored = new DbOperation(operation).ToModel().InvalidationCalls;

        restored.Select(x => x.MethodName).Should().Equal("Get", "Count", "CountOfLength");
        restored.Select(x => x.ServiceType).Should().AllBeEquivalentTo(operation.InvalidationCalls[0].ServiceType);
        restored[0].Arguments.Should().Equal("ab");
        restored[1].Arguments.Should().BeEmpty();
        // JSON has no int/long distinction for a boxed argument - InvalidationCallApplier.Coerce
        // is what brings it back to the parameter's type
        restored[2].Arguments.Should().HaveCount(1);
        await Services.GetRequiredService<InvalidationCallApplier>()
            .Apply(restored, new InvalidationSource("test"));
    }

    protected override void ConfigureTestServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureTestServices(services, isClient);
        if (isClient)
            return;

        services.AddFusion().AddService<DbInvalidationModeService>();
        services.AddSingleton<OperationCapture>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IOperationCompletionListener, OperationCapture>(
            c => c.GetRequiredService<OperationCapture>()));
    }

    // Private methods

    private Operation GetOperation()
        => Services.GetRequiredService<OperationCapture>().Operations
            .Single(x => x.Command is InvalidationModeService_Set);
}
