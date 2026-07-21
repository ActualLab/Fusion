using System.Diagnostics;
using System.Diagnostics.Metrics;
using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcBasicTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var commander = services.AddCommander();
        commander.AddService<TestRpcService>();
        commander.AddService<TestRpcBackend>();

        var rpc = services.AddRpc();
        rpc.AddServerAndClient<ITestRpcService, TestRpcService>();
        rpc.AddServerAndClient<ITestRpcBackend, TestRpcBackend>();
        services.AddSingleton<RpcPeerOptions>(_ => RpcPeerOptions.Default with {
            UseRandomHandshakeIndex = true,
            PeerFactory = (hub, route) => route.Ref.IsServer
                ? new RpcServerPeer(hub, route)
                : new RpcClientPeer(hub, route)
        });
    }

    [Fact]
    public async Task WhenConnectedTest1()
    {
        for (var i = 0; i < 5; i++) {
            await using var services = CreateServices();
            var peer = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend).ClientPeer;

            var whenConnectedTask = peer.WhenConnected();
            await peer.DisposeAsync();

            var whenConnectedResult = await whenConnectedTask.ResultAwait();
            if (whenConnectedResult.Error is RpcReconnectFailedException)
                return;
        }
        Assert.Fail("whenConnectedResult.Error was never of an expected type.");
    }

    [Fact]
    public async Task WhenConnectedTest2()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var peer = connection.ClientPeer;

        await peer.WhenConnected();
        peer.WhenConnected().IsCompletedSuccessfully.Should().BeTrue();

        await connection.Disconnect();
        var whenConnectedResult = await peer.WhenConnected(TimeSpan.FromSeconds(1)).ResultAwait();
        whenConnectedResult.Error.Should().BeOfType<TimeoutException>();

        var whenConnectedTask = peer.WhenConnected(TimeSpan.FromHours(1));
        await peer.DisposeAsync();
        whenConnectedResult = await whenConnectedTask.ResultAwait();
        whenConnectedResult.Error.Should().BeOfType<RpcReconnectFailedException>();
    }

    [Fact]
    public async Task TraceTest()
    {
        await using var services = CreateServices(s => {
            s.AddSingleton<RpcDiagnosticsOptions>(_ => RpcDiagnosticsOptions.Default with {
                CallTracerFactory = methodDef => new TestRpcCallTracer(methodDef),
            });
        });
        var clientPeer = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend).ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();

        var divMethod = services.RpcHub().ServiceRegistry[typeof(ITestRpcService)]["Div:2"];
        var divTracer = (TestRpcCallTracer)divMethod.Tracer!;

        divTracer.EnterCount.Should().Be(0);
        divTracer.ExitCount.Should().Be(0);
        divTracer.ErrorCount.Should().Be(0);
        (await client.Div(6, 2)).Should().Be(3);
        await Assert.ThrowsAsync<DivideByZeroException>(
            () => client.Div(1, 0));
        await AssertNoCalls(clientPeer, Out);

        divTracer.EnterCount.Should().Be(2);
        divTracer.ExitCount.Should().Be(2);
        divTracer.ErrorCount.Should().Be(1);
    }

    [Fact]
    public async Task TraceActivityTest()
    {
        await using var services = CreateServices();
        var clientPeer = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend).ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();

        var divMethod = services.RpcHub().ServiceRegistry[typeof(ITestRpcService)]["Div:2"];
        var divTracer = (RpcDefaultCallTracer)divMethod.Tracer!;
        divTracer.InboundDurationHistogram.Should().NotBeNull();

        (await client.Div(6, 2)).Should().Be(3);
        await Assert.ThrowsAsync<DivideByZeroException>(
            () => client.Div(1, 0));
        await AssertNoCalls(clientPeer, Out);
    }

    [Fact]
    public async Task InboundTraceIncludesResponseSerializationTest()
    {
        var stoppedActivities = new ConcurrentQueue<Activity>();
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
        meterListener.SetMeasurementEventCallback<double>((_, _, tags, _) =>
            measurements.Enqueue(tags.ToArray()));
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

        stoppedActivities.Should().BeEmpty();
        RpcSendHandlers.CompleteInboundCall(
            peer.ConnectionState.Value.Transport!, responseMessage, serializationError);

        var activity = stoppedActivities.Should().ContainSingle().Subject;
        activity.Status.Should().Be(ActivityStatusCode.Error);
        activity.GetTagItem("error.type").Should().Be(typeof(InvalidDataException).FullName);
        measurements.Should().ContainSingle();
        HasTag(measurements.Single(), "error.type", typeof(InvalidDataException).FullName!).Should().BeTrue();
    }

    [Fact]
    public async Task CallMetricsTest()
    {
        var measurements = new ConcurrentQueue<(
            string Name,
            double Value,
            KeyValuePair<string, object?>[] Tags)>();
        var publishedNames = new ConcurrentQueue<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) => {
            if (!ReferenceEquals(instrument.Meter, RpcInstruments.Meter))
                return;

            publishedNames.Enqueue(instrument.Name);
            if (instrument is Histogram<double>)
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Enqueue((instrument.Name, value, tags.ToArray())));
        listener.Start();

        await using var services = CreateServices();
        var clientPeer = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend).ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();
        var method = services.RpcHub().ServiceRegistry[typeof(ITestRpcService)]["Div:2"];

        (await client.Div(6, 2)).Should().Be(3);
        await Assert.ThrowsAsync<DivideByZeroException>(() => client.Div(1, 0));
        await AssertNoCalls(clientPeer, Out);

        RpcInstruments.InboundDurationHistogram.Name.Should().Be("rpc.server.call.duration");
        RpcInstruments.InboundDurationHistogram.Unit.Should().Be("ms");
        RpcInstruments.OutboundDurationHistogram.Name.Should().Be("rpc.client.call.duration");
        RpcInstruments.OutboundDurationHistogram.Unit.Should().Be("ms");
        measurements.Should().Contain(x =>
            x.Name == RpcInstruments.InboundDurationHistogram.Name
            && HasTag(x.Tags, "rpc.method", method.FullName)
            && HasTag(x.Tags, "rpc.system.name", "actuallab.rpc"));
        measurements.Should().Contain(x =>
            x.Name == RpcInstruments.OutboundDurationHistogram.Name
            && HasTag(x.Tags, "rpc.method", method.FullName)
            && HasTag(x.Tags, "rpc.system.name", "actuallab.rpc"));
        measurements.Should().Contain(x => HasTag(
            x.Tags, "error.type", typeof(DivideByZeroException).FullName!));
        measurements.Should().OnlyContain(x => x.Value >= 0);
        publishedNames.Should().NotContain(x => x.StartsWith("rpc.server.ITestRpcService", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RerouteMetricsTest()
    {
        var measurements = new ConcurrentQueue<KeyValuePair<string, object?>[]>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) => {
            if (ReferenceEquals(instrument, RpcInstruments.OutboundRerouteCounter))
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) => {
            value.Should().Be(1);
            measurements.Enqueue(tags.ToArray());
        });
        listener.Start();

        await using var services = CreateServices();
        var method = services.RpcHub().ServiceRegistry[typeof(ITestRpcService)]["Div:2"];

        RpcInstruments.RegisterReroute(method, RpcRoutingMode.Inbound);

        RpcInstruments.OutboundRerouteCounter.Name.Should().Be("rpc.client.reroute.count");
        RpcInstruments.OutboundRerouteCounter.Unit.Should().Be("{reroute}");
        measurements.Should().ContainSingle();
        var tags = measurements.Single();
        HasTag(tags, "rpc.method", method.FullName).Should().BeTrue();
        HasTag(tags, "rpc.method.kind", "query").Should().BeTrue();
        HasTag(tags, "rpc.routing.mode", "inbound").Should().BeTrue();
    }

    [Fact]
    public async Task TraceActivityPropagationWithoutOutboundActivityTest()
    {
        var stoppedActivities = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == RpcInstruments.ActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = stoppedActivities.Enqueue,
        };
        ActivitySource.AddActivityListener(listener);

        await using var services = CreateServices(s => {
            s.AddSingleton<RpcDiagnosticsOptions>(_ => RpcDiagnosticsOptions.Default with {
                CallTracerFactory = methodDef => new RpcDefaultCallTracer(
                    methodDef, mustTraceInbound: true, mustTraceOutbound: false),
            });
        });
        var clientPeer = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend).ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();
        var divMethod = services.RpcHub().ServiceRegistry[typeof(ITestRpcService)]["Div:2"];
        var divTracer = (RpcDefaultCallTracer)divMethod.Tracer!;
        using var parent = new Activity("parent").SetIdFormat(ActivityIdFormat.W3C).Start();
        var parentContext = parent.Context;

        (await client.Div(6, 2)).Should().Be(3);
        await AssertNoCalls(clientPeer, Out);
        await WaitFor(() => stoppedActivities.Any(a =>
            a.Kind == ActivityKind.Server && a.DisplayName == divTracer.InboundCallName));

        var serverActivity = stoppedActivities.Single(a =>
            a.Kind == ActivityKind.Server && a.DisplayName == divTracer.InboundCallName);
        serverActivity.TraceId.Should().Be(parentContext.TraceId);
        serverActivity.ParentSpanId.Should().Be(parentContext.SpanId);
        serverActivity.Links.Should().BeEmpty();
        serverActivity.GetTagItem("rpc.system.name").Should().Be("actuallab.rpc");
        serverActivity.GetTagItem("rpc.method").Should().Be(divMethod.FullName);
    }

    [Fact]
    public void TraceHeaderInjectionTest()
    {
        var activityContext = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded,
            "new-state");
        var originalHeaders = new[] {
            new RpcHeader(WellKnownRpcHeaders.W3CTraceParent, "old-parent-1"),
            new RpcHeader(WellKnownRpcHeaders.W3CTraceState, "old-state-1"),
            new RpcHeader("custom", "value"),
            new RpcHeader(WellKnownRpcHeaders.W3CTraceParent, "old-parent-2"),
            new RpcHeader(WellKnownRpcHeaders.W3CTraceState, "old-state-2"),
        };

        var headers = RpcActivityInjector.Inject(originalHeaders, activityContext);

        originalHeaders[0].Value.Should().Be("old-parent-1");
        headers.Count(h => h.Key == WellKnownRpcHeaders.W3CTraceParent).Should().Be(1);
        headers.Count(h => h.Key == WellKnownRpcHeaders.W3CTraceState).Should().Be(1);
        headers.TryGet(new RpcHeaderKey("custom")).Should().Be("value");
        RpcActivityInjector.TryExtract(headers, out var extractedContext).Should().BeTrue();
        extractedContext.TraceId.Should().Be(activityContext.TraceId);
        extractedContext.SpanId.Should().Be(activityContext.SpanId);
        extractedContext.TraceFlags.Should().Be(activityContext.TraceFlags);
        extractedContext.TraceState.Should().Be(activityContext.TraceState);
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
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, targetListener) => {
            if (ReferenceEquals(instrument, RpcInstruments.OutboundDurationHistogram))
                targetListener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<double>((_, value, _, _) => clientDurations.Enqueue(value));
        meterListener.Start();

        await using var services = CreateServices();
        var clientPeer = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend).ClientPeer;
        var method = services.RpcHub().ServiceRegistry[typeof(ITestRpcService)]["Div:2"];
        var tracer = (RpcDefaultCallTracer)method.Tracer!;
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

        var outboundActivities = stoppedActivities
            .Where(a => a.Kind == ActivityKind.Client && a.DisplayName == tracer.OutboundCallName)
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

    [Fact]
    public async Task TraceErrorActivityTest()
    {
        var stoppedActivities = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == RpcInstruments.ActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = stoppedActivities.Enqueue,
        };
        ActivitySource.AddActivityListener(listener);

        await using var services = CreateServices();
        var clientPeer = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend).ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();
        var method = services.RpcHub().ServiceRegistry[typeof(ITestRpcService)]["Div:2"];
        var tracer = (RpcDefaultCallTracer)method.Tracer!;

        await Assert.ThrowsAsync<DivideByZeroException>(() => client.Div(1, 0));
        await AssertNoCalls(clientPeer, Out);
        await WaitFor(() => stoppedActivities.Count(a =>
            a.DisplayName == tracer.InboundCallName || a.DisplayName == tracer.OutboundCallName) == 2);

        var activities = stoppedActivities
            .Where(a => a.DisplayName == tracer.InboundCallName || a.DisplayName == tracer.OutboundCallName)
            .ToArray();
        activities.Should().HaveCount(2);
        activities.Should().OnlyContain(a => a.Status == ActivityStatusCode.Error);
        activities.Should().OnlyContain(a => Equals(
            a.GetTagItem("error.type"), typeof(DivideByZeroException).FullName));
        activities.Should().OnlyContain(a => a.Events.Single().Name == "exception");
        activities.Should().OnlyContain(a => Equals(
            a.Events.Single().Tags.Single(t => t.Key == "exception.type").Value,
            typeof(DivideByZeroException).FullName));
        activities.Should().OnlyContain(a =>
            !a.Events.Single().Tags.Single(t => t.Key == "exception.message").Value!.ToString()!.IsNullOrEmpty());
        activities.Should().OnlyContain(a =>
            !a.Events.Single().Tags.Single(t => t.Key == "exception.stacktrace").Value!.ToString()!.IsNullOrEmpty());
    }

    [Theory]
    [InlineData("json5")]
    [InlineData("njson5")]
    [InlineData("json5np")]
    [InlineData("njson5np")]
    [InlineData("mempack5")]
    [InlineData("mempack5c")]
    [InlineData("msgpack5")]
    [InlineData("msgpack5c")]
    [InlineData("mempack6")]
    [InlineData("mempack6c")]
    [InlineData("msgpack6")]
    [InlineData("msgpack6c")]
