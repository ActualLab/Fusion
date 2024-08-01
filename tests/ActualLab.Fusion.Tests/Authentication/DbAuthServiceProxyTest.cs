using ActualLab.Fusion.Authentication.Services;
using ActualLab.Fusion.Tests.Model;
using ActualLab.Interception;

namespace ActualLab.Fusion.Tests.Authentication;

public class DbAuthServiceProxyTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void OneArgTest()
    {
        var type = typeof(DbAuthService<TestDbContext>);
        Proxies.TryGetProxyType(type).Should().BeNull();
    }

    [Fact]
    public void ThreeArgTest()
    {
        var type = typeof(DbAuthService<TestDbContext, DbSessionInfo<string>, DbUser<string>, string>);
        var proxyType = Proxies.GetProxyType(type);
        proxyType.Should().BeAssignableTo(type);
    }
}
