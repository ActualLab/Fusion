using ActualLab.Rpc.Internal;

namespace ActualLab.Tests.Rpc;

public class IncreasingSeqCompressorTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void Test()
    {
        var rnd = new Random();
        var testedLengths = new HashSet<int>();
        for (var i = 0; i < 1000; i++) {
            var seq = Enumerable.Range(0, rnd.Next(20))
                .Select(_ => (long)rnd.Next(100))
                .OrderBy(x => x)
                .ToArray();
            testedLengths.Add(seq.Length);

            var data = IncreasingSeqCompressor.Serialize(seq);
            var seq1 = IncreasingSeqCompressor.Deserialize(data);
            seq1.Should().Equal(seq);
        }
        WriteLine($"Tested lengths: {testedLengths.OrderBy(x => x).ToDelimitedString()}");
    }
}
