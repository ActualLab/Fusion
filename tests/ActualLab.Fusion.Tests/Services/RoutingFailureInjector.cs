using ActualLab.Fusion.Operations.Internal;
using ActualLab.Interception;
using ActualLab.CommandR.Operations;

namespace ActualLab.Fusion.Tests.Services;

// Makes a routed invalidation fail on demand, so a test can drive the path the Distributed event
// row exists for: the origin didn't finish routing, and a reader has to replay the whole list.
public sealed class RoutingFailureInjector
{
    private int _routedApplyCount;

    // How many routed calls to let through before failing; -1 disables the injection, and the
    // failure disarms itself once it fires - the replay after it has to succeed
    public int FailAfterCount { get; set; } = -1;
    public int RoutedApplyCount => Volatile.Read(ref _routedApplyCount);

    public void OnRoutedApply()
    {
        var count = Interlocked.Increment(ref _routedApplyCount);
        if (FailAfterCount < 0 || count <= FailAfterCount)
            return;

        FailAfterCount = -1;
        throw new InvalidOperationException("Injected routing failure.");
    }
}

public sealed class InjectedFailureOperationCompletionHandler(IServiceProvider services)
    : FusionOperationCompletionHandler(services)
{
    private RoutingFailureInjector Injector { get; }
        = services.GetRequiredService<RoutingFailureInjector>();

    protected override Task ApplyInvalidation(
        ServiceCall call, bool mustResolve = false, CancellationToken cancellationToken = default)
    {
        // mustResolve is set only on the routed pass, which is the one that must not be lost
        if (mustResolve)
            Injector.OnRoutedApply();

        return base.ApplyInvalidation(call, mustResolve, cancellationToken);
    }
}
