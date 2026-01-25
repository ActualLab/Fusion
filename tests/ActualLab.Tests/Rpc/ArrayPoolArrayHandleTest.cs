using ActualLab.IO;

namespace ActualLab.Tests.Rpc;

public class ArrayPoolArrayHandleTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void BasicLifecycleTest()
    {
        var poolBuffer = new ArrayPoolBuffer<byte>(100, false);
        poolBuffer.Advance(50); // Simulate writing 50 bytes
        var holder = poolBuffer.ReplaceAndReturnArrayHandle(100);

        // Initial state - refcount is 0, IsDisposed is true but array is still available
        holder.WrittenCount.Should().Be(50);
        holder.Array.Should().NotBeNull();

        // Can access the array
        holder.Array[0] = 42;
        holder.Array[0].Should().Be(42);

        // Dispose unconditionally returns array to pool
        holder.Dispose();
        holder.Array.Should().BeNull();
    }

    [Fact]
    public void AddRefAndReleaseTest()
    {
        var poolBuffer = new ArrayPoolBuffer<byte>(100, false);
        poolBuffer.Advance(100);
        var holder = poolBuffer.ReplaceAndReturnArrayHandle(poolBuffer.Capacity);

        // Add references via AddRef
        var ref1 = holder.NewRef();
        var ref2 = holder.NewRef();
        var ref3 = holder.NewRef();

        // Release refs one by one - buffer should not be disposed
        ref1.Dispose();
        holder.IsDisposed.Should().BeFalse();

        ref2.Dispose();
        holder.IsDisposed.Should().BeFalse();

        // Final release
        ref3.Dispose();
        holder.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void ArrayPoolBufferRefTest()
    {
        var poolBuffer = new ArrayPoolBuffer<byte>(100, false);
        poolBuffer.Advance(100);
        var holder = poolBuffer.ReplaceAndReturnArrayHandle(poolBuffer.Capacity);

        // Create a ref via AddRef
        var ref1 = holder.NewRef();
        ref1.IsNone.Should().BeFalse();
        ref1.Handle.Should().Be(holder);

        // Dispose ref1 - buffer should be released
        ref1.Dispose();
        holder.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void MultipleBufferRefsTest()
    {
        var poolBuffer = new ArrayPoolBuffer<byte>(100, false);
        poolBuffer.Advance(100);
        var holder = poolBuffer.ReplaceAndReturnArrayHandle(poolBuffer.Capacity);

        // Create multiple refs
        var ref1 = holder.NewRef();
        var ref2 = holder.NewRef();
        var ref3 = holder.NewRef();

        holder.IsDisposed.Should().BeFalse();

        // Dispose refs one by one
        ref1.Dispose();
        holder.IsDisposed.Should().BeFalse();

        ref2.Dispose();
        holder.IsDisposed.Should().BeFalse();

        ref3.Dispose();
        holder.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void ConcurrentRefCountingTest()
    {
        var poolBuffer = new ArrayPoolBuffer<byte>(100, false);
        poolBuffer.Advance(100);
        var holder = poolBuffer.ReplaceAndReturnArrayHandle(poolBuffer.Capacity);

        const int threadCount = 10;
        const int iterationsPerThread = 1000;

        // Create refs for all threads
        var threadRefs = new ArrayPoolArrayRef<byte>[threadCount];
        for (var i = 0; i < threadCount; i++)
            threadRefs[i] = holder.NewRef();

        // Write some data
        holder.Array[0] = 123;

        var tasks = new Task[threadCount];

        for (var t = 0; t < threadCount; t++) {
            var threadIndex = t;
            tasks[t] = Task.Run(() => {
                // Each thread does add/release pairs
                for (var i = 0; i < iterationsPerThread; i++) {
                    var tempRef = holder.NewRef();
                    tempRef.Dispose();
                }
                // Final release of this thread's reference
                threadRefs[threadIndex].Dispose();
            });
        }

        Task.WaitAll(tasks);

        holder.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void ReplaceAndReturnRefCountingBufferTest()
    {
        var poolBuffer = new ArrayPoolBuffer<byte>(100, false);

        // Write some data
        var span = poolBuffer.GetSpan(10);
        for (var i = 0; i < 10; i++)
            span[i] = (byte)i;
        poolBuffer.Advance(10);

        // Replace and get ref counting buffer
        var holder = poolBuffer.ReplaceAndReturnArrayHandle(100);

        // Ref counting buffer should have the data
        holder.WrittenCount.Should().Be(10);
        holder.Array[0].Should().Be(0);
        holder.Array[9].Should().Be(9);

        // Clean up
        holder.Dispose();
        poolBuffer.Dispose();
    }
}
