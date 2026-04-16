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

    /// <summary>
    /// Wire-format fixtures shared with the TypeScript port at
    /// <c>ts/packages/rpc/tests/increasing-seq-compressor.test.ts</c>.
    /// Both implementations MUST produce the same bytes for these inputs —
    /// this is the cross-platform contract for <c>$sys.Reconnect</c>.
    /// </summary>
    [Theory]
    [InlineData(new long[] { }, new byte[] { })]
    [InlineData(new long[] { 0 }, new byte[] { 0x00 })]
    [InlineData(new long[] { 1 }, new byte[] { 0x01 })]
    [InlineData(new long[] { 127 }, new byte[] { 0x7f })]
    [InlineData(new long[] { 128 }, new byte[] { 0x80, 0x01 })]
    [InlineData(new long[] { 300 }, new byte[] { 0xac, 0x02 })]
    [InlineData(new long[] { 16384 }, new byte[] { 0x80, 0x80, 0x01 })]
    [InlineData(new long[] { 10, 11 }, new byte[] { 0x0a, 0x01 })]
    [InlineData(new long[] { 10, 10 }, new byte[] { 0x0a, 0x00 })]
    [InlineData(new long[] { 128, 129 }, new byte[] { 0x80, 0x01, 0x01 })]
    public void CrossPlatformWireFormatFixtures(long[] input, byte[] expectedBytes)
    {
        var actual = IncreasingSeqCompressor.Serialize(input);
        actual.Should().Equal(expectedBytes);

        // And the inverse: .NET must decode its own output back to the input.
        var decoded = IncreasingSeqCompressor.Deserialize(expectedBytes);
        decoded.Should().Equal(input);
    }
}
