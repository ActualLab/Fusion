namespace ActualLab.Tests.Api;

public class ApiOptionTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void BasicTest()
    {
        Test(0);
        Test(10);
        Test((string?)null!);
        Test("s");

        void Test<T>(T value)
        {
            var v = ApiOption<T>.None.AssertPassesThroughAllSerializers(Out);
            v.HasValue.Should().BeFalse();
            v.ValueOrDefault.Should().Be(default(T));
            v.Should().Be(Option.None<T>());
            v.IsSome(out _).Should().BeFalse();
            v.ToOption().Should().Be(Option.None<T>());
            Option.None<T>().ToApiOption().Should().Be(v);

            v = ApiOption.Some(value).AssertPassesThroughAllSerializers(Out);
            v.HasValue.Should().BeTrue();
            v.ValueOrDefault.Should().Be(value);
            v.Should().Be(new(true, value));
            v.Should().Be(ApiOption.Some(value));
            v.IsSome(out var x).Should().BeTrue();
            x.Should().Be(value);
            v.ToOption().Should().Be(Option.Some(value));
            Option.Some(value).ToApiOption().Should().Be(v);
        }
    }
}
