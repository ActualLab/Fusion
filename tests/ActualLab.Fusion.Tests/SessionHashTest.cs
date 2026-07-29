using ActualLab.Compliance;
using System.ComponentModel;
using ActualLab.Conversion;
using ActualLab.Fusion.Internal;
using ActualLab.RestEase.Internal;

namespace ActualLab.Fusion.Tests;

public class SessionHashTest
{
    [Fact]
    public void Sha256HashIsWellFormed()
    {
        var session = Session.New();
        session.Sha256Hash.Length.Should().Be(32);
        session.Hash.Length.Should().Be(8);
        session.Hash.Should().MatchRegex("^[0-9a-f]+$");
    }

    [Fact]
    public void HashesAreStableAcrossInstances()
    {
        var id = Session.New().Id;
        var session1 = new Session(id);
        var session2 = new Session(id);
        session2.Sha256Hash.ToArray().Should().Equal(session1.Sha256Hash.ToArray());
        session2.Hash.Should().Be(session1.Hash);
        session2.ToString().Should().Be(session1.ToString());

        var other = Session.New();
        other.Sha256Hash.ToArray().Should().NotEqual(session1.Sha256Hash.ToArray());
        other.Hash.Should().NotBe(session1.Hash);
    }

    [Fact]
    public void KnownHashesTest()
    {
        using var _ = Sanitization.Begin();
        // Hash must stay the legacy XxHash3-based value: it's on the wire via
        // SessionAuthInfo.SessionHash and Auth_SignOut.KickUserSessionHash.
        Session.Default.Hash.Should().Be("9644ea3b");
        Session.Default.ToString().Should().Be("~:9644ea3b");

        // `printf '~' | sha256sum`
        Convert.ToHexString(Session.Default.Sha256Hash.ToArray()).ToLowerInvariant().Should()
            .Be("7ace431cb61584cb9b8dc7ec08cf38ac0a2d649660be86d349fb43108b542fa4");

        // `printf 'test-session-id' | sha256sum`
        var session = new Session("test-session-id");
        Convert.ToHexString(session.Sha256Hash.ToArray()).ToLowerInvariant().Should()
            .Be("08001f8fa6f5dbb9a20ddf1e8366af93a76815f84035cfd2e93233475c968279");
    }

    [Fact]
    public void ToStringIsRedacted()
    {
        using var _ = Sanitization.Begin();
        var session = Session.New();
        var id = session.Id;
        id.Length.Should().BeGreaterThan(Session.IdPrefixLength);

        var s = session.ToString();
        s.Should().Be(string.Concat(id.AsSpan(0, Session.IdPrefixLength), Session.IdPrefixSeparator, session.Hash));
        s.Should().NotContain(id);
        // Nothing beyond the intended prefix leaks: the 5th+ chars of Id must be absent
        s.Should().NotContain(id.Substring(0, Session.IdPrefixLength + 1));
        s.Should().NotContain(id.Substring(Session.IdPrefixLength));
    }

    [Fact]
    public void ConversionsStillRoundTripTheId()
    {
        var session = Session.New();
        ((IConvertibleTo<string>)session).Convert().Should().Be(session.Id);

        var converter = TypeDescriptor.GetConverter(typeof(Session));
        converter.Should().BeOfType<SessionTypeConverter>();
        var s = converter.ConvertToString(session);
        s.Should().Be(session.Id);
        converter.ConvertFromString(s!).Should().Be(session);
    }

    [Fact]
    public void RestEaseQueryParamStillUsesTheId()
    {
        var session = Session.New();
        new TestQueryParamSerializer().Serialize(session).Should().Be(session.Id);
    }

    // Nested types

    private sealed class TestQueryParamSerializer : RestEaseRequestQueryParamSerializer
    {
        public string? Serialize(object source)
            => SerializeSimpleType(source, default);
    }
}
