using System.Diagnostics;
using System.Diagnostics.Metrics;
using ActualLab.Fusion.Diagnostics;
using ActualLab.Fusion.Operations.Internal;
using ActualLab.Interception;
using ActualLab.Reflection;
using ActualLab.Serialization;
using ActualLab.CommandR.Operations;

namespace ActualLab.Fusion.Tests.Internal;

public sealed class InvalidationDiagnosticsTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    [Fact]
    public async Task PassMetricsTest()
    {
        // Read before the listener is installed: FusionInstruments' static ctor creates the
        // ActivitySource, which notifies every listener already present - and ShouldListenTo would
        // then read the static field that same ctor hasn't assigned yet
        var activitySourceName = FusionInstruments.ActivitySource.Name;
        var activities = new ConcurrentQueue<Activity>();
        using var activityListener = new ActivityListener {
            ShouldListenTo = source => source.Name == activitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => {
                if (activity.OperationName.StartsWith("inv.pass.", StringComparison.Ordinal))
                    activities.Enqueue(activity);
            },
        };
        ActivitySource.AddActivityListener(activityListener);

        var measurements = new ConcurrentQueue<Measurement>();
        var duration = FusionInstruments.InvalidationPassDuration;
        var callCount = FusionInstruments.InvalidationPassCallCount;
        duration.Name.Should().Be("invalidation.pass.duration");
        duration.Unit.Should().Be("ms");
        callCount.Name.Should().Be("invalidation.pass.call.count");
        callCount.Unit.Should().Be("{call}");
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) => {
            if (ReferenceEquals(instrument, duration) || ReferenceEquals(instrument, callCount))
                listener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Enqueue(NewMeasurement(instrument.Name, value, tags)));
        meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Enqueue(NewMeasurement(instrument.Name, value, tags)));
        meterListener.Start();

        using var services = CreateDiagnosticServices();
        var handler = services.GetRequiredService<FusionOperationCompletionHandler>();
        var calls = ImmutableList.Create(NewInvocation("Get"));

        await handler.ApplyLocalInvalidations(calls);
        services.GetRequiredService<InvalidationFailureInjector>().MustFail = true;
        await handler.ApplyLocalInvalidations(calls);

        // Both passes now carry the same generated source, so the failure tag is what tells
        // them apart
        activities.Should().HaveCount(2);
        var failedActivity = activities.Single(x => Equals(x.GetTagItem("invalidation.partial_failure"), true));
        failedActivity.GetTagItem("invalidation.partial_failure").Should().Be(true);
        failedActivity.GetTagItem("invalidation.failure.count").Should().Be(1);

        var durations = measurements.Where(x => x.InstrumentName == duration.Name).ToArray();
        durations.Should().HaveCount(2);
        durations.Should().OnlyContain(x => x.Value >= 0);
        durations.Select(x => x.Outcome).Should().BeEquivalentTo("success", "error");
        var counts = measurements.Where(x => x.InstrumentName == callCount.Name).ToArray();
        counts.Should().HaveCount(2);
        counts.Should().OnlyContain(x => x.Value == 1);
        counts.Select(x => x.Outcome).Should().BeEquivalentTo("success", "error");
        measurements.Should().OnlyContain(x => x.Kind == "local" && x.TagCount == 2);
    }

    [Fact]
    public async Task DropMetricTest()
    {
        using var services = CreateDiagnosticServices();
        var handler = services.GetRequiredService<FusionOperationCompletionHandler>();
        // Read before the listener is installed - see PassMetricsTest. Here the re-entrancy is
        // silent rather than fatal: the field is still null while its own instrument is published,
        // so the listener never enables it and the test sees no measurements at all.
        var dropCount = FusionInstruments.DeferredInvalidationDropCount;
        var drops = 0L;
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) => {
            if (ReferenceEquals(instrument, dropCount))
                listener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<long>((_, value, _, _) => Interlocked.Add(ref drops, value));
        meterListener.Start();

        // An unresolvable method can't be applied, and a local pass has no one to throw to
        await handler.ApplyLocalInvalidations(ImmutableList.Create(NewInvocation("NoSuchMethod")));

        Volatile.Read(ref drops).Should().Be(1);
    }

    // Private methods

    private ServiceProvider CreateDiagnosticServices()
        => CreateServices(services => {
            services.AddSingleton<InvalidationFailureInjector>();
            services.AddFusion().AddService<InvalidationDiagnosticsService>();
            services.AddSingleton<FusionOperationCompletionHandler>(
                c => new InjectedFailureHandler(c));
        });

    private static ServiceCall NewInvocation(string methodName)
        => new() {
            ServiceType = new TypeRef(typeof(InvalidationDiagnosticsService)).WithoutAssemblyVersions(),
            MethodName = $"{methodName}:1",
            Arguments = ArgumentList.New(default(CancellationToken)),
        };

    private static Measurement NewMeasurement(
        string instrumentName,
        double value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
        => new(
            instrumentName,
            value,
            GetTag(tags, "invalidation.kind"),
            GetTag(tags, "outcome"),
            tags.Length);

    private static string GetTag(ReadOnlySpan<KeyValuePair<string, object?>> tags, string name)
    {
        foreach (var tag in tags)
            if (tag.Key == name)
                return (string)tag.Value!;

        return "";
    }

    // Nested types

    public sealed class InvalidationFailureInjector
    {
        public bool MustFail { get; set; }
    }

    public class InvalidationDiagnosticsService : IComputeService
    {
        [ComputeMethod]
        public virtual Task<int> Get(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class InjectedFailureHandler(IServiceProvider services)
        : FusionOperationCompletionHandler(services)
    {
        private InvalidationFailureInjector Injector { get; }
            = services.GetRequiredService<InvalidationFailureInjector>();

        protected override Task ApplyInvalidation(
            ServiceCall call, bool mustResolve = false, CancellationToken cancellationToken = default)
            => Injector.MustFail
                ? Task.FromException(new InvalidOperationException("Injected invalidation failure."))
                : base.ApplyInvalidation(call, mustResolve, cancellationToken);
    }

    private sealed record Measurement(
        string InstrumentName,
        double Value,
        string Kind,
        string Outcome,
        int TagCount);
}
