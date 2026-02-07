using System.Reflection;
using ActualLab.Fusion.Authentication;
using ActualLab.Fusion.Tests.DbModel;
using ActualLab.Fusion.Tests.Services;
using ActualLab.Generators;
using ActualLab.Testing.Collections;
using ActualLab.Tests;
using User = ActualLab.Fusion.Authentication.User;

namespace ActualLab.Fusion.Tests.Serialization;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class SerializationTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void SessionSerialization()
    {
        default(Session).AssertPassesThroughAllSerializers(Out);
        new Session("0123456789-0123456789").AssertPassesThroughAllSerializers(Out);
    }

    [Fact]
    public void UserSerialization()
    {
        var user = new User("b", "bob");
        AssertEqual(user.PassThroughAllSerializers(Out), user);

        user = new User("b", "bob") { Version = 3 }
            .WithClaim("email1", "bob1@bob.bom")
            .WithClaim("email2", "bob2@bob.bom")
            .WithIdentity("google/1", "s")
            .WithIdentity("google/2", "q");
        AssertEqual(user.PassThroughAllSerializers(Out), user);
    }

    [Fact(Skip =
        "Must be executed manually: MemoryPack caches formatters, "
        + "so StringAsSymbolMemoryPackFormatterAttribute.Formatter is accessed just once.")]
    public void OldAndNewUserSerializationCompatibility()
    {
        StringAsSymbolMemoryPackFormatterAttribute.IsEnabled = true;
        try {
            // Old to new
            var oldUser = new Types.OldUser("b", "bob");
            var newUser = oldUser.AssertPassesThroughMemoryPackSerializer<Types.OldUser, User>(AssertEqual, Out);
            newUser.Claims.Count.Should().Be(0);
            newUser.Identities.Count.Should().Be(0);

            oldUser = new Types.OldUser("b", "bob") { Version = 3 }
                .WithClaim("email1", "bob1@bob.bom")
                .WithClaim("email2", "bob2@bob.bom")
                .WithIdentity("google/1", "s")
                .WithIdentity("google/2", "q");
            newUser = oldUser.AssertPassesThroughMemoryPackSerializer<Types.OldUser, User>(AssertEqual, Out);
            newUser.Claims.Count.Should().Be(2);
            newUser.Identities.Count.Should().Be(2);

            // New to old
            newUser = new User("b", "bob");
            oldUser = newUser.AssertPassesThroughMemoryPackSerializer<User, Types.OldUser>(AssertEqual, Out);
            oldUser.Claims.Count.Should().Be(0);
            oldUser.Identities.Count.Should().Be(0);

            newUser = new User("b", "bob") { Version = 3 }
                .WithClaim("email1", "bob1@bob.bom")
                .WithClaim("email2", "bob2@bob.bom")
                .WithIdentity("google/1", "s")
                .WithIdentity("google/2", "q");
            oldUser = newUser.AssertPassesThroughMemoryPackSerializer<User, Types.OldUser>(AssertEqual, Out);
            oldUser.Claims.Count.Should().Be(2);
            oldUser.Identities.Count.Should().Be(2);
        }
        finally {
            StringAsSymbolMemoryPackFormatterAttribute.IsEnabled = false;
        }
    }

    [Fact]
    public void TestCommandSerialization()
    {
        var c = new Types.TestCommand<HasStringId>("1", new("2")).PassThroughAllSerializers();
        c.Id.Should().Be("1");
        c.Value!.Id.Should().Be("2");
    }

    [Fact]
    public void ScreenshotSerialization()
    {
        var s = new Screenshot {
            Width = 10,
            Height = 20,
            CapturedAt = Moment.Now,
            Image = [1, 2, 3],
        };
        var t = s.PassThroughAllSerializers();
        t.Width.Should().Be(s.Width);
        t.Height.Should().Be(s.Height);
        t.CapturedAt.Should().Be(s.CapturedAt);
        t.Image.Should().Equal(s.Image);
    }

    [Fact]
    public void DbUserSerialization()
    {
        var u = new DbUser { Id = 1, Name = "Alice", Email = "alice@test.com" };
        var t = u.PassThroughAllSerializers(Out);
        t.Id.Should().Be(u.Id);
        t.Name.Should().Be(u.Name);
        t.Email.Should().Be(u.Email);
    }

    [Fact]
    public void DbChatSerialization()
    {
        var c = new DbChat {
            Id = 42,
            Title = "Test Chat",
            Author = new DbUser { Id = 1, Name = "Alice", Email = "alice@test.com" },
        };
        var t = c.PassThroughAllSerializers(Out);
        t.Id.Should().Be(c.Id);
        t.Title.Should().Be(c.Title);
        t.Author.Id.Should().Be(c.Author.Id);
        t.Author.Name.Should().Be(c.Author.Name);
    }

    [Fact]
    public void DbMessageSerialization()
    {
        var m = new DbMessage {
            Id = 100,
            Date = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc),
            Text = "Hello, world!",
            Author = new DbUser { Id = 1, Name = "Alice", Email = "alice@test.com" },
            Chat = new DbChat {
                Id = 42,
                Title = "Test Chat",
                Author = new DbUser { Id = 1, Name = "Alice", Email = "alice@test.com" },
            },
        };
        var t = m.PassThroughAllSerializers(Out);
        t.Id.Should().Be(m.Id);
        t.Date.Should().Be(m.Date);
        t.Text.Should().Be(m.Text);
        t.Author.Name.Should().Be(m.Author.Name);
        t.Chat.Title.Should().Be(m.Chat.Title);
    }

    [Fact]
    public void ByteStringSerialization()
    {
        var s = new ByteString([1, 2, 3]);
        s.AssertPassesThroughAllSerializers();
    }

    [Fact]
    public void SessionAuthInfoSerialization()
    {
        var si = new SessionAuthInfo(new Session(RandomStringGenerator.Default.Next())) {
            UserId = RandomStringGenerator.Default.Next(),
            AuthenticatedIdentity = new UserIdentity("a", "b"),
            IsSignOutForced = true,
        };
        Test(si);

        void Test(SessionAuthInfo s) {
            var t = s.PassThroughAllSerializers();
            t.SessionHash.Should().Be(s.SessionHash);
            t.UserId.Should().Be(s.UserId);
            t.AuthenticatedIdentity.Should().Be(s.AuthenticatedIdentity);
            t.IsSignOutForced.Should().Be(s.IsSignOutForced);
        }
    }

    [Fact]
    public void SessionInfoSerialization()
    {
        var si = new SessionInfo(new Session(RandomStringGenerator.Default.Next())) {
            Version = 1,
            CreatedAt = Moment.Now,
            LastSeenAt = Moment.Now + TimeSpan.FromSeconds(1),
            UserId = RandomStringGenerator.Default.Next(),
            AuthenticatedIdentity = new UserIdentity("a", "b"),
            IPAddress = "1.1.1.1",
            UserAgent = "None",
            IsSignOutForced = true,
        };
        si.Options.Set((Symbol)"test");
        si.Options.Set(true);
        Test(si);

        void Test(SessionInfo s) {
            var t = s.PassThroughAllSerializers();
            t.SessionHash.Should().Be(s.SessionHash);
            t.Version.Should().Be(s.Version);
            t.CreatedAt.Should().Be(s.CreatedAt);
            t.LastSeenAt.Should().Be(s.LastSeenAt);
            t.IPAddress.Should().Be(s.IPAddress);
            t.UserAgent.Should().Be(s.UserAgent);
            t.AuthenticatedIdentity.Should().Be(s.AuthenticatedIdentity);
            t.IsSignOutForced.Should().Be(s.IsSignOutForced);
            AssertEqual(s.Options, t.Options);
        }
    }

    [Theory]
    [InlineData(typeof(MemoryPackByteSerializer))]
    [InlineData(typeof(MessagePackByteSerializer))]
    public void ByteSerializerStringSerialization(Type serializerType)
    {
        var serializer = (IByteSerializer)serializerType
            .GetProperty("Default", BindingFlags.Static | BindingFlags.Public)!
            .GetValue(null)!;
        var typedSerializer = serializer.ToTyped<string?>();

        for (var i = 1; i <= 1000; i++) {
            var s = i < 0 ? null : new string('\0', i);
            var bytes = new ByteString(typedSerializer.Write(s).WrittenMemory);
            // Out.WriteLine(bytes.ToHexString());
            // bytes.Bytes.Span[0].Should().NotBe(0);
        }
    }

    // Private methods

    private static User ToNewUser(Types.OldUser user)
        => new(user.Id, user.Name, user.Version, user.Claims.ToApiMap(), user.JsonCompatibleIdentities.ToApiMap());

    private static void AssertEqual(ImmutableOptionSet a, ImmutableOptionSet b)
    {
        b.Items.Count.Should().Be(a.Items.Count);
        foreach (var (key, item) in b.Items)
            item.Should().Be(a[key]);
    }

    private static void AssertEqual(User some, User expected)
    {
        some.Id.Should().Be(expected.Id);
        some.Name.Should().Be(expected.Name);
        some.Version.Should().Be(expected.Version);
        some.Claims.Should().BeEquivalentTo(expected.Claims);
        some.Identities.Should().BeEquivalentTo(expected.Identities);
    }

    private static void AssertEqual(User user, Types.OldUser expected)
        => AssertEqual(user, ToNewUser(expected));

    private static void AssertEqual(Types.OldUser user, User expected)
        => AssertEqual(ToNewUser(user), expected);
}
