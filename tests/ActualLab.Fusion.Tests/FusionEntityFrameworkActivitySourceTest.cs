using ActualLab.Fusion.EntityFramework.Internal;
using ActualLab.Fusion.EntityFramework.Operations.LogProcessing;
using ActualLab.Fusion.Tests.DbModel;

namespace ActualLab.Fusion.Tests;

public sealed class FusionEntityFrameworkActivitySourceTest(ITestOutputHelper @out) : FusionTestBase(@out)
{
    [Fact]
    public async Task OperationLogReaderActivityUsesEntityFrameworkSource()
    {
        var activitySource = FusionEntityFrameworkInstruments.ActivitySource;
        var startedActivities = new ConcurrentQueue<Activity>();
        var stoppedActivities = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener {
            ShouldListenTo = source => source == activitySource,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = startedActivities.Enqueue,
            ActivityStopped = stoppedActivities.Enqueue,
        };
        ActivitySource.AddActivityListener(listener);
        activitySource.HasListeners().Should().BeTrue();

        var reader = new GapTestLogReader(
            new DbOperationLogReader<TestDbContext>.Options { IsTracingEnabled = true },
            Services);
        reader.Settings.IsTracingEnabled.Should().BeTrue();
        await reader.RunProcessBatch(batchSize: 1);

        startedActivities.Should().ContainSingle();
        var activity = stoppedActivities.Should().ContainSingle().Subject;
        activity.Source.Should().BeSameAs(activitySource);
        activity.OperationName.Should().Contain(nameof(GapTestLogReader));
    }
}
