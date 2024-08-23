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
            var v1 = ApiNullable<T>.Null.AssertPassesThroughAllSerializers(Out);
            v1.HasValue.Should().BeFalse();
            v1.Value.Should().BeNull();
            v1.Should().Be((T?)null);
            v1.Should().Be(((T?)null).ToApiNullable());
            v1.Should().Be(ApiNullable.Null<T>());
            v1.Should().Be(ApiNullable.From((T?)null));
            v1.IsValue(out _).Should().BeFalse();
            v1.ToOption().Should().Be(Option.None<T>());

            var v2 = ApiNullable8<T>.Null.AssertPassesThroughAllSerializers(Out);
            v2.HasValue.Should().BeFalse();
            v2.Value.Should().BeNull();
            v2.Should().Be((T?)null);
            v2.Should().Be(((T?)null).ToApiNullable8());
            v2.Should().Be(ApiNullable8.Null<T>());
            v2.Should().Be(ApiNullable8.From((T?)null));
            v1.IsValue(out _).Should().BeFalse();
            v2.ToOption().Should().Be(Option.None<T>());

            v1 = ApiNullable.Value(value).AssertPassesThroughAllSerializers(Out);
            v1.Should().Be(new(true, value));
            v1.Should().Be(value);
            v1.Should().Be((T?)value);
            v1.Should().Be(((T?)value).ToApiNullable());
            v1.Should().Be(ApiNullable.Value(value));
            v1.Should().Be(ApiNullable.From((T?)value));
            v1.IsValue(out var x1).Should().BeTrue();
            x1.Should().Be(value);
            v1.ToOption().Should().Be(Option.Some(value));

            v2 = ApiNullable8.Value(value).AssertPassesThroughAllSerializers(Out);
            v2.Should().Be(new(true, value));
            v2.Should().Be(value);
            v2.Should().Be((T?)value);
            v2.Should().Be(((T?)value).ToApiNullable8());
            v2.Should().Be(ApiNullable8.Value(value));
            v2.Should().Be(ApiNullable8.From((T?)value));
            v2.IsValue(out var x2).Should().BeTrue();
            x2.Should().Be(value);
            v2.ToOption().Should().Be(Option.Some(value));
        }
    }
}
