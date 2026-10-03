namespace ActualLab.Tests.Time;

// Quantizing a delay to a bare lattice pins every producer to the same instant, which turns an
// hourly schedule into an hourly storm. An offset keyed off the event's uuid prefix spreads them
// without breaking what the quantization is for: the uuid is the deduplication key, so everything
// derived from one prefix still has to land on one instant.
public class HashBasedOffsetTest(ITestOutputHelper @out) : TestBase(@out)
{
    private static readonly Moment Base = new(DateTime.Parse("2026-10-03T12:34:56.789Z").ToUniversalTime());

    [Fact]
    public void OffsetIsWithinTheUnit()
    {
        var unit = TimeSpan.FromHours(1);
        foreach (var source in new[] { "a", "user-1", "user-2", "", "a-much-longer-prefix-here" }) {
            var offset = TimeSpanExt.GetHashBasedOffset(source, unit);
            offset.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
            offset.Should().BeLessThan(unit);
        }
    }

    [Fact]
    public void OffsetIsStableForTheSameSource()
    {
        var unit = TimeSpan.FromHours(1);
        // Must hold across hosts and processes, which is why it can't be string.GetHashCode()
        TimeSpanExt.GetHashBasedOffset("order-42", unit)
            .Should().Be(TimeSpanExt.GetHashBasedOffset("order-42", unit));
    }

    [Fact]
    public void OffsetSpreadsNearlyIdenticalSources()
    {
        var unit = TimeSpan.FromHours(1);
        // The case that matters: keys differing in one character are what a real app produces
        var offsets = Enumerable.Range(0, 200)
            .Select(i => TimeSpanExt.GetHashBasedOffset($"user-{i}", unit))
            .ToList();

        offsets.Distinct().Should().HaveCountGreaterThan(190);
        // Spread over the whole hour, not clustered in one corner of it
        offsets.Select(x => (int)(x.TotalMinutes / 10)).Distinct().Should().HaveCount(6);
    }

    [Fact]
    public void OffsetIsZeroWhenThereIsNoLatticeOrNoSource()
    {
        TimeSpanExt.GetHashBasedOffset("a", TimeSpan.Zero).Should().Be(TimeSpan.Zero);
        TimeSpanExt.GetHashBasedOffset("a", TimeSpan.FromTicks(-1)).Should().Be(TimeSpan.Zero);
        TimeSpanExt.GetHashBasedOffset(null, TimeSpan.FromHours(1)).Should().Be(TimeSpan.Zero);
        TimeSpanExt.GetHashBasedOffset("", TimeSpan.FromHours(1)).Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void QuantizingWithoutAnOffsetIsUnchanged()
    {
        var unit = TimeSpan.FromHours(1);
        // Every existing call site passes no offset, so these have to be what they always were
        Base.Floor(unit).Should().Be(new Moment(DateTime.Parse("2026-10-03T12:00:00Z").ToUniversalTime()));
        Base.Ceiling(unit).Should().Be(new Moment(DateTime.Parse("2026-10-03T13:00:00Z").ToUniversalTime()));
    }

    [Fact]
    public void QuantizingShiftsTheWholeLattice()
    {
        var unit = TimeSpan.FromHours(1);
        var offset = TimeSpan.FromMinutes(20);

        Base.Floor(unit, offset).Should()
            .Be(new Moment(DateTime.Parse("2026-10-03T12:20:00Z").ToUniversalTime()));
        Base.Ceiling(unit, offset).Should()
            .Be(new Moment(DateTime.Parse("2026-10-03T13:20:00Z").ToUniversalTime()));
    }

    [Fact]
    public void QuantizingIsIdempotentOnTheLattice()
    {
        var unit = TimeSpan.FromHours(1);
        var offset = TimeSpan.FromMinutes(37);

        // A value already on the lattice must not move - otherwise the uuid derived from it would
        // differ between a first write and a retry
        var onLattice = Base.Ceiling(unit, offset);
        onLattice.Ceiling(unit, offset).Should().Be(onLattice);
        onLattice.Floor(unit, offset).Should().Be(onLattice);
    }

    [Fact]
    public void AnOffsetOutsideTheUnitFoldsBack()
    {
        var unit = TimeSpan.FromHours(1);
        // No normalization needed anywhere, because PositiveModulo already does it
        Base.Ceiling(unit, TimeSpan.FromMinutes(20)).Should()
            .Be(Base.Ceiling(unit, TimeSpan.FromMinutes(80)));
        Base.Ceiling(unit, TimeSpan.FromMinutes(20)).Should()
            .Be(Base.Ceiling(unit, TimeSpan.FromMinutes(-40)));
    }

    [Fact]
    public void AZeroUnitIsANoOp()
    {
        // SetDelayUntil only rejects a negative quanta, so zero is reachable - and it used to
        // divide by zero here
        Base.Floor(TimeSpan.Zero).Should().Be(Base);
        Base.Ceiling(TimeSpan.Zero).Should().Be(Base);
        Base.Round(TimeSpan.Zero).Should().Be(Base);
    }
}
