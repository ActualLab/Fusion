using ActualLab.Generators;

namespace ActualLab.Tests.Generators;

public class UlidUuidGeneratorTest(ITestOutputHelper @out) : TestBase(@out)
{
    private const string Base32Chars = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    [Fact]
    public void FormatTest()
    {
        var generator = UlidUuidGenerator.Instance;
        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var ids = Enumerable.Range(0, 1000).Select(_ => generator.Next()).ToList();
        var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Out.WriteLine(ids[0]);

        ids.Distinct(StringComparer.Ordinal).Count().Should().Be(ids.Count);
        foreach (var id in ids) {
            id.Length.Should().Be(26);
            id.All(c => Base32Chars.Contains(c)).Should().BeTrue();
            id[0].Should().BeLessThanOrEqualTo('7'); // 26 chars hold 130 bits, so the top 2 bits are 0
            var timestamp = DecodeTimestamp(id);
            timestamp.Should().BeInRange(before, after);
        }
    }

    [Fact]
    public async Task OrderingTest()
    {
        var generator = UlidUuidGenerator.Instance;
        var id1 = generator.Next();
        await Task.Delay(5);
        var id2 = generator.Next();
        string.CompareOrdinal(id1, id2).Should().BeNegative();
    }

    [Fact]
    public async Task ThreadsTest()
    {
        var ids = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => Enumerable.Range(0, 1000).Select(_ => UlidUuidGenerator.Instance.Next()).ToList())));
        var all = ids.SelectMany(x => x).ToList();
        all.Distinct(StringComparer.Ordinal).Count().Should().Be(all.Count);
    }

    // The first 10 chars encode the 48-bit timestamp (50 bits, the top 2 are 0)
    private static long DecodeTimestamp(string id)
    {
        var result = 0L;
        for (var i = 0; i < 10; i++)
            result = (result << 5) | (long)Base32Chars.IndexOf(id[i]);
        return result;
    }
}
