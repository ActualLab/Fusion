using System.Diagnostics;
using System.Diagnostics.Metrics;
using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

// These tests count every activity and measurement of a method, and both the activity source and the meter
// are process-global - so they run exclusively: in a full run, performance tests call the same methods
// millions of times in parallel.

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcTraceMetricsTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        services.AddRpc().AddServerAndClient<ITestRpcService, TestRpcService>();
    }

    [Fact]
    public async Task InboundTraceIncludesResponseSerializationTest()
    {
        var stoppedActivities = new ConcurrentQueue<Activity>();
        var errorTypeTag = new KeyValuePair<string, object?>("error.type", typeof(InvalidDataException).FullName);
        var measurements = new ConcurrentQueue<KeyValuePair<string, object?>[]>();
        using var activityListener = new ActivityListener {
            ShouldListenTo = source => source == RpcInstruments.ActivitySource,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = stoppedActivities.Enqueue,
        };
        ActivitySource.AddActivityListener(activityListener);
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, targetListener) => {
            if (ReferenceEquals(instrument, RpcInstruments.InboundDurationHistogram))
                targetListener.EnableMeasurementEvents(instrument);
        };
        // Scoped by the error tag: calls of the tests that ran earlier may still be completing
        meterListener.SetMeasurementEventCallback<double>((_, _, tags, _) => {
            var tagArray = tags.ToArray();
            if (tagArray.Contains(errorTypeTag))
                measurements.Enqueue(tagArray);
        });
        meterListener.Start();

        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var peer = connection.ServerPeer;
        await peer.WhenConnected();
        var method = services.RpcHub().ServiceRegistry[typeof(ITestRpcService)]["Div:2"];
        var inboundMessage = new RpcInboundMessage(method.CallType.Id, 1, method.Ref, default, null) {
            Arguments = ArgumentList.New<int?, int>(6, 2),
        };
        var inboundContext = new RpcInboundContext(peer, inboundMessage, default);
        var inboundCall = inboundContext.Call;
        inboundCall.ResultTask = Task.FromResult<int?>(3);
        inboundCall.Trace = method.Tracer!.StartInboundTrace(inboundCall);
        var activity = inboundCall.Trace!.Activity!;
        var responseContext = new RpcOutboundContext(peer, inboundCall.Id) {
            InboundCall = inboundCall,
        };
        var responseCall = responseContext.PrepareCallForSendNoWait(
            services.GetRequiredService<RpcSystemCallSender>().OkMethodDef,
            ArgumentList.New<int?>(3))!;
        var responseMessage = responseCall.CreateOutboundMessage(
            inboundCall.Id,
            needsPolymorphism: false,
            RpcSendHandlers.CompleteInboundCall);
        var serializationError = new InvalidDataException("Response serialization failed.");

        stoppedActivities.Should().NotContain(activity);
        RpcSendHandlers.CompleteInboundCall(
            peer.ConnectionState.Value.Transport!, responseMessage, serializationError);

        stoppedActivities.Should().ContainSingle(a => a == activity);
        activity.Status.Should().Be(ActivityStatusCode.Error);
        activity.GetTagItem("error.type").Should().Be(typeof(InvalidDataException).FullName);
        measurements.Should().ContainSingle();
    }

    [Fact]
    public async Task TraceRerouteActivityTest()
    {
        var stoppedActivities = new ConcurrentQueue<Activity>();
        var clientDurations = new ConcurrentQueue<double>();
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == RpcInstruments.ActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = stoppedActivities.Enqueue,
        };
        ActivitySource.AddActivityListener(listener);

        await using var services = CreateServices();
        var clientPeer = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend).ClientPeer;
        var method = services.RpcHub().ServiceRegistry[typeof(ITestRpcService)]["Div:2"];
        var tracer = (RpcDefaultCallTracer)method.Tracer!;

        // Started only once the method is known: the meter is process-global, so measurements
        // have to be scoped by their rpc.method tag or parallel tests bleed into clientDurations
        var methodName = method.FullName;
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, targetListener) => {
            if (ReferenceEquals(instrument, RpcInstruments.OutboundDurationHistogram))
                targetListener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<double>((_, value, tags, _) => {
            foreach (var tag in tags)
                if (tag.Key == "rpc.method" && Equals(tag.Value, methodName))
                    clientDurations.Enqueue(value);
        });
        meterListener.Start();

        using var parent = new Activity("parent").SetIdFormat(ActivityIdFormat.W3C).Start();
        var parentContext = parent.Context;

        var context = new RpcOutboundContext(clientPeer);
        var firstCall = context.PrepareCall(method, ArgumentList.New<int?, int>(6, 2))!;
        var firstActivity = context.Trace!.Activity!;
        firstCall.Register();
        firstCall.SetMustRerouteError();

        var secondCall = context.PrepareReroutedCall()!;
        var secondActivity = context.Trace!.Activity!;
        var secondMessage = secondCall.CreateOutboundMessage(
            secondCall.Id,
            method.HasPolymorphicArguments,
            sendHandler: null);
        secondCall.Register();
        secondCall.SetResult(3, context: null);

        // The listener is process-global, so other tests running in parallel feed it too -
        // a.TraceId is what narrows this to our own parent activity
        var outboundActivities = stoppedActivities
            .Where(a => a.TraceId == parentContext.TraceId
                && a.Kind == ActivityKind.Client
                && a.DisplayName == tracer.OutboundCallName)
            .ToArray();
        outboundActivities.Should().HaveCount(2);
        outboundActivities.Should().OnlyContain(a => a.ParentSpanId == parentContext.SpanId);
        outboundActivities.Select(a => a.SpanId).Should().OnlyHaveUniqueItems();
        firstActivity.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        secondActivity.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        secondActivity.SpanId.Should().NotBe(firstActivity.SpanId);
        RpcActivityInjector.TryExtract(secondMessage.Headers, out var propagatedContext).Should().BeTrue();
        propagatedContext.TraceId.Should().Be(secondActivity.TraceId);
        propagatedContext.SpanId.Should().Be(secondActivity.SpanId);
        secondActivity.GetTagItem("rpc.system.name").Should().Be("actuallab.rpc");
        secondActivity.GetTagItem("rpc.method").Should().Be(method.FullName);
        clientDurations.Should().ContainSingle();
        clientDurations.Single().Should().BeGreaterThanOrEqualTo(0);
    }
}
