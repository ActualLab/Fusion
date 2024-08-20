using System.Numerics;

namespace ActualLab.Tests.Collections;

public class ArrayExtTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void SortInPlaceTest1()
    {
        var rnd = new Random();
        for (var i = 0; i < 1000; i++) {
            const int itemCount = 20;
            var a = Enumerable.Range(0, itemCount)
                .OrderBy(_ => rnd.NextDouble())
                .ToArray(itemCount);
            var b = a.OrderBy(x => x).ToArray(itemCount);

            a = a.SortInPlace();
            a.Length.Should().Be(itemCount);
            a.Should().Equal(b);

            a = a.SortInPlace(Comparer<int>.Default);
            a.Length.Should().Be(itemCount);
            a.Should().Equal(b);
        }
    }

    [Fact]
    public void SortInPlaceTest2()
    {
        var rnd = new Random();
        for (var i = 0; i < 1000; i++) {
            const int itemCount = 20;
            var a = Enumerable.Range(0, 10)
                .Select(i => new Vector2(-i, i))
                .OrderBy(_ => rnd.NextDouble())
                .ToArray(itemCount);
            var b = a.OrderBy(x => x.Y).ToArray(itemCount);

            a = a.SortInPlace(x => x.Y);
            a.Length.Should().Be(itemCount);
            a.Should().Equal(b);
        }
    }
}
