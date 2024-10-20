namespace ActualLab.Tests.Mathematics;

public class PrimeSieveTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void BasicTest()
    {
        var sieve = PrimeSieve.GetOrCompute();
        var range = Enumerable.Range(1, 10);
        foreach (var x in range)
            Out.WriteLine($"{x} => {sieve.IsPrime(x)}");
        Assert.Equal([
                true,
                false,
                true,
                false,
                true,
                false,
                true,
                false,
                false,
                false
            ],
            Enumerable.Range(1, 10).Select(x => sieve.IsPrime(x)));
        Assert.False(sieve.IsPrime(1299825));
        Assert.True(sieve.IsPrime(1299827));
    }

    [Fact]
    public void PrecomputedPrimesTest()
    {
        var sieve = PrimeSieve.GetOrCompute();
        foreach (var prime in PrimeSieve.PrecomputedPrimes)
            sieve.IsPrime(prime).Should().BeTrue();

        PrimeSieve.GetPrecomputedPrime(1).Should().Be(1);
        PrimeSieve.GetPrecomputedPrime(2).Should().Be(3);
        PrimeSieve.GetPrecomputedPrime(3).Should().Be(3);
        PrimeSieve.GetPrecomputedPrime(4).Should().Be(7);
        PrimeSieve.GetPrecomputedPrime(64).Should().Be(71);
        var lastPrime = PrimeSieve.PrecomputedPrimes[^1];
        PrimeSieve.GetPrecomputedPrime(lastPrime).Should().Be(lastPrime);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => PrimeSieve.GetPrecomputedPrime(lastPrime + 1));
    }
}
