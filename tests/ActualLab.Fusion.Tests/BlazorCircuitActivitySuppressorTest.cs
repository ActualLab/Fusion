using System.Diagnostics;
using ActualLab.Fusion.Server.Internal;

namespace ActualLab.Fusion.Tests;

public sealed class BlazorCircuitActivitySuppressorTest
{
    [Fact]
    public async Task SuppressesConnectionScopedActivitiesOnly()
    {
        var suppressor = new BlazorCircuitActivitySuppressor();
        using var hostingSource = new ActivitySource("Microsoft.AspNetCore");
        using var appSource = new ActivitySource("MyApp");
        using var listener = new ActivityListener {
            ShouldListenTo = s => s.Name is "Microsoft.AspNetCore" or "MyApp",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        using (var legacy = new Activity("Microsoft.AspNetCore.Hosting.HttpRequestIn").Start()) {
            await suppressor.OnCircuitOpenedAsync(null!, default);
            Activity.Current.Should().BeNull();
        }
        Activity.Current = null;

        using (var hosting = hostingSource.StartActivity("HttpRequestIn")) {
            hosting.Should().NotBeNull();
            await suppressor.OnConnectionUpAsync(null!, default);
            Activity.Current.Should().BeNull();
        }
        Activity.Current = null;

        using (var deliberate = appSource.StartActivity("op")) {
            deliberate.Should().NotBeNull();
            await suppressor.OnCircuitOpenedAsync(null!, default);
            Activity.Current.Should().BeSameAs(deliberate);
        }
    }
}
