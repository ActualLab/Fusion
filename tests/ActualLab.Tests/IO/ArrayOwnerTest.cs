using ActualLab.IO;

namespace ActualLab.Tests.IO;

public class ArrayOwnerTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void BasicTest1()
    {
        var poolBuffer = new ArrayPoolBuffer<byte>(100, false);
        poolBuffer.Advance(50); // Simulate writing 50 bytes
        var holder = poolBuffer.ToArrayOwnerAndReset(100);

        // Initial state - refcount is 0, IsDisposed is true but array is still available
        holder.Length.Should().Be(50);
        holder.Array.Should().NotBeNull();

        // Can access the array
        holder.Array[0] = 42;
        holder.Array[0].Should().Be(42);

        // Dispose unconditionally returns array to pool
        holder.Dispose();
        holder.Array.Should().BeNull();
    }

    [Fact]
    public void BasicTest2()
    {
        var poolBuffer = new ArrayPoolBuffer<byte>(100, false);

        // Write some data
        var span = poolBuffer.GetSpan(10);
        for (var i = 0; i < 10; i++)
            span[i] = (byte)i;
        poolBuffer.Advance(10);

        // Replace and get ref counting buffer
        var holder = poolBuffer.ToArrayOwnerAndReset(100);

        // Ref counting buffer should have the data
        holder.Length.Should().Be(10);
        holder.Array[0].Should().Be(0);
        holder.Array[9].Should().Be(9);

        // Clean up
        holder.Dispose();
        poolBuffer.Dispose();
    }
}
