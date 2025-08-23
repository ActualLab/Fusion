namespace ActualLab.Tests.Collections;

public class SpanAndMemoryExtTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void SpanTest()
    {
        Span<byte> source = [1, 2, 3, 4];
        var readOnly = (ReadOnlySpan<byte>)source;
#if NETCOREAPP3_1_OR_GREATER
        var writeable = readOnly.AsSpanUnsafe();
#else
        var writeable = source;
#endif
        writeable.Length.Should().Be(readOnly.Length);
        writeable[0] = 10;
        readOnly[0].Should().Be(10);
        source[0].Should().Be(10);

        // Write/Read unchecked
        writeable.WriteUnchecked(1);
        readOnly.ReadUnchecked<int>().Should().Be(1);
    }

    [Fact]
    public void MemoryTest()
    {
        Memory<byte> source = new byte[] {1, 2, 3};
        var readOnly = (ReadOnlyMemory<byte>)source;
        var writeable = readOnly.AsMemoryUnsafe();
        writeable.Span[0] = 10;
        readOnly.Span[0].Should().Be(10);
        source.Span[0].Should().Be(10);
    }
}