#if NET8_0_OR_GREATER
    [InlineData("nmsgpack6")]
    [InlineData("nmsgpack6c")]
#endif
    public async Task BasicTest(string serializationFormat)
    {
        SerializationFormat = serializationFormat;
        await using var services = CreateServices();
        var clientPeer = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend).ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();

        (await client.Div(6, 2)).Should().Be(3);
        (await client.Div(6, 2)).Should().Be(3);
        (await client.Div(10, 2)).Should().Be(5);
        (await client.Div(null, 2)).Should().Be(null);
        await Assert.ThrowsAsync<DivideByZeroException>(
            () => client.Div(1, 0));
        await AssertNoCalls(clientPeer, Out);
    }

    [Fact]
    public async Task RpcMethodAttributeTest()
    {
        await using var services = CreateServices();
        var clientPeer = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend).ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();

        var method = services.RpcHub().ServiceRegistry[typeof(ITestRpcService)]["RenamedMethod"];
        method.OutboundCallTimeouts.RunTimeout.Should().Be(TimeSpan.FromSeconds(0.5));
        method.LocalExecutionMode.Should().Be(RpcLocalExecutionMode.Unconstrained);

        (await client.AddWithAttribute(3, 4)).Should().Be(7);
        await AssertNoCalls(clientPeer, Out);
    }

    [Fact]
    public async Task CommandTest()
    {
        await using var services = CreateServices();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        var clientPeer = connection.ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();

        (await client.OnHello(new HelloCommand("X"))).Should().Be("Hello, X!");
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.OnHello(new HelloCommand("error")));
#if NET6_0_OR_GREATER
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => client.OnHello(null!)); // That's due to RpcCallValidator
#endif

        await connection.Disconnect();
        // NOTE: It won't throw under the debugger due to debug mode timeouts
        await Assert.ThrowsAsync<TimeoutException>(
            // The default connect timeout is 1.5s
            () => client.OnHello(new HelloCommand("X", TimeSpan.FromSeconds(3))));
        await Delay(0.1);
        await AssertNoCalls(clientPeer, Out);

        await connection.Connect();
        // NOTE: It won't throw under the debugger due to debug mode timeouts
        await Assert.ThrowsAsync<TimeoutException>(
            // The default run timeout is 10s, but checks are every 10s or so
            () => client.OnHello(new HelloCommand("X", TimeSpan.FromSeconds(30))));

        await AssertNoCalls(clientPeer, Out);
    }

    [Fact]
    public async Task NoWaitTest()
    {
        await using var services = CreateServices();
        var clientPeer = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend).ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();

        // We need to make sure the connection is there before the next call
        await client.Add(1, 1);

        await client.MaybeSet("a", "b");
        await TestExt.When(async () => {
            var result = await client.Get("a");
            result.Should().Be("b");
        }, TimeSpan.FromSeconds(1));

        await client.MaybeSet("a", "c");
        await TestExt.When(async () => {
            var result = await client.Get("a");
            result.Should().Be("c");
        }, TimeSpan.FromSeconds(1));

        await AssertNoCalls(clientPeer, Out);
    }

    [Fact]
    public async Task DelayTest()
    {
        await using var services = CreateServices();
        var clientPeer = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend).ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();
        await client.Add(1, 1); // Warm-up

        var startedAt = CpuTimestamp.Now;
        await client.Delay(TimeSpan.FromMilliseconds(200));
        startedAt.Elapsed.TotalMilliseconds.Should().BeInRange(100, 500);
        await AssertNoCalls(clientPeer, Out);

        {
            using var cts = new CancellationTokenSource(1);
            startedAt = CpuTimestamp.Now;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => client.Delay(TimeSpan.FromHours(1), cts.Token));
            startedAt.Elapsed.TotalMilliseconds.Should().BeInRange(0, 500);
            await AssertNoCalls(clientPeer, Out);
        }

        {
            using var cts = new CancellationTokenSource(500);
            startedAt = CpuTimestamp.Now;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => client.Delay(TimeSpan.FromHours(1), cts.Token));
            startedAt.Elapsed.TotalMilliseconds.Should().BeInRange(300, 1000);
            await AssertNoCalls(clientPeer, Out);
        }
    }

    [Fact]
    public void RpcSerializableAttributeTest()
    {
        // NonPolymorphicBase is abstract, so it would normally be polymorphic.
        // But [RpcSerializable] overrides that.
        RpcArgumentSerializer.IsPolymorphic(typeof(NonPolymorphicBase)).Should().BeFalse();
        RpcArgumentSerializer.IsPolymorphic(typeof(NonPolymorphicDerived)).Should().BeFalse();

        // Types without [RpcSerializable] still follow the default rules
        RpcArgumentSerializer.IsPolymorphic(typeof(ITuple)).Should().BeTrue();
        RpcArgumentSerializer.IsPolymorphic(typeof(object)).Should().BeTrue();
        RpcArgumentSerializer.IsPolymorphic(typeof(string)).Should().BeFalse();
        RpcArgumentSerializer.IsPolymorphic(typeof(int)).Should().BeFalse();
    }

    [Theory]
    [InlineData("json5")]
    [InlineData("json5np")]
    [InlineData("mempack6")]
    [InlineData("msgpack6")]
    public async Task NonPolymorphTest(string serializationFormat)
    {
        SerializationFormat = serializationFormat;
        await using var services = CreateServices();
        var clientPeer = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend).ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();

        var methodDef = services.RpcHub().ServiceRegistry[typeof(ITestRpcService)]["NonPolymorphRoundtrip:2"];
        methodDef.HasPolymorphicArguments.Should().BeFalse();
        methodDef.HasPolymorphicResult.Should().BeFalse();

        var item1 = new NonPolymorphicDerived { Value = 42, Tag = "test" };
        var result1 = await client.NonPolymorphRoundtrip(item1);
        result1.Should().BeOfType<NonPolymorphicDerived>();
        result1.Value.Should().Be(42);
        ((NonPolymorphicDerived)result1).Tag.Should().Be("test");

        var item2 = new NonPolymorphicDerived2 { Value = 7, Score = 3.14 };
        var result2 = await client.NonPolymorphRoundtrip(item2);
        result2.Should().BeOfType<NonPolymorphicDerived2>();
        result2.Value.Should().Be(7);
        ((NonPolymorphicDerived2)result2).Score.Should().Be(3.14);

        await AssertNoCalls(clientPeer, Out);
    }

    [Theory]
    [InlineData("json5")]
    [InlineData("json5np")]
    [InlineData("mempack6")]
    [InlineData("msgpack6")]
    public async Task NonPolymorphStreamTest(string serializationFormat)
    {
        SerializationFormat = serializationFormat;
        await using var services = CreateServices();
        var clientPeer = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend).ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();

        var count = 10;
        var stream = await client.StreamNonPolymorph(count);
        var items = await stream.ToListAsync();
        items.Should().HaveCount(count);
        for (var i = 0; i < count; i++) {
            items[i].Value.Should().Be(i);
            if (i % 2 == 0) {
                items[i].Should().BeOfType<NonPolymorphicDerived>();
                ((NonPolymorphicDerived)items[i]).Tag.Should().Be($"tag-{i}");
            }
            else {
                items[i].Should().BeOfType<NonPolymorphicDerived2>();
                ((NonPolymorphicDerived2)items[i]).Score.Should().Be(i * 0.5);
            }
        }

        await AssertNoCalls(clientPeer, Out);
    }

    [Theory]
    [InlineData("json5")]
    [InlineData("njson5")]
    [InlineData("mempack5")]
    [InlineData("mempack5c")]
    [InlineData("msgpack5")]
    [InlineData("msgpack5c")]
    [InlineData("mempack6")]
    [InlineData("mempack6c")]
    [InlineData("msgpack6")]
    [InlineData("msgpack6c")]
