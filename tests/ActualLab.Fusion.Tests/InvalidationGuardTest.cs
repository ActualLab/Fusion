using ActualLab.CommandR.Operations;
using ActualLab.Fusion.Tests.Services;
using MessagePack;

namespace ActualLab.Fusion.Tests;

// InvalidationGuard is the only thing standing between a command and its operation scope while a
// pass is active: every scope provider down the chain used to opt out on Invalidation.IsActive of
// its own accord, and those checks are gone now that the guard rejects such a command outright.
//
// Only a *nested* command can get there with the pass still flowing. Commander.Run suppresses the
// execution context flow for an outermost command, so the ambient ComputeContext - and with it
// IsActive - never reaches it. Every case below therefore runs its command from inside a handler.
public class InvalidationGuardTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    [Fact]
    public async Task ANestedCommandDuringAnInvalidationPassThrows()
    {
        var services = CreateHostServices();
        var service = services.GetRequiredService<InvalidationGuardTestService>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => services.Commander().Call(new InvalidationGuardTestService_RunNested(PassKind.Invalidation)));

        // The guard throws before the inner handler's body, so the mutation never happened
        service.InnerRunCount.Should().Be(0);
    }

    [Fact]
    public async Task ANestedCommandDuringACapturePassThrows()
    {
        var services = CreateHostServices();
        var service = services.GetRequiredService<InvalidationGuardTestService>();

        // A capture pass isn't an invalidation pass - it carries no CallOptions.Invalidate - but a
        // command started from one would run for real inside the operation's open transaction, so
        // the guard has to reject it too
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => services.Commander().Call(new InvalidationGuardTestService_RunNested(PassKind.Capture)));

        service.InnerRunCount.Should().Be(0);
    }

    [Fact]
    public async Task ANestedCommandOutsideAPassRuns()
    {
        var services = CreateHostServices();
        var service = services.GetRequiredService<InvalidationGuardTestService>();

        // What the guard must not do: reject the nested commands that CommandR exists to compose
        await services.Commander().Call(new InvalidationGuardTestService_RunNested(PassKind.None));

        service.InnerRunCount.Should().Be(1);
    }

    [Fact]
    public async Task AnOutermostCommandIsUnaffectedByAnAmbientPass()
    {
        var services = CreateHostServices();
        var service = services.GetRequiredService<InvalidationGuardTestService>();

        // Commander.Run suppresses the context flow for an outermost command, so the pass doesn't
        // reach it and there is nothing for the guard to reject. This pins the asymmetry the guard's
        // own comment relies on - if it ever stopped holding, the case above would be a false pass.
        using (Invalidation.Begin(new InvalidationSource(nameof(InvalidationGuardTest)))) {
            Invalidation.IsActive.Should().BeTrue();
            await services.Commander().Call(new InvalidationGuardTestService_Inner());
        }

        service.InnerRunCount.Should().Be(1);
    }

    // Private methods

    private IServiceProvider CreateHostServices()
        => CreateServices(services => {
            var fusion = services.AddFusion();
            fusion.AddService<InvalidationGuardTestService>();
        });
}

public enum PassKind
{
    None = 0,
    Invalidation,
    Capture,
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record InvalidationGuardTestService_RunNested(
    [property: DataMember, MemoryPackOrder(0), Key(0)] PassKind PassKind
) : ICommand<Unit>;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record InvalidationGuardTestService_Inner : ICommand<Unit>;

public class InvalidationGuardTestService(ICommander commander) : IComputeService
{
    private int _innerRunCount;

    public int InnerRunCount => Volatile.Read(ref _innerRunCount);

    [CommandHandler]
    [DeferredInvalidationMode(DeferredInvalidationMode.Local)]
    public virtual async Task OnRunNested(
        InvalidationGuardTestService_RunNested command,
        CancellationToken cancellationToken = default)
    {
        // A CommandContext is current here, so the inner call below is nested: Commander.Run
        // won't suppress the flow, and whatever pass is open reaches the guard
        switch (command.PassKind) {
        case PassKind.Invalidation:
            using (Invalidation.Begin(new InvalidationSource(nameof(InvalidationGuardTestService))))
                await commander.Call(new InvalidationGuardTestService_Inner(), cancellationToken)
                    .ConfigureAwait(false);
            break;
        case PassKind.Capture:
            using (new ComputeContext(new List<ServiceCall>()).Activate())
                await commander.Call(new InvalidationGuardTestService_Inner(), cancellationToken)
                    .ConfigureAwait(false);
            break;
        default:
            await commander.Call(new InvalidationGuardTestService_Inner(), cancellationToken)
                .ConfigureAwait(false);
            break;
        }
    }

    [CommandHandler]
    [DeferredInvalidationMode(DeferredInvalidationMode.Local)]
    public virtual Task OnInner(
        InvalidationGuardTestService_Inner command,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _innerRunCount);
        return Task.CompletedTask;
    }
}
