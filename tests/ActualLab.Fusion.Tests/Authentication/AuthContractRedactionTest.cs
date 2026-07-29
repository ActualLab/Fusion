using System.Security.Claims;
using ActualLab.Compliance;
using ActualLab.Fusion.Authentication;

namespace ActualLab.Fusion.Tests.Authentication;

public class AuthContractRedactionTest
{
    private const string Secret = "ya29.a0Af-must-never-be-logged";
    private const string ClaimValue = "user@example.com-must-never-be-logged";

    [Fact]
    public void UserDoesNotPrintIdentitySecretsOrClaimValues()
    {
        var user = NewUser();
        // Sanitization is suspended by default so serializers see raw values;
        // SanitizingLogger opens exactly this scope around each log call
        using var _ = Sanitization.Begin();
        var text = user.ToString();

        text.Should().NotContain(Secret);
        text.Should().NotContain(ClaimValue);
        // The parts that make a log line useful survive
        text.Should().Contain("u-1").And.Contain("Alice");
        text.Should().Contain("Google"); // The identity's schema
        text.Should().Contain(ClaimTypes.Email); // The claim's name
    }

    [Fact]
    public void UserPrintsEverythingWhileSanitizationIsInactive()
    {
        // The default state - what a serializer or a plain Console.WriteLine sees
        var user = NewUser();

        var text = user.ToString();
        text.Should().Contain(Secret);
        text.Should().Contain(ClaimValue);
    }

    [Fact]
    public void SignInCommandDoesNotPrintTheUsersSecrets()
    {
        // The command that actually carries a whole User across the wire
        var command = new AuthBackend_SignIn(Session.Default, NewUser());

        using var _ = Sanitization.Begin();
        command.ToString().Should().NotContain(Secret);
        command.ToString().Should().NotContain(ClaimValue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SessionCommandsHideCallerSuppliedOptions(bool isActive)
    {
        var options = PropertyBag.Empty.Set("apiKey", Secret);
        var setup = new AuthBackend_SetupSession(Session.Default, "203.0.113.7", "Mozilla/5.0", options);
        var setOptions = new AuthBackend_SetSessionOptions(Session.Default, options);

        using var _ = isActive ? Sanitization.Begin() : Sanitization.Suspend();
        setup.ToString().Contains(Secret, StringComparison.Ordinal).Should().Be(!isActive);
        setOptions.ToString().Contains(Secret, StringComparison.Ordinal).Should().Be(!isActive);
        // IPAddress is diagnostic, not a credential - it stays readable either way
        setup.ToString().Should().Contain("203.0.113.7");
    }

    [Fact]
    public void SessionRedactsItselfInsideACommand()
    {
        var session = new Session("s-0123456789abcdefghij");
        var command = new AuthBackend_SetSessionOptions(session, PropertyBag.Empty);

        using var _ = Sanitization.Begin();
        command.ToString().Should().NotContain(session.Id);
        command.ToString().Should().Contain(session.ToString());
    }

    // Private methods

    private static User NewUser()
        => new User("u-1", "Alice")
            .WithClaim(ClaimTypes.Email, ClaimValue)
            .WithIdentity(new UserIdentity("Google", "12345"), Secret);
}
