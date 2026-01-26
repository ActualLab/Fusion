namespace ActualLab.Tests.Collections;

public class SpanExtTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void ReadWriteVarUInt32()
    {
        Span<byte> buffer = stackalloc byte[16];

        foreach (var value in UInt32Values())
            TestRoundtrip(value, buffer);

        // Invalid: 5th byte must be <= 15 and must not have the high bit set
        Assert.Throws<FormatException>(() => new byte[] { 0x80, 0x80, 0x80, 0x80, 0x10 }.ReadVarUInt32());
        Assert.Throws<FormatException>(() => new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80 }.ReadVarUInt32());
        return;

        static void TestRoundtrip(uint value, Span<byte> buffer)
        {
            foreach (var startOffset in new[] { 0, 1, 2, 7 }) {
                buffer.Clear();
                var endOffset = buffer.WriteVarUInt32(value, startOffset);
                endOffset.Should().BeGreaterThan(startOffset);
                endOffset.Should().BeLessThanOrEqualTo(buffer.Length);
                var size = endOffset - startOffset;
                size.Should().BeGreaterThanOrEqualTo(1);
                size.Should().BeLessThanOrEqualTo(5);

                var (readValue, readEndOffset) = ((ReadOnlySpan<byte>)buffer).ReadVarUInt32(startOffset);
                readValue.Should().Be(value);
                readEndOffset.Should().Be(endOffset);

                AssertLength(value, size);
            }
        }

        static void AssertLength(uint value, int size)
        {
            var expectedSize = value switch {
                <= 0x7Fu => 1,
                <= 0x3FFFu => 2,
                <= 0x1F_FFFFu => 3,
                <= 0x0FFF_FFFFu => 4,
                _ => 5,
            };
            size.Should().Be(expectedSize);
        }
    }

    [Fact]
    public void ReadWriteVarUInt64()
    {
        Span<byte> buffer = stackalloc byte[32];

        foreach (var value in UInt64Values())
            TestRoundtrip(value, buffer);

        // Invalid: 10th byte must be <= 1 and must not have the high bit set
        Assert.Throws<FormatException>(() => new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x02 }.ReadVarUInt64());
        Assert.Throws<FormatException>(() => new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80 }.ReadVarUInt64());
        return;

        static void TestRoundtrip(ulong value, Span<byte> buffer)
        {
            foreach (var startOffset in new[] { 0, 1, 2, 11 }) {
                buffer.Clear();
                var endOffset = buffer.WriteVarUInt64(value, startOffset);
                endOffset.Should().BeGreaterThan(startOffset);
                endOffset.Should().BeLessThanOrEqualTo(buffer.Length);
                var size = endOffset - startOffset;
                size.Should().BeGreaterThanOrEqualTo(1);
                size.Should().BeLessThanOrEqualTo(10);

                var (readValue, readEndOffset) = ((ReadOnlySpan<byte>)buffer).ReadVarUInt64(startOffset);
                readValue.Should().Be(value);
                readEndOffset.Should().Be(endOffset);

                AssertLength(value, size);
            }
        }

        static void AssertLength(ulong value, int size)
        {
            var expectedSize = value switch {
                <= 0x7Ful => 1,
                <= 0x3FFFul => 2,
                <= 0x1F_FFFFul => 3,
                <= 0x0FFF_FFFFul => 4,
                <= 0x7_FFFF_FFFFul => 5,
                <= 0x3FF_FFFF_FFFFul => 6,
                <= 0x1_FFFF_FFFF_FFFFul => 7,
                <= 0xFF_FFFF_FFFF_FFFFul => 8,
                <= 0x7FFF_FFFF_FFFF_FFFFul => 9,
                _ => 10,
            };
            size.Should().Be(expectedSize);
        }
    }

    private static IEnumerable<ulong> UInt64Values()
    {
        yield return 0ul;
        yield return 1ul;
        yield return 0x7Ful;
        yield return 0x80ul;
        yield return 0x3FFFul;
        yield return 0x4000ul;
        var v = 1ul;
        for (int i = 0; i < 64; i++) {
            yield return v;        // 2^i
            yield return v + 1ul;  // 2^i + 1
            v <<= 1;
        }
        yield return ulong.MaxValue;
    }

    private static IEnumerable<uint> UInt32Values()
    {
        yield return 0u;
        yield return 1u;
        yield return 0x7Fu;
        yield return 0x80u;
        yield return 0x3FFFu;
        yield return 0x4000u;
        var v = 1u;
        for (int i = 0; i < 32; i++) {
            yield return v;       // 2^i
            yield return v + 1u;  // 2^i + 1
            v <<= 1;
        }
        yield return uint.MaxValue;
    }
}
