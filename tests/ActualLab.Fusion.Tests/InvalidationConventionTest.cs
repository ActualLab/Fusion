using ActualLab.Fusion.Tests.Services;

namespace ActualLab.Fusion.Tests;

// Copyable template for the per-command invalidation test convention from
// docs/PartO.md ("Testing Invalidation"): for every mutating command, capture both the
// entity-specific query and the aggregate query it affects *before* running the command,
// then assert both got invalidated afterward.
public class InvalidationConventionTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    [Fact]
    public async Task Set_InvalidatesEntityAndAggregateQuery()
    {
        var services = CreateServicesWithComputeService<InvalidationConventionService>();
        var kv = services.GetRequiredService<InvalidationConventionService>();
        var commander = services.Commander();

        var cGet = await Computed.Capture(() => kv.Get("a"));
        var cCount = await Computed.Capture(() => kv.Count());
        cGet.IsConsistent().Should().BeTrue();
        cCount.IsConsistent().Should().BeTrue();

        await commander.Call(new InvalidationConventionService_Set("a", "1"));

        cGet.IsConsistent().Should().BeFalse();
        cCount.IsConsistent().Should().BeFalse();
        (await kv.Get("a")).Should().Be("1");
        (await kv.Count()).Should().Be(1);
    }

    [Fact]
    public async Task Remove_InvalidatesEntityAndAggregateQuery()
    {
        var services = CreateServicesWithComputeService<InvalidationConventionService>();
        var kv = services.GetRequiredService<InvalidationConventionService>();
        var commander = services.Commander();
        await commander.Call(new InvalidationConventionService_Set("a", "1"));

        var cGet = await Computed.Capture(() => kv.Get("a"));
        var cCount = await Computed.Capture(() => kv.Count());
        cGet.IsConsistent().Should().BeTrue();
        cCount.IsConsistent().Should().BeTrue();

        await commander.Call(new InvalidationConventionService_Remove("a"));

        cGet.IsConsistent().Should().BeFalse();
        cCount.IsConsistent().Should().BeFalse();
        (await kv.Get("a")).Should().BeNull();
        (await kv.Count()).Should().Be(0);
    }

    // Negative counterpart: proves the convention above isn't a rubber stamp -- if a handler's
    // invalidation branch omits an affected aggregate query, the "both got invalidated" assertion
    // is exactly what catches it. BrokenInvalidationConventionService.Set invalidates Get but not
    // Count, so a real per-command test asserting cCount.IsConsistent().Should().BeFalse() would
    // fail right here, pointing straight at the missing "_ = Count(default)" line.
    [Fact]
    public async Task Set_WithMissingAggregateInvalidation_LeavesAggregateQueryStale()
    {
        var services = CreateServicesWithComputeService<BrokenInvalidationConventionService>();
        var kv = services.GetRequiredService<BrokenInvalidationConventionService>();
        var commander = services.Commander();

        var cGet = await Computed.Capture(() => kv.Get("a"));
        var cCount = await Computed.Capture(() => kv.Count());

        await commander.Call(new InvalidationConventionService_Set("a", "1"));

        cGet.IsConsistent().Should().BeFalse();
        cCount.IsConsistent().Should().BeTrue(); // Stale: the aggregate invalidation was forgotten
        (await kv.Count()).Should().Be(0); // Still 0, even though Get("a") already returns "1"
    }
}