#if NET8_0_OR_GREATER
    [InlineData("nmsgpack6")]
    [InlineData("nmsgpack6c")]
#endif
    public async Task PolymorphTest(string serializationFormat)
    {
        SerializationFormat = serializationFormat;
        await using var services = CreateServices();
        var clientPeer = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend).ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();
        var backendClient = services.RpcHub().GetClient<ITestRpcBackend>();

        await backendClient.Polymorph(null!);

        var t = new Tuple<int>(1);
        (await backendClient.Polymorph(t)).Should().Be(t);

        (await client.PolymorphArg(new Tuple<int>(1))).Should().Be(1);
        (await client.PolymorphResult(2)).Should().Be(new Tuple<int>(2));

        await AssertNoCalls(clientPeer, Out);
    }

    [Theory]
    [InlineData("json5np")]
    [InlineData("njson5np")]
    public async Task PolymorphTest_NP_ShouldFail(string serializationFormat)
    {
        SerializationFormat = serializationFormat;
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<ITestRpcService>();
        var backendClient = services.RpcHub().GetClient<ITestRpcBackend>();

        // Client-side: polymorphic arguments cause SerializationException during Send
        await Assert.ThrowsAsync<SerializationException>(
            () => backendClient.Polymorph(new Tuple<int>(1)));
        await Assert.ThrowsAsync<SerializationException>(
            () => client.PolymorphArg(new Tuple<int>(1)));

        // Server-side: polymorphic result serialization also fails,
        // but the error surfaces differently to the client
        await Assert.ThrowsAnyAsync<Exception>(
            () => client.PolymorphResult(2));
    }

    [Fact]
    public async Task CancellationTest()
    {
        await using var services = CreateServices();
        var clientPeer = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend).ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();

        // This test may fail due to other tests, so we retry it for up to 5s
        await TestExt.When(async () => {
            var baseCancellationCount = await client.GetCancellationCount();
            var cts = new CancellationTokenSource(100);
            var result = await client.Delay(TimeSpan.FromMilliseconds(300), cts.Token).ResultAwait();
            result.Error.Should().BeAssignableTo<OperationCanceledException>();
            var cancellationCount = await client.GetCancellationCount();
            (cancellationCount - baseCancellationCount).Should().Be(1);
        }, TimeSpan.FromSeconds(5));

        await AssertNoCalls(clientPeer, Out);
    }

    [Theory]
    [InlineData("json5")]
    [InlineData("njson5")]
    [InlineData("json5np")]
    [InlineData("njson5np")]
    [InlineData("mempack5")]
    [InlineData("mempack5c")]
    [InlineData("msgpack5")]
    [InlineData("msgpack5c")]
    [InlineData("mempack6")]
    [InlineData("mempack6c")]
    [InlineData("msgpack6")]
    [InlineData("msgpack6c")]
