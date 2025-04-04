namespace ActualLab.Tests.Api;

public class ApiXxxSerializationTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void ApiArrayTest()
    {
        ApiArray<string>.Empty.Should().BeEmpty();

        for (var length = 0; length < 100; length++) {
            var c = Enumerable.Range(0, length).ToApiArray();
            Out.WriteLine($"Testing: {c}");
            var s = c.PassThroughAllSerializers(Out);
            s.Should().Equal(c);

            if (length > 3) {
                c[3].Should().Be(3);
                c[1..3].Should().Equal(1, 2);
            }

            var c1 = Enumerable.Range(0, length).Select(x => Option.Some(x.ToString())).ToApiArray();
            Out.WriteLine($"Testing: {c}");
            var s1 = c1.PassThroughAllSerializers(Out);
            s1.Should().Equal(c1);
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
}
