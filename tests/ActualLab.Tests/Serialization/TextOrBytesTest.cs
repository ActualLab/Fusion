using System.Buffers;

namespace ActualLab.Tests.Serialization;

public class TextOrBytesTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void StringRoundTripShouldReturnOriginalString()
    {
        // arrange
        var source = "Hello, World!";

        // act
        var data = new TextOrBytes(source);
        var isText = data.IsText(out var text);

        // assert
        isText.Should().BeTrue();
        data.Data.Length.Should().Be(source.Length * 2);
        MemoryMarshal.TryGetString(text, out var textString, out var start, out var length).Should().BeTrue();
        textString.Should().BeSameAs(source);
        (start, length).Should().Be((0, source.Length));
    }

    [Fact]
    public void SlicedTextShouldRoundTrip()
    {
        var source = "0123456789".AsMemory(3, 4);

        var data = new TextOrBytes(source);
        data.IsText(out var text).Should().BeTrue();

        new string(text.Span.ToArray()).Should().Be("3456");
        data.ToString().Should().Be("[ 4 char(s): `3456` ]");
        data.ToString(2).Should().Be("[ 4 char(s): `34`... ]");
    }

    [Fact]
    public void TextViewShouldAliasByteArray()
    {
        // arrange
        var bytes = MemoryMarshal.AsBytes("ab".AsSpan()).ToArray();
        var data = new TextOrBytes(DataFormat.Text, bytes);

        // act
        data.IsText(out var text).Should().BeTrue();
        bytes[0] = (byte)'z';

        // assert
        new string(text.Span.ToArray()).Should().Be("zb");
        data.Bytes.Should().Equal(bytes);
    }

    [Fact]
    public void OddByteOffsetsAndLengthsShouldWork()
    {
        var bytes = MemoryMarshal.AsBytes("abcd".AsSpan()).ToArray();

        new TextOrBytes(DataFormat.Text, bytes.AsMemory(0, 7)).IsText(out var text1).Should().BeTrue();
        new string(text1.Span.ToArray()).Should().Be("abc");

        new TextOrBytes(DataFormat.Text, bytes.AsMemory(1, 4)).IsText(out var text2).Should().BeTrue();
        text2.Length.Should().Be(2);
        text2.Span.SequenceEqual(MemoryMarshal.Cast<byte, char>(bytes.AsSpan(1, 4))).Should().BeTrue();
    }

    [Fact]
    public void EmptyTextShouldWork()
    {
        var data = new TextOrBytes("");

        data.IsEmpty.Should().BeTrue();
        data.IsText(out var text).Should().BeTrue();
        text.IsEmpty.Should().BeTrue();
        data.Bytes.Should().BeEmpty();
        data.ToString().Should().Be("[ 0 char(s): `` ]");
    }

    [Fact]
    public void ManagerBackedTextShouldWork()
    {
        // arrange
        using var manager = new TestMemoryManager("xyz".ToCharArray());
        var data = new TextOrBytes((ReadOnlyMemory<char>)manager.Memory);

        // act
        data.IsText(out var text).Should().BeTrue();
        var bytes = data.Bytes;

        // assert
        new string(text.Span.ToArray()).Should().Be("xyz");
        bytes.Should().Equal(MemoryMarshal.AsBytes("xyz".AsSpan()).ToArray());
    }

    [Fact]
    public unsafe void PinsShouldBeIndependent()
    {
        // arrange
        var data = new TextOrBytes("abcdef");
        data.IsText(out var text).Should().BeTrue();
        var bytes = data.Data;

        // act
        var handle1 = bytes.Slice(4).Pin();
        var handle2 = bytes.Pin();
        handle1.Dispose();
        var c2 = *(char*)((byte*)handle2.Pointer + 4);
        handle2.Dispose();
        using var handle3 = text.Slice(3).Pin();

        // assert
        c2.Should().Be('c');
        (*(char*)handle3.Pointer).Should().Be('d');
    }

    [Fact]
    public unsafe void PinnedTextViewOfBytesShouldPointAtTheRightChar()
    {
        // arrange
        var bytes = MemoryMarshal.AsBytes("abcdef".AsSpan()).ToArray();
        var data = new TextOrBytes(DataFormat.Text, bytes);
        data.IsText(out var text).Should().BeTrue();

        // act
        using var handle1 = text.Slice(1).Pin(); // Calls Pin(1), which must scale 1 by sizeof(char)
        using var handle4 = text.Slice(4).Pin();

        // assert
        (*(char*)handle1.Pointer).Should().Be('b');
        (*(char*)handle4.Pointer).Should().Be('e');
    }

    // Nested types

    private sealed class TestMemoryManager(char[] array) : MemoryManager<char>
    {
        public override Span<char> GetSpan()
            => array;

        public override unsafe MemoryHandle Pin(int elementIndex = 0)
        {
            var handle = GCHandle.Alloc(array, GCHandleType.Pinned);
            var pointer = (char*)handle.AddrOfPinnedObject() + elementIndex;
            return new MemoryHandle(pointer, handle);
        }

        public override void Unpin()
        { }

        protected override void Dispose(bool disposing)
        { }
    }
}
