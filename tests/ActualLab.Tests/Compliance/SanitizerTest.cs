using ActualLab.Compliance;

namespace ActualLab.Tests.Compliance;

public class SanitizerTest
{
    [Fact]
    public void GetReturnsOneSharedInstance()
    {
        Sanitizer.Get<Sanitizers.Hidden>().Should().BeSameAs(Sanitizer.Get<Sanitizers.Hidden>());
        // Both overloads must agree, or two call sites could disagree about policy
        Sanitizer.Get(typeof(Sanitizers.Hidden)).Should().BeSameAs(Sanitizer.Get<Sanitizers.Hidden>());
    }

    [Fact]
    public void MaybeSanitizeHonoursSuspension()
    {
        const string secret = "hunter2";

        Sanitizer.MaybeSanitize<Sanitizers.Hidden>(secret).Should().Be(Sanitizers.HiddenValue);
        Sanitizer.Get<Sanitizers.Hidden>().MaybeApply(secret).Should().Be(Sanitizers.HiddenValue);

        using (Sanitization.Suspend()) {
            Sanitizer.MaybeSanitize<Sanitizers.Hidden>(secret).Should().Be(secret);
            Sanitizer.Get<Sanitizers.Hidden>().MaybeApply(secret).Should().Be(secret);
            // Sanitize() itself is unconditional - the "Maybe" is what reads the ambient state
            Sanitizer.Get<Sanitizers.Hidden>().Apply(secret).Should().Be(Sanitizers.HiddenValue);
        }
    }

    [Fact]
    public void SessionStringMatchesSessionToString()
    {
        // The format is Session.ToString()'s: a 4-char prefix, ':' and the bare XxHash3.
        // Session.Default is "~", and its hash is pinned by SessionHashTest.
        Sanitizer.Get<Sanitizers.SessionString>().Apply("~").Should().Be("~:9644ea3b");

        var sanitized = Sanitizer.Get<Sanitizers.SessionString>().Apply("Ab3fSomeLongSessionId");
        sanitized.Should().StartWith("Ab3f:");
        sanitized.Should().MatchRegex("^Ab3f:[0-9a-f]{8}$");
        sanitized.Should().NotContain("SomeLongSessionId");
    }

    [Fact]
    public void SessionStringIsStable()
    {
        var a = Sanitizer.Get<Sanitizers.SessionString>().Apply("some-session-id");
        var b = Sanitizer.Get<Sanitizers.SessionString>().Apply("some-session-id");
        a.Should().Be(b);
        Sanitizer.Get<Sanitizers.SessionString>().Apply("other-session-id").Should().NotBe(a);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("?", "?")]
    [InlineData("?f=mempack6", "?f=mempack6")]
    [InlineData("f=mempack6", "f=mempack6")] // works with or without the leading '?'
    [InlineData("?flag", "?flag")] // a valueless parameter is left alone
    [InlineData("?p=Zm9vYmFy", "?p=<<Zm* [8-15]>>")]
    [InlineData("?p=abc&c=7", "?p=<<* [2-3]>>&c=7")]
    [InlineData("?P=Zm9vYmFy", "?P=<<Zm* [8-15]>>")] // names are matched case-insensitively
    // Deny by default: an unlisted parameter is masked rather than logged, so a new one
    // can't start leaking a credential just because no one remembered to list it
    [InlineData("?secret=hunter2", "?secret=<<hu* [4-7]>>")]
    public void RpcRequestQuerySanitizesTheCredentialParameters(string query, string expected)
        => Sanitizer.Get<Sanitizers.RpcRequestQuery>().Apply(query).Should().Be(expected);

    [Fact]
    public void RpcRequestQueryMasksSessionAndClientId()
    {
        var sanitized = Sanitizer.Get<Sanitizers.RpcRequestQuery>()
            .Apply("?clientId=x7FTKcK88zakKdYBij3p-w&session=Ab3fSomeLongSessionId&f=mempack6");

        sanitized.Should().NotContain("x7FTKcK88zakKdYBij3p-w");
        sanitized.Should().NotContain("SomeLongSessionId");
        sanitized.Should().MatchRegex(@"^\?clientId=<<x7\* \[16-31\]>>&session=Ab3f:[0-9a-f]{8}&f=mempack6$");
    }

    [Fact]
    public void RpcRequestQueryIsCacheable()
        // The point of deriving from UriQuery with a fixed policy: it has a parameterless
        // constructor, so it can be a SanitizedString<> type argument.
        => new SanitizedString<Sanitizers.RpcRequestQuery>("?p=Zm9vYmFy").ToString()
            .Should().Be("?p=<<Zm* [8-15]>>");

    [Fact]
    public void RpcRequestQueryHidesAPercentEncodedSessionParameter()
    {
        // "%73ession" decodes to "session" but doesn't match it by name. Deny-by-default is what
        // keeps it out of the log anyway - an allowlist policy would have had to predict this.
        const string sessionId = "Ab3fSomeLongSessionId";
        var sanitized = Sanitizer.Get<Sanitizers.RpcRequestQuery>().Apply($"?%73ession={sessionId}");

        sanitized.Should().NotContain(sessionId);
        sanitized.Should().Be($"?%73ession={Sanitizer.Sanitize<Sanitizers.PrefixAndLengthHint>(sessionId)}");
    }

    [Fact]
    public void RpcRequestQueryIgnoresTheAmbientSanitizationScope()
    {
        // Request logging calls Apply, not MaybeApply: a suspended scope somewhere up the stack
        // must never turn a credential back on in the log.
        const string sessionId = "Ab3fSomeLongSessionId";
        using var _ = Sanitization.Suspend();

        Sanitizer.Get<Sanitizers.RpcRequestQuery>()
            .Apply($"?session={sessionId}").Should().NotContain(sessionId);
    }

    [Fact]
    public void UriQueryLeavesUnmappedParametersAlone()
    {
        var sanitizer = new Sanitizers.UriQuery(
            name => string.Equals(name, "a", StringComparison.Ordinal) ? Sanitizer.Get<Sanitizers.Hidden>() : null);

        sanitizer.Apply("?a=1&b=2").Should().Be("?a=" + Sanitizers.HiddenValue + "&b=2");
    }
}
