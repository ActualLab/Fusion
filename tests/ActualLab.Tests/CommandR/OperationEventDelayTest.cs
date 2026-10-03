using ActualLab.CommandR.Operations;
using ActualLab.Versioning;

namespace ActualLab.Tests.CommandR;

// A quantized delay serves two purposes at once: it batches work onto a coarse schedule, and the
// quantized instant goes into the Uuid so that repeated producers of the same logical event
// deduplicate. The offset has to spread the first without breaking the second.
public class OperationEventDelayTest(ITestOutputHelper @out) : TestBase(@out)
{
    private static readonly Moment LoggedAt = new(DateTime.Parse("2026-10-03T12:00:00Z").ToUniversalTime());
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    [Fact]
    public void SamePrefixAndCellDeduplicate()
    {
        // Two hosts, two moments inside one cell of this prefix's lattice, one logical event.
        // The cell is derived rather than guessed: where its boundaries fall depends on the prefix.
        const string prefix = "nightly-report";
        var cell = LoggedAt.Ceiling(Hour, TimeSpanExt.GetHashBasedOffset(prefix, Hour));

        var a = NewEvent().SetDelayUntil(cell + TimeSpan.FromMinutes(1), Hour, prefix);
        var b = NewEvent().SetDelayUntil(cell + TimeSpan.FromMinutes(59), Hour, prefix);

        a.DelayUntil.Should().Be(cell + Hour);
        b.DelayUntil.Should().Be(cell + Hour);
        a.Uuid.Should().Be(b.Uuid);
        a.UuidConflictStrategy.Should().Be(KeyConflictStrategy.Skip);
    }

    [Fact]
    public void InstantsStraddlingALatticePointDoNot()
    {
        // Inherent to quantizing, and true of the bare lattice too - it's just that the offset moves
        // where the seam falls, so "scheduled within the same hour" is no longer the same thing as
        // "lands on the same instant". Producers that must deduplicate have to agree on the target
        // instant, not merely on the hour.
        const string prefix = "nightly-report";
        var cell = LoggedAt.Ceiling(Hour, TimeSpanExt.GetHashBasedOffset(prefix, Hour));

        var before = NewEvent().SetDelayUntil(cell - TimeSpan.FromMinutes(1), Hour, prefix);
        var after = NewEvent().SetDelayUntil(cell + TimeSpan.FromMinutes(1), Hour, prefix);

        before.DelayUntil.Should().Be(cell);
        after.DelayUntil.Should().Be(cell + Hour);
        before.Uuid.Should().NotBe(after.Uuid);
    }

    [Fact]
    public void DifferentPrefixesLandAtDifferentOffsets()
    {
        var delayUntil = LoggedAt + TimeSpan.FromMinutes(3);
        var instants = Enumerable.Range(0, 50)
            .Select(i => NewEvent().SetDelayUntil(delayUntil, Hour, $"tenant-{i}").DelayUntil)
            .ToList();

        // The storm this exists to prevent: with a bare lattice all 50 would be one instant
        instants.Distinct().Should().HaveCountGreaterThan(45);
    }

    [Fact]
    public void TheInstantSitsOnThePrefixesOwnLattice()
    {
        var prefix = "nightly-report";
        var e = NewEvent().SetDelayUntil(LoggedAt + TimeSpan.FromMinutes(3), Hour, prefix);

        var offset = TimeSpanExt.GetHashBasedOffset(prefix, Hour);
        e.DelayUntil.Should().Be((LoggedAt + TimeSpan.FromMinutes(3)).Ceiling(Hour, offset));
        // And the Uuid carries that instant, which is what makes it the dedup key
        e.Uuid.Should().Be($"{prefix}-at-{e.DelayUntil.EpochOffsetTicks:x}");
    }

    [Fact]
    public void AnExplicitZeroOffsetRestoresTheBareLattice()
    {
        var e = NewEvent().SetDelayUntil(
            LoggedAt + TimeSpan.FromMinutes(3), Hour, TimeSpan.Zero, "nightly-report");

        e.DelayUntil.Should().Be(LoggedAt + Hour); // 13:00:00 exactly
    }

    [Fact]
    public void AnExplicitOffsetWins()
    {
        var offset = TimeSpan.FromMinutes(7);
        var e = NewEvent().SetDelayUntil(
            LoggedAt + TimeSpan.FromMinutes(3), Hour, offset, "nightly-report");

        e.DelayUntil.Should().Be(LoggedAt + offset); // 12:07:00, the next lattice point
    }

    [Fact]
    public void SetDelayByAgreesWithSetDelayUntil()
    {
        var byEvent = NewEvent().SetDelayBy(TimeSpan.FromMinutes(3), Hour, "nightly-report");
        var untilEvent = NewEvent().SetDelayUntil(LoggedAt + TimeSpan.FromMinutes(3), Hour, "nightly-report");

        byEvent.DelayUntil.Should().Be(untilEvent.DelayUntil);
        byEvent.Uuid.Should().Be(untilEvent.Uuid);
    }

    [Fact]
    public void WithoutAPrefixTheEventsOwnUuidKeysTheOffset()
    {
        // Each event has a fresh ULID, so each gets its own offset - and deduplicates with nothing,
        // exactly as before: a dedup key that includes a random uuid never matches another event
        var a = NewEvent().SetDelayUntil(LoggedAt + TimeSpan.FromMinutes(3), Hour);
        var b = NewEvent().SetDelayUntil(LoggedAt + TimeSpan.FromMinutes(3), Hour);

        a.Uuid.Should().NotBe(b.Uuid);
    }

    [Fact]
    public void AZeroQuantaMeansNoQuanta()
    {
        // A degenerate but legal setting: no quantization, so the instant passes through. The Uuid
        // then carries that exact instant and matches nothing else, which is what "no quanta" means
        // for a dedup key - the same as the SetDelayUntil(Moment) overload, reached by configuration
        // rather than by picking a different method.
        var delayUntil = LoggedAt + TimeSpan.FromMinutes(3);
        var e = NewEvent().SetDelayUntil(delayUntil, TimeSpan.Zero, "nightly-report");

        e.DelayUntil.Should().Be(delayUntil);
        e.Uuid.Should().Be($"nightly-report-at-{delayUntil.EpochOffsetTicks:x}");
    }

    [Fact]
    public void ANegativeQuantaThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NewEvent().SetDelayUntil(LoggedAt, TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NewEvent().SetDelayUntil(LoggedAt, TimeSpan.FromTicks(-1), TimeSpan.Zero));
    }

    // Private methods

    private static OperationEvent NewEvent()
        => new("value") { LoggedAt = LoggedAt };
}
