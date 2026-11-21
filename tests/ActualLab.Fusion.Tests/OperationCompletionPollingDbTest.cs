using ActualLab.Fusion.EntityFramework.Operations.LogProcessing;
using ActualLab.Fusion.Tests.DbModel;

namespace ActualLab.Fusion.Tests;

// Runs the ICompletion test with no operation log watcher, so the other host learns about the
// operation only by polling - the same as when a change notification is lost. With a zero
// StartOffset, a start entry lookup bound sliding with "now" would never find the entry,
// so the test fails deterministically unless the bound is fixed at the reader's first lookup.
public class OperationCompletionPollingDbTest : OperationCompletionDbTest
{
    // Lets the other host's reader make its first start entry lookup before the command runs
    protected override double DelayBeforeCommand => 1;

    public OperationCompletionPollingDbTest(ITestOutputHelper @out) : base(@out)
        => UseOperationLogChangeTracking = false;

    protected override void ConfigureTestServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureTestServices(services, isClient);
        if (isClient)
            return;

        services.AddSingleton(_ => new DbOperationLogReader<TestDbContext>.Options {
            CheckPeriod = TimeSpan.FromSeconds(2),
            StartOffset = TimeSpan.Zero,
        });
    }
}
