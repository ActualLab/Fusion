namespace ActualLab.Tests.Api;

public class ApiXxxSerializationTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void ApiArrayTest()
    {
        ApiArray<string>.Empty.Should().BeEmpty();
        default(ApiArray<string>).Should().BeEmpty();

        for (var length = 0; length < 10; length++) {
            var c = Enumerable.Range(0, length).ToApiArray();
            Out.WriteLine($"Testing: {c}");
            var s = c.PassThroughAllSerializers(Out);
            s.Should().Equal(c);

            if (length > 3) {
                c[3].Should().Be(3);
                c[1..3].Should().Equal(1, 2);
            }
        }

        for (var length = 0; length < 10; length++) {
            var c = Enumerable.Range(0, length)
                .Select(i => new Session($"WhateverString-{i}"))
                .ToApiArray();
            Out.WriteLine($"Testing: {c}");
            var s = c.PassThroughAllSerializers(Out);
            s.Should().Equal(c);
        }
    }

    [Fact]
    public void ApiListTest()
    {
        for (var length = 0; length < 10; length++) {
            var c = Enumerable.Range(0, length).ToApiList();
            Out.WriteLine($"Testing: {c}");
            var s = c.PassThroughAllSerializers(Out);
            s.Should().Equal(c);
        }
    }

    [Fact]
    public void ApiMapTest()
    {
        for (var length = 0; length < 10; length++) {
            var c = Enumerable.Range(0, length).Select(i => KeyValuePair.Create(i, i)).ToApiMap();
            Out.WriteLine($"Testing: {c}");
            var s = c.PassThroughAllSerializers(Out);
            s.Count.Should().Be(s.Count);
            foreach (var (key, value) in s)
                value.Should().Be(c[key]);
        }
    }

    [Fact]
    public void ApiSetTest()
    {
        for (var length = 0; length < 10; length++) {
            var c = Enumerable.Range(0, length).ToApiSet();
            Out.WriteLine($"Testing: {c}");
            var s = c.PassThroughAllSerializers(Out);
            s.Count.Should().Be(s.Count);
            s.Should().BeSubsetOf(c);
        }
    }

    [Fact]
    public void ApiNullableTest()
    {
        Test(0);
        Test(10);
        Test(0L);
        Test(10L);

        void Test<T>(T value)
            where T : struct
        {
            ApiNullable<T>.None.AssertPassesThroughAllSerializers(Out)
                .IsNone.Should().BeTrue();
            ApiNullable4<T>.None.AssertPassesThroughAllSerializers(Out)
                .IsNone.Should().BeTrue();
            ApiNullable8<T>.None.AssertPassesThroughAllSerializers(Out)
                .IsNone.Should().BeTrue();
            ApiNullable.Some(value).AssertPassesThroughAllSerializers(Out)
                .IsSome(out _).Should().BeTrue();
            ApiNullable4.Some(value).AssertPassesThroughAllSerializers(Out)
                .IsSome(out _).Should().BeTrue();
            ApiNullable8.Some(value).AssertPassesThroughAllSerializers(Out)
                .IsSome(out _).Should().BeTrue();
        }
    }
}
