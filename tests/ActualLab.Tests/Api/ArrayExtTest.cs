namespace ActualLab.Tests.Api;

public class ArrayExtTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void SerializationTest()
    {
        void Equal(int[] x, int[] v) => x.Should().Equal(v);

        Array.Empty<int>().AssertPassesThroughAllSerializers(Equal, Out);
        new[] {1}.AssertPassesThroughAllSerializers(Equal, Out);
        new[] {1, 2}.AssertPassesThroughAllSerializers(Equal, Out);
    }

    [Fact]
    public void WithTest()
    {
        var a = Enumerable.Range(0, 5).ToArray();
        a.Should().HaveCount(5);

        a.WithOrSkip(0).Should().HaveCount(5);
        a = a.With(6);
        a.Should().HaveCount(6);

        a = a.Without(0);
        a[0].Should().Be(1);
        a.Should().HaveCount(5);

        a = a.Without(-1);
        a.Should().HaveCount(5);

        a = a.Without(item => item > 3);
        a.Should().HaveCount(3);
        a = a.Without((_, index) => index >= 2);
        a.Should().HaveCount(2);

        a = a.ToTrimmed(5);
        a.Should().HaveCount(2);

        a = a.ToTrimmed(1);
        a.Should().HaveCount(1);
        a[0].Should().Be(1);
    }

    [Fact]
    public void WithManyTest()
    {
        var a = Enumerable.Range(0, 5).ToArray();
        a.Should().HaveCount(5);

        var b = a.WithMany(6, 7);
        b.Should().HaveCount(7);
        b[0].Should().Be(0);
        b[^1].Should().Be(7);

        b = a.WithMany(true, 6, 7);
        b.Should().HaveCount(7);
        b[0].Should().Be(6);
        b[^1].Should().Be(4);
    }

    [Fact]
    public void WithOrReplaceTest()
    {
        var a = Enumerable.Range(0, 5).ToArray();
        a.Should().HaveCount(5);

        a.WithOrReplace(6).Should().HaveCount(6);

        a.Should().HaveCount(5);
        a = a.With(6);
        a.Should().HaveCount(6);

        a = a.WithOrReplace(6);
        a.Should().HaveCount(6);

        a = a.WithOrReplace(8);
        a.Should().HaveCount(7);
    }

    [Fact]
    public void WithOrUpdateTest()
    {
        var a = Enumerable.Range(0, 5).ToArray();
        a.Should().HaveCount(5);

        a.WithOrUpdate(5, i => i + 100).Should().HaveCount(6);

        a.Should().HaveCount(5);
        a.Should().HaveCount(5);
        a = a.With(5);
        a.Should().HaveCount(6);

        a = a.WithOrUpdate(5, i => i + 2);
        a.Should().HaveCount(6);
        a[5].Should().Be(7);

        a = a.WithOrUpdate(3, i => i + 2);
        a.Should().HaveCount(6);
        a[3].Should().Be(5);
    }

    [Fact]
    public void UpdateTest()
    {
        var a = Enumerable.Range(0, 5).ToApiArray();
        a.Should().HaveCount(5);

        a.WithUpdate(i => i == 5, i => i + 100).Should().BeEquivalentTo([0, 1, 2, 3, 4], o => o.WithStrictOrdering());
        a.WithUpdate(i => i == 4, i => i + 100).Should().BeEquivalentTo([0, 1, 2, 3, 104], o => o.WithStrictOrdering());
        a.WithUpdate(i => i != 5, i => i + 100).Should().BeEquivalentTo([100, 101, 102, 103, 104], o => o.WithStrictOrdering());
        a.Should().BeEquivalentTo([0, 1, 2, 3, 4], o => o.WithStrictOrdering());
    }
}
