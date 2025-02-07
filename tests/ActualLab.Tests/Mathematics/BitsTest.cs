namespace ActualLab.Tests.Mathematics;

public class BitsTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void BasicTest()
    {
        Bits.PopCount(ulong.MaxValue).Should().Be(64);
        Bits.LeadingBitIndex(0).Should().Be(64);
        Bits.LeadingZeroCount(0).Should().Be(64);
        Bits.TrailingZeroCount(0).Should().Be(64);
        Bits.LeadingBitMask(0).Should().Be(0);
        Bits.TrailingBitMask(0).Should().Be(0);
        for (var i = 0; i < 64; i++) {
            var x = 1ul << i;
            var xl = x | (x >> 1) | (x >> 5);
            var xh = x | (x << 1) | (x << 5);
            Bits.LeadingBitIndex(xl).Should().Be(i);
            Bits.LeadingZeroCount(xl).Should().Be(63 - i);
            Bits.TrailingZeroCount(xh).Should().Be(i);
            Bits.LeadingBitMask(xl).Should().Be(x);
            Bits.TrailingBitMask(xh).Should().Be(x);
            Bits.IsPowerOf2(x).Should().BeTrue();
            Bits.IsPowerOf2(xh).Should().Be(x == xh);
            Bits.IsPowerOf2(xl).Should().Be(x == xl);
        }
    }

    [Fact]
    public void ReadWrite7BitEncodedTest()
    {
        Span<byte> buffer = stackalloc byte[10];

        for (var shift = 0; shift < 64; shift++) {
            var value = 1ul << shift;
            Test(value - 1, buffer);
            Test(value, buffer);
            Test(value + 1, buffer);
        }

        void Test(ulong value, Span<byte> buffer) {
            buffer.Clear();
            var size = Bits.Write7BitEncoded(value, buffer);
            size.Should().BeLessThanOrEqualTo(buffer.Length);
            Out.WriteLine($"{value} -> {size} bytes");

            var readBuffer = buffer[..size];
            var readSize = Bits.Read7BitEncoded(readBuffer, out var readValue);
            readValue.Should().Be(value);
            readSize.Should().Be(size);
        }
    }
}
