namespace ActualLab.Tests.Collections;

public class SpanExtTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void ReadWriteVarUInt32()
    {
        Span<byte> buffer = stackalloc byte[5];

        for (var shift = 0; shift < 32; shift++) {
            var value = 1u << shift;
            Test(value - 1, buffer);
            Test(value, buffer);
            Test(value + 1, buffer);
        }
        Assert.Throws<FormatException>(() => {
            var incorrectBuffer = Enumerable.Range(0, 5).Select(_ => (byte)0xff).ToArray();
            incorrectBuffer.ReadVarUInt32();
        });
        return;

        void Test(uint value, Span<byte> buffer) {
            buffer.Clear();
            var size = buffer.WriteVarUInt32(value);
            size.Should().BeLessThanOrEqualTo(buffer.Length);
            WriteLine($"{value} -> {size} bytes");

            var readBuffer = buffer[..size];
            var (readValue, readSize) = readBuffer.ReadVarUInt32();
            readValue.Should().Be(value);
            readSize.Should().Be(size);
        }
    }

    [Fact]
    public void ReadWriteVarUInt64()
    {
        Span<byte> buffer = stackalloc byte[10];

        for (var shift = 63; shift < 64; shift++) {
            var value = 1ul << shift;
            Test(value - 1, buffer);
            Test(value, buffer);
            Test(value + 1, buffer);
        }
        Assert.Throws<FormatException>(() => {
            var incorrectBuffer = Enumerable.Range(0, 10).Select(_ => (byte)0xff).ToArray();
            incorrectBuffer.ReadVarUInt32();
        });
        return;

        void Test(ulong value, Span<byte> buffer) {
            buffer.Clear();
            var size = buffer.WriteVarUInt64(value);
            size.Should().BeLessThanOrEqualTo(buffer.Length);
            WriteLine($"{value} -> {size} bytes");

            var readBuffer = buffer[..size];
            var (readValue, readSize) = readBuffer.ReadVarUInt64();
            readValue.Should().Be(value);
            readSize.Should().Be(size);
        }
    }
}
