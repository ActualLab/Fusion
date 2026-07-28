namespace ActualLab.Tests.Collections;

public class VersionSetTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void OrderIndependenceTest()
    {
        var ab = new VersionSet(("a", "1.0"), ("b", "2.0"));
        var ba = new VersionSet(("b", "2.0"), ("a", "1.0"));
        ab.Should().Be(ba);
        ab.HashCode.Should().Be(ba.HashCode);
        ab.GetHashCode().Should().Be(ba.GetHashCode());

        new VersionSet(("a", "1.0")).Should().NotBe(new VersionSet(("a", "1.1")));
        new VersionSet(("a", "1.0")).Should().NotBe(new VersionSet(("b", "1.0")));
    }

    [Fact]
    public void EmptyHashCodeTest()
    {
        VersionSet.Empty.HashCode.Should().Be(1);
        new VersionSet().HashCode.Should().Be(1);
    }

    [Fact]
    public void XorCollisionCandidatesDontCollideTest()
    {
        // A XOR fold is linear over GF(2)^32, so any 33+ single-item hashes are linearly dependent:
        // some subset of them XORs to 0, and under a XOR fold that subset's VersionSet hashes exactly
        // like the empty one. Gaussian elimination finds such a subset from hashes the attacker can
        // read off single-item sets, which is what makes XOR collisions constructible here.
        var items = Enumerable.Range(0, 64)
            .Select(i => ($"scope{i}", new Version(1, i)))
            .ToArray();
        var subset = FindXorZeroSubset(items);
        subset.Length.Should().BeGreaterThan(0);
        WriteLine($"Collision candidate: {subset.Length} of {items.Length} item(s)");

        var xorHashCode = 0;
        foreach (var (scope, version) in subset)
            xorHashCode ^= new VersionSet(scope, version).HashCode;
        xorHashCode.Should().Be(0);

        var versions = new VersionSet(subset);
        versions.HashCode.Should().NotBe(VersionSet.Empty.HashCode);
        versions.Should().NotBe(VersionSet.Empty);
    }

    [Fact]
    public void KeepScopesTest()
    {
        var versions = new VersionSet(("a", "1.0"), ("b", "2.0"), ("c", "3.0"));
        var abc = new HashSet<string>(StringComparer.Ordinal) { "a", "b", "c", "d" };
        versions.KeepScopes(abc).Should().BeSameAs(versions);

        var ac = versions.KeepScopes(new HashSet<string>(StringComparer.Ordinal) { "a", "c" });
        ac.Should().Be(new VersionSet(("a", "1.0"), ("c", "3.0")));

        var none = versions.KeepScopes(new HashSet<string>(StringComparer.Ordinal) { "x" });
        none.Should().BeSameAs(VersionSet.Empty);
        VersionSet.Empty.KeepScopes(abc).Should().BeSameAs(VersionSet.Empty);
    }

    // Private methods

    private static (string Scope, Version Version)[] FindXorZeroSubset((string Scope, Version Version)[] items)
    {
        // A XOR basis over GF(2)^32: each basis vector carries the mask of items that produced it, so
        // the first item that reduces to zero without being inserted names a subset XORing to zero.
        var basis = new (uint Value, ulong Mask)[32];
        for (var i = 0; i < items.Length; i++) {
            var (scope, version) = items[i];
            var value = (uint)new VersionSet(scope, version).HashCode;
            var mask = 1ul << i;
            for (var bit = 31; bit >= 0 && value != 0; bit--) {
                if ((value & (1u << bit)) == 0)
                    continue;

                if (basis[bit].Value == 0) {
                    basis[bit] = (value, mask);
                    value = 0;
                    mask = 0;
                    break;
                }
                value ^= basis[bit].Value;
                mask ^= basis[bit].Mask;
            }
            if (value == 0 && mask != 0)
                return items.Where((_, j) => (mask & (1ul << j)) != 0).ToArray();
        }
        return [];
    }
}
