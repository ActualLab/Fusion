using ActualLab.Fusion.Authentication;
using ActualLab.Reflection;
using ActualLab.Tests;

namespace ActualLab.Fusion.Tests.Serialization;

public class MovedTypeResolutionTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void BackendCommandsShouldResolveByTheirPreV16Names()
    {
        // arrange
        var cases = new (string OldName, Type Type)[] {
            ("ActualLab.Fusion.Authentication.AuthBackend_SignIn, ActualLab.Fusion.Ext.Services",
                typeof(AuthBackend_SignIn)),
            ("ActualLab.Fusion.Authentication.AuthBackend_SetupSession, ActualLab.Fusion.Ext.Services",
                typeof(AuthBackend_SetupSession)),
        };

        // act
        var resolved = cases.Select(c => TypeRef.Resolve(c.OldName)).ToList();

        // assert
        resolved.Should().Equal(cases.Select(c => c.Type));
    }
}
