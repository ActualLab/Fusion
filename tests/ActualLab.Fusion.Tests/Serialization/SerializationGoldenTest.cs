#if NET8_0_OR_GREATER
using ActualLab.Fusion.Authentication;
using ActualLab.Fusion.Extensions;
using ActualLab.Tests.Serialization;

namespace ActualLab.Fusion.Tests.Serialization;

public class SerializationGoldenTest(ITestOutputHelper @out) : GoldenSerializationTestBase(@out)
{
    private static readonly Moment FixedMoment = new(new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc));
    private static readonly Session FixedSession = new("golden-session-id-0001");

    [Fact]
    public void OutputShouldMatchGolden()
        => AssertOutputMatchesGolden();

    protected override IEnumerable<GoldenCase> GetCases()
    {
        var user = NewUser();
        var identity = new UserIdentity("google", "user-1");

        yield return Case("Session.Null", (Session?)null);
        yield return Case("Session.Default", Session.Default);
        yield return Case("Session.Value", FixedSession);
        yield return Case("UserIdentity.Default", default(UserIdentity));
        yield return Case("UserIdentity.Value", identity);
        yield return Case("User.Guest", new User("", "Guest"));
        yield return Case("User.Value", user);
        yield return Case("SessionAuthInfo.Default", new SessionAuthInfo());
        yield return Case("SessionAuthInfo.Value", new SessionAuthInfo(FixedSession) {
            AuthenticatedIdentity = identity,
            UserId = "user-1",
            IsSignOutForced = true,
        });
        yield return Case("SessionInfo.Default", new SessionInfo());
        yield return Case("SessionInfo.Value", new SessionInfo(FixedSession, FixedMoment) {
            AuthenticatedIdentity = identity,
            UserId = "user-1",
            Version = 3,
            LastSeenAt = FixedMoment + TimeSpan.FromMinutes(1),
            IPAddress = "127.0.0.1",
            UserAgent = "Golden/1.0",
            Options = new PropertyBag().Set("Int", 1).Set("String", "x"),
        });
        yield return Case("PageRef<int>.Count", PageRef.New<int>(10));
        yield return Case("PageRef<int>.After", PageRef.New(10, 5));
        yield return Case("PageRef<string>.After", PageRef.New(10, "key"));
        yield return Case("Auth_EditUser.Value", new Auth_EditUser(FixedSession, "Name"));
        yield return Case("Auth_SignOut.Value",
            new Auth_SignOut(FixedSession, "hash", kickAllUserSessions: true, force: true));
        yield return Case("AuthBackend_SetSessionOptions.Value", new AuthBackend_SetSessionOptions(
            FixedSession, new PropertyBag().Set("Int", 1), 7));
        yield return Case("SandboxedKeyValueStore_Set.Value", new SandboxedKeyValueStore_Set(
            FixedSession, [("a", "1", null), ("b", "2", FixedMoment)]));
        yield return Case("SandboxedKeyValueStore_Remove.Value", new SandboxedKeyValueStore_Remove(
            FixedSession, ["a", "b"]));
        yield return Case("AuthBackend_SignIn.Value", new AuthBackend_SignIn(FixedSession, user, identity));
        yield return Case("AuthBackend_SetupSession.Value", new AuthBackend_SetupSession(
            FixedSession, "127.0.0.1", "Golden/1.0", new PropertyBag().Set("Int", 1)));
        yield return Case("KeyValueStore_Set.Value", new KeyValueStore_Set(
            "shard", [("a", "1", null), ("b", "2", FixedMoment)]));
        yield return Case("KeyValueStore_Remove.Value", new KeyValueStore_Remove("shard", ["a", "b"]));
    }

    // Private methods

    private static User NewUser()
        => new User("user-1", "Alice") { Version = 2 }
            .WithClaim("role", "admin")
            .WithClaim("email", "alice@example.com")
            .WithIdentity(new UserIdentity("google", "user-1"), "secret")
            .WithIdentity(new UserIdentity("github", "alice"));
}
#endif
