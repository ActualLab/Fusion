using ActualLab.IO;

namespace ActualLab.Tests.Collections;

public class RefArrayPoolBufferTest
{
    private readonly Random _rnd = new();

    [Fact]
    public void CombinedTest()
    {
        for (var iteration = 0; iteration < 10; iteration++) {
            for (var length = 0; length < 50; length++) {
                var list = new List<byte>(length);
                for (var i = 0; i < length; i++) {
                    list.Add((byte)(_rnd.Next() % 256));
                    TestByte(list);
                }
            }
        }
    }

    private void TestByte(List<byte> list)
    {
        var buffer = new RefArrayPoolBuffer<byte>(mustClear: true);
        try {
            foreach (var i in list)
                buffer.Add(i);
            buffer.ToArray().Should().Equal(list);

            for (var _ = 0; _ < 5; _++) {
                if (buffer.Count == 0)
                    break;

                var idx = _rnd.Next(list.Count);
                var item = buffer[idx];
                buffer.RemoveAt(idx);
                list.RemoveAt(idx);
                buffer.ToArray().Should().Equal(list);

                idx = _rnd.Next(list.Count);
                buffer.Insert(idx, item);
                list.Insert(idx, item);
                buffer.ToArray().Should().Equal(list);

                idx = _rnd.Next(list.Count);
                (buffer[idx], list[idx]) = (list[idx], buffer[idx]);
                buffer.ToArray().Should().Equal(list);
            }
        }
        finally {
            buffer.Release();
        }
    }

    [Fact]
    public void TestEnsureCapacity0()
    {
        var b = new RefArrayPoolBuffer<int>();
        try {
            b.Capacity.Should().BeGreaterThanOrEqualTo(256);

            b.EnsureCapacity(1000);
            b.Capacity.Should().BeGreaterThanOrEqualTo(1000);

            var oldCapacity = b.Capacity;
            b.EnsureCapacity(100);
            b.Capacity.Should().Be(oldCapacity);
        }
        finally {
            b.Release();
        }
    }

    [Fact]
    public void TestEnsureCapacity1()
    {
        const int minCapacity = 16; // RefArrayPoolBuffer.MinCapacity
        var b = new RefArrayPoolBuffer<int>(mustClear: true);
        try {
            for (var i = 0; i < 3; i++) {
                var capacity = b.Capacity;
                capacity.Should().BeGreaterThanOrEqualTo(minCapacity);
                var numbers = Enumerable.Range(0, capacity + 1).ToArray();
                b.AddSpan(numbers.AsSpan());
                b.Capacity.Should().BeGreaterThanOrEqualTo(capacity << 1);
            }

            b.Reset();
            b.Capacity.Should().BeGreaterThanOrEqualTo(minCapacity);

            // Same test, but with .AddRange(IEnumerable<T>)
            for (var i = 0; i < 3; i++) {
                var capacity = b.Capacity;
                capacity.Should().BeGreaterThanOrEqualTo(minCapacity);
                var numbers = Enumerable.Range(0, capacity + 1);
                b.AddRange(numbers);
                b.Capacity.Should().BeGreaterThanOrEqualTo(capacity << 1);
            }
        }
        finally {
            b.Release();
        }
    }

    [Fact]
    public void TestWithSharedPool()
    {
        var b = new RefArrayPoolBuffer<byte>(ArrayPools.SharedBytePool, 32, mustClear: false);
        try {
            b.Pool.Should().BeSameAs(ArrayPools.SharedBytePool);
            b.Add(1);
            b.Add(2);
            b.Add(3);
            b.Count.Should().Be(3);
            b.ToArray().Should().Equal([1, 2, 3]);
        }
        finally {
            b.Release();
        }
    }

    [Fact]
    public void TestWrittenSpanAndMemory()
    {
        var b = new RefArrayPoolBuffer<int>(16, mustClear: false);
        try {
            b.Add(10);
            b.Add(20);
            b.Add(30);

            b.WrittenSpan.ToArray().Should().Equal([10, 20, 30]);
            b.WrittenMemory.ToArray().Should().Equal([10, 20, 30]);
            b.WrittenCount.Should().Be(3);
            b.WrittenArraySegment.Count.Should().Be(3);
        }
        finally {
            b.Release();
        }
    }

    [Fact]
    public void TestGetSpanAndAdvance()
    {
        var b = new RefArrayPoolBuffer<byte>(16, mustClear: false);
        try {
            var span = b.GetSpan(3);
            span[0] = 1;
            span[1] = 2;
            span[2] = 3;
            b.Advance(3);

            b.Count.Should().Be(3);
            b.ToArray().Should().Equal([1, 2, 3]);
        }
        finally {
            b.Release();
        }
    }

    [Fact]
    public void TestRenew()
    {
        var b = new RefArrayPoolBuffer<int>(1024, mustClear: false);
        try {
            b.Capacity.Should().BeGreaterThanOrEqualTo(1024);
            b.Add(1);
            b.Add(2);
            b.Count.Should().Be(2);

            b.Renew(32);
            b.Count.Should().Be(0);
            b.Capacity.Should().BeLessThanOrEqualTo(64); // Renewed to smaller capacity
        }
        finally {
            b.Release();
        }
    }

    [Fact]
    public void TestEnumerator()
    {
        var b = new RefArrayPoolBuffer<int>(16, mustClear: false);
        try {
            b.Add(1);
            b.Add(2);
            b.Add(3);

            var sum = 0;
            foreach (var item in b)
                sum += item;

            sum.Should().Be(6);
        }
        finally {
            b.Release();
        }
    }
}
