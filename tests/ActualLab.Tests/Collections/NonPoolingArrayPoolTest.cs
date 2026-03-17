using System.Buffers;
using ActualLab.Collections.Internal;

namespace ActualLab.Tests.Collections;

public class NonPoolingArrayPoolTest
{
    [Fact]
    public void InstanceIsSingleton()
    {
        var instance1 = NonPoolingArrayPool<int>.Instance;
        var instance2 = NonPoolingArrayPool<int>.Instance;
        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public void RentReturnsArrayOfAtLeastRequestedLength()
    {
        var pool = NonPoolingArrayPool<int>.Instance;
        var array = pool.Rent(10);
        array.Should().NotBeNull();
        array.Length.Should().BeGreaterThanOrEqualTo(10);
    }

    [Fact]
    public void RentReturnsNewArrayEachTime()
    {
        var pool = NonPoolingArrayPool<int>.Instance;
        var array1 = pool.Rent(10);
        var array2 = pool.Rent(10);
        array1.Should().NotBeSameAs(array2);
    }

    [Fact]
    public void RentZeroLengthReturnsEmptyArray()
    {
        var pool = NonPoolingArrayPool<int>.Instance;
        var array = pool.Rent(0);
        array.Should().NotBeNull();
        array.Length.Should().Be(0);
    }

    [Fact]
    public void ReturnDoesNotThrow()
    {
        var pool = NonPoolingArrayPool<int>.Instance;
        var array = pool.Rent(10);
        var act = () => pool.Return(array);
        act.Should().NotThrow();
    }

    [Fact]
    public void ReturnWithClearDoesNotThrow()
    {
        var pool = NonPoolingArrayPool<int>.Instance;
        var array = pool.Rent(10);
        var act = () => pool.Return(array, clearArray: true);
        act.Should().NotThrow();
    }

    [Fact]
    public void ReturnedArrayIsNotReused()
    {
        var pool = NonPoolingArrayPool<byte>.Instance;
        var array1 = pool.Rent(16);
        pool.Return(array1);
        var array2 = pool.Rent(16);
        array2.Should().NotBeSameAs(array1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1024)]
    [InlineData(65536)]
    public void RentVariousSizes(int size)
    {
        var pool = NonPoolingArrayPool<byte>.Instance;
        var array = pool.Rent(size);
        array.Should().NotBeNull();
        array.Length.Should().BeGreaterThanOrEqualTo(size);
    }

    [Fact]
    public void WorksWithReferenceTypes()
    {
        var pool = NonPoolingArrayPool<string>.Instance;
        var array = pool.Rent(5);
        array.Should().NotBeNull();
        array.Length.Should().BeGreaterThanOrEqualTo(5);
        pool.Return(array);
    }

    [Fact]
    public void IsAssignableToArrayPool()
    {
        ArrayPool<int> pool = NonPoolingArrayPool<int>.Instance;
        pool.Should().NotBeNull();
        pool.Should().BeOfType<NonPoolingArrayPool<int>>();
    }
}
