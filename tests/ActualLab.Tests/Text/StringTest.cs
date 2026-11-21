namespace ActualLab.Tests.Text;

#pragma warning disable CA1820

public class StringTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void EqualsTest()
    {
        string.Equals(null, null, StringComparison.Ordinal).Should().BeTrue();
        string.Equals(null, "", StringComparison.Ordinal).Should().BeFalse();
    }

    [Fact]
    public void StableHashTest()
    {
        "abc".AsSpan().GetDjb2HashCode().Should().Be(-1549454715);
        "abc".GetXxHash3().Should().Be(-1701228566);
    }

    [Fact]
    public void Djb2HashCodeShouldMatchCommunityToolkitValues()
    {
        // FilePath.GetHashedName persists these values, so they must never change.
        // The expected values were computed by CommunityToolkit.HighPerformance 8.4.0.
        "".AsSpan().GetDjb2HashCode().Should().Be(5381);
        "a".AsSpan().GetDjb2HashCode().Should().Be(6534596);
        "0_0".AsSpan().GetDjb2HashCode().Should().Be(-510429798);
        "Hello, World!".AsSpan().GetDjb2HashCode().Should().Be(1147760072);
        "ActualLab.Fusion_DbContext_Operations".AsSpan().GetDjb2HashCode().Should().Be(-1478490691);
        "Привет".AsSpan().GetDjb2HashCode().Should().Be(697289447);
        "1234567890abcdefXYZ".AsSpan().GetDjb2HashCode().Should().Be(-2055123176);
    }

    [Fact]
    public void Djb2HashCodeShouldMatchNaiveImplementation()
    {
        var random = new Random(1);
        for (var length = 0; length <= 64; length++)
        for (var pass = 0; pass < 20; pass++) {
            var chars = new char[length + 3];
            for (var i = 0; i < chars.Length; i++)
                chars[i] = (char)random.Next(0, 0x10000); // Includes NULs and unpaired surrogates
            var offset = random.Next(0, 4);
            var source = chars.AsSpan(offset, Math.Min(length, chars.Length - offset));

            source.GetDjb2HashCode().Should().Be(NaiveDjb2(source));
        }
        return;

        static int NaiveDjb2(ReadOnlySpan<char> source)
        {
            var hash = 5381;
            foreach (var c in source)
                hash = unchecked((hash * 33) ^ c.GetHashCode());
            return hash;
        }
    }
}
