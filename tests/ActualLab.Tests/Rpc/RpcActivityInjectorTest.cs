using System.Diagnostics;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Tests.Rpc;

public class RpcActivityInjectorTest(ITestOutputHelper @out) : TestBase(@out)
{
    private const string TraceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

    [Fact]
    public void CompliantTraceContextIsAdopted()
    {
        var traceState = NewTraceState(RpcActivityInjector.MaxTraceStateLength);
        var headers = NewHeaders(TraceParent, traceState);

        RpcActivityInjector.TryExtract(headers, out var activityContext).Should().BeTrue();

        activityContext.TraceState.Should().Be(traceState);
    }

    [Fact]
    public void OverLimitTraceStateIsDropped()
    {
        var headers = NewHeaders(TraceParent, NewTraceState(RpcActivityInjector.MaxTraceStateLength + 1));

        RpcActivityInjector.TryExtract(headers, out var activityContext).Should().BeTrue();

        activityContext.TraceState.Should().BeNull();
        activityContext.TraceId.Should().Be(ActivityTraceId.CreateFromString(TraceParent.AsSpan(3, 32)));
    }

    [Fact]
    public void OverLimitTraceParentIsRejected()
    {
        var headers = NewHeaders(TraceParent + "-extra", null);

        RpcActivityInjector.TryExtract(headers, out var activityContext).Should().BeFalse();

        activityContext.Should().Be(default(ActivityContext));
    }

    // Private methods

    private static RpcHeader[] NewHeaders(string traceParent, string? traceState)
    {
        var headers = new List<RpcHeader> { new(WellKnownRpcHeaders.W3CTraceParent, traceParent) };
        if (traceState is not null)
            headers.Add(new RpcHeader(WellKnownRpcHeaders.W3CTraceState, traceState));

        return headers.ToArray();
    }

    private static string NewTraceState(int length)
        => string.Concat(Enumerable.Repeat("k=v,", 1 + (length / 4)))[..length];
}
