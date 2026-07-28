using ActualLab.Rpc.Internal;

namespace ActualLab.Tests.Rpc;

public sealed class RpcQuerySanitizerTest
{
    private const string SessionId = "s-1234567890-must-never-be-logged";
    private const string ClientId = "0123456789abcdef";

    [Fact]
    public void RequestDescriptionTest()
    {
        var query = $"?clientId={ClientId}&f=mempack6&session={SessionId}";
        var sanitizedQuery = RpcQuerySanitizer.Sanitize(query);
        var uri = new UriBuilder("https", "example.com", -1, "/rpc/ws", sanitizedQuery);
        var requestDescription = $"GET {uri}";

        requestDescription.Should().NotContain(SessionId);
        requestDescription.Should().NotContain(ClientId);
        sanitizedQuery.Should().Be(
            $"?clientId={RpcQuerySanitizer.Hash(ClientId)}&f=mempack6&session={RpcQuerySanitizer.RedactedValue}");
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("?", "")]
    [InlineData("?f=json5", "?f=json5")]
    [InlineData("?serializationFormat=json5", "?serializationFormat=json5")]
    [InlineData("?session=", "?session=")]
    [InlineData("?SESSION=abc", "?SESSION=<redacted>")]
    [InlineData("?%73ession=abc", "?%73ession=<redacted>")]
    [InlineData("?session=a&session=b", "?session=<redacted>&session=<redacted>")]
    [InlineData("?token", "?token")]
    [InlineData("?apiKey=abc&f=json5", "?apiKey=<redacted>&f=json5")]
    [InlineData("?c=42&p=Zm9v", "?c=42&p=<redacted>")]
    public void SanitizeTest(string? query, string expected)
        => RpcQuerySanitizer.Sanitize(query).Should().Be(expected);

    [Fact]
    public void HashTest()
    {
        var hash = RpcQuerySanitizer.Hash(ClientId);
        hash.Length.Should().Be(8);
        hash.Should().Be(RpcQuerySanitizer.Hash(ClientId));
        hash.Should().NotBe(RpcQuerySanitizer.Hash(ClientId + "!"));
    }
}
