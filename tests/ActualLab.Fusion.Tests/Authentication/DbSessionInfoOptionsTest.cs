using ActualLab.Fusion.Authentication.Services;
using ActualLab.Tests;

namespace ActualLab.Fusion.Tests.Authentication;

#pragma warning disable CS0618 // ImmutableOptionSet is used here solely to produce the legacy OptionsJson

public class DbSessionInfoOptionsTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void OptionsRoundTrip()
    {
        var dbSessionInfo = new DbSessionInfo<long> {
            Options = PropertyBag.Empty.Set("a", 1).KeylessSet(true),
        };
        var optionsJson = dbSessionInfo.OptionsJson;
        Out.WriteLine(optionsJson);

        var clone = new DbSessionInfo<long> { OptionsJson = optionsJson };
        clone.Options.Count.Should().Be(2);
        clone.Options["a"].Should().Be(1);
        clone.Options.KeylessGet<bool>().Should().BeTrue();
    }

    [Fact]
    public void LegacyOptionsJsonIsReadAsEmpty()
    {
        var legacyOptions = ImmutableOptionSet.Empty.Set(1).Set("x");
        var legacyOptionsJson = NewtonsoftJsonSerialized.New(legacyOptions).Data;
        Out.WriteLine(legacyOptionsJson);

        var dbSessionInfo = new DbSessionInfo<long> { Id = "someSessionId" };
        dbSessionInfo.OptionsJson = legacyOptionsJson;
        dbSessionInfo.Options.Count.Should().Be(0);
        dbSessionInfo.Id.Should().Be("someSessionId"); // The rest of the row is unaffected
    }

    [Fact]
    public void LegacyOptionsJsonMustNotConstructArbitraryTypes()
    {
        // The C1 payload: TypeNameHandling.Auto used to honor $type for the object-typed option values.
        var dbSessionInfo = new DbSessionInfo<long> {
            OptionsJson = """
                {"Items":{"x":{"$type":"System.Text.StringBuilder, System.Private.CoreLib","Capacity":4242}}}
                """,
        };
        dbSessionInfo.Options.Count.Should().Be(0);
    }

    [Fact]
    public void GarbageOptionsJsonIsReadAsEmpty()
    {
        var dbSessionInfo = new DbSessionInfo<long> { OptionsJson = "{ this is not JSON" };
        dbSessionInfo.Options.Count.Should().Be(0);
    }
}
