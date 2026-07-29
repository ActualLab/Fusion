using ActualLab.Compliance;

namespace ActualLab.Tests.Compliance;

public class SanitizerTest
{
    [Fact]
    public void GetReturnsOneSharedInstance()
    {
        Sanitizer.Get<Sanitizer.Hidden>().Should().BeSameAs(Sanitizer.Get<Sanitizer.Hidden>());
        // Both overloads must agree, or two call sites could disagree about policy
        Sanitizer.Get(typeof(Sanitizer.Hidden)).Should().BeSameAs(Sanitizer.Get<Sanitizer.Hidden>());
    }

    [Fact]
    public void SessionStringMatchesSessionToString()
    {
        // The format is Session.ToString()'s: a 4-char prefix, ':' and the bare XxHash3.
        // Session.Default is "~", and its hash is pinned by SessionHashTest.
        Sanitizer.Get<Sanitizer.SessionString>().Sanitize("~").Should().Be("~:9644ea3b");

        var sanitized = Sanitizer.Get<Sanitizer.SessionString>().Sanitize("Ab3fSomeLongSessionId");
        sanitized.Should().StartWith("Ab3f:");
        sanitized.Should().MatchRegex("^Ab3f:[0-9a-f]{8}$");
        sanitized.Should().NotContain("SomeLongSessionId");
    }

    [Fact]
    public void SessionStringIsStable()
    {
        var a = Sanitizer.Get<Sanitizer.SessionString>().Sanitize("some-session-id");
        var b = Sanitizer.Get<Sanitizer.SessionString>().Sanitize("some-session-id");
        a.Should().Be(b);
        Sanitizer.Get<Sanitizer.SessionString>().Sanitize("other-session-id").Should().NotBe(a);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("?", "?")]
    [InlineData("?f=mempack6", "?f=mempack6")]
    [InlineData("f=mempack6", "f=mempack6")] // works with or without the leading '?'
    [InlineData("?flag", "?flag")] // a valueless parameter is left alone
    [InlineData("?p=Zm9vYmFy", "?p=" + Sanitizer.HiddenValue)]
    [InlineData("?p=abc&c=7", "?p=" + Sanitizer.HiddenValue + "&c=7")]
    [InlineData("?P=Zm9vYmFy", "?P=" + Sanitizer.HiddenValue)] // names are matched case-insensitively
    [InlineData("?secret=hunter2", "?secret=hunter2")] // not an RPC query parameter - the reconnect secret travels in the handshake
    public void RpcRequestQuerySanitizesTheCredentialParameters(string query, string expected)
        => Sanitizer.Get<Sanitizer.RpcRequestQuery>().Sanitize(query).Should().Be(expected);

    [Fact]
    public void RpcRequestQueryMasksSessionAndClientId()
    {
        var sanitized = Sanitizer.Get<Sanitizer.RpcRequestQuery>()
            .Sanitize("?clientId=x7FTKcK88zakKdYBij3p-w&session=Ab3fSomeLongSessionId&f=mempack6");

        sanitized.Should().NotContain("x7FTKcK88zakKdYBij3p-w");
        sanitized.Should().NotContain("SomeLongSessionId");
        sanitized.Should().MatchRegex(@"^\?clientId=<<[0-9a-f]{8}>>&session=Ab3f:[0-9a-f]{8}&f=mempack6$");
    }

    [Fact]
    public void RpcRequestQueryIsCacheable()
        // The point of deriving from UriQuery with a fixed policy: it has a parameterless
        // constructor, so it can be a SanitizedString<> type argument.
        => new SanitizedString<Sanitizer.RpcRequestQuery>("?p=Zm9vYmFy").ToString()
            .Should().Be("?p=" + Sanitizer.HiddenValue);

    [Fact]
    public void UriQueryLeavesUnmappedParametersAlone()
    {
        var sanitizer = new Sanitizer.UriQuery(
            name => string.Equals(name, "a", StringComparison.Ordinal) ? Sanitizer.Get<Sanitizer.Hidden>() : null);

        sanitizer.Sanitize("?a=1&b=2").Should().Be("?a=" + Sanitizer.HiddenValue + "&b=2");
    }
}