#if NET8_0_OR_GREATER
    [InlineData("nmsgpack6")]
    [InlineData("nmsgpack6c")]
#endif
    public async Task StreamTest(string serializationFormat)
    {
        SerializationFormat = serializationFormat;
        await using var services = CreateServices();
        var clientPeer = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend).ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();

        var expected1 = Enumerable.Range(0, 5).ToList();
        var stream1 = await client.StreamInt32(expected1.Count);
        (await stream1.ToListAsync()).Should().Equal(expected1);
        await AssertNoObjects(clientPeer);

        if (serializationFormat.EndsWith("np"))
            return; // Polymorphic streams are going to "hang" w/ non-polymorphic serializer

        var expected2 = Enumerable.Range(0, 5)
            .Select(x => (x & 2) == 0 ? (ITuple)new Tuple<int>(x) : new Tuple<long>(x))
            .ToList();
        var stream2 = await client.StreamTuples(expected2.Count);
        (await stream2.ToListAsync()).Should().Equal(expected2);
        await AssertNoObjects(clientPeer);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(10_000)]
    [InlineData(50_000)]
    [InlineData(200_000)]
    [InlineData(1000_000)]
    public async Task PerformanceTest(int iterationCount)
    {
        if (TestRunnerInfo.IsBuildAgent())
            iterationCount = 100;

        UseLogging = false;
        await using var services = CreateServices();
        var clientPeer = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend).ClientPeer;
        var client = services.RpcHub().GetClient<ITestRpcService>();
        await client.Div(1, 1);
        await AssertNoCalls(clientPeer, Out);

        var startedAt = CpuTimestamp.Now;
        for (var i = iterationCount; i > 0; i--)
            if (i != await client.Add(i, 0).ConfigureAwait(false))
                Assert.Fail("Wrong result.");
        var elapsed = startedAt.Elapsed;
        WriteLine($"{iterationCount}: {iterationCount / elapsed.TotalSeconds:F} ops/s");
        await AssertNoCalls(clientPeer, Out);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(10_000)]
    [InlineData(50_000)]
    [InlineData(200_000)]
    public async Task StreamPerformanceTest(int itemCount)
    {
        if (TestRunnerInfo.IsBuildAgent())
            itemCount = 100;

        UseLogging = false;
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<ITestRpcService>();
        var stream = await client.StreamInt32(200);
        await stream.CountAsync();

        stream = await client.StreamInt32(itemCount);
        var startedAt = CpuTimestamp.Now;
        (await stream.CountAsync()).Should().Be(itemCount);
        var elapsed = startedAt.Elapsed;
        WriteLine($"{itemCount}: {itemCount / elapsed.TotalSeconds:F} ops/s");
    }

    // Private methods

    private static bool HasTag(
        IEnumerable<KeyValuePair<string, object?>> tags,
        string name,
        object value)
        => tags.Any(x => x.Key == name && Equals(x.Value, value));

    private static async Task WaitFor(Func<bool> condition)
    {
        for (var i = 0; i < 200; i++) {
            if (condition.Invoke())
                return;

            await Task.Delay(10);
        }
        Assert.Fail("The expected RPC trace wasn't completed.");
    }
}
