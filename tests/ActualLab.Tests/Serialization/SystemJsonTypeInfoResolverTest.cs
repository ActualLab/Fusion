#if NET8_0_OR_GREATER
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ActualLab.Tests.Serialization;

public partial class SystemJsonTypeInfoResolverTest(ITestOutputHelper @out) : TestBase(@out)
{
    public sealed record Payload(string Value);

    [JsonSerializable(typeof(Payload))]
    internal sealed partial class PayloadContext : JsonSerializerContext;

    [Fact]
    public void AddedContextShouldResolveItsTypes()
    {
        // arrange
        var resolver = new SystemJsonTypeInfoResolver();
        resolver.Add(PayloadContext.Default);
        var options = new JsonSerializerOptions { TypeInfoResolver = resolver };

        // act
        var json = JsonSerializer.Serialize(new Payload("x"), options);

        // assert
        json.Should().Be("""{"Value":"x"}""");
    }

    // System.Text.Json caches the failed lookup in the options, so a context must be added
    // before the types it covers are first serialized
    [Fact]
    public void ContextAddedAfterFailedLookupShouldNotBeUsed()
    {
        // arrange
        var resolver = new SystemJsonTypeInfoResolver();
        var options = new JsonSerializerOptions { TypeInfoResolver = resolver };
        var write = () => JsonSerializer.Serialize(new Payload("x"), options);
        write.Should().Throw<NotSupportedException>();

        // act
        resolver.Add(PayloadContext.Default);

        // assert
        write.Should().Throw<NotSupportedException>();
    }
}
#endif
