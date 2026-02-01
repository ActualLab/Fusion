using ActualLab.IO;

namespace ActualLab.Tests.Collections;

public class ArrayPoolBufferTest
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
                    Test(list);
                }
            }
        }
    }

    private void Test<T>(List<T> list)
    {
        using var buffer = new ArrayPoolBuffer<T>(mustClear: true);
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

    [Fact]
    public void TestEnsureCapacity0()
    {
        using var b = new ArrayPoolBuffer<int>();
        b.Capacity.Should().BeGreaterThanOrEqualTo(256);

        b.EnsureCapacity(1000);
        b.Capacity.Should().BeGreaterThanOrEqualTo(1000);

        var oldCapacity = b.Capacity;
        b.EnsureCapacity(100);
        b.Capacity.Should().Be(oldCapacity);
    }

    [Fact]
    public void TestEnsureCapacity1()
    {
        const int minCapacity = 16; // ArrayPoolBuffer.MinCapacity
        using var b = new ArrayPoolBuffer<int>(mustClear: true);
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
}
