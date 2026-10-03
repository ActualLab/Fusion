using ActualLab.CommandR.Operations;

namespace ActualLab.Tests.CommandR;

// The map a recorded ServiceCall is identified through: a call records the type a service is
// registered as, and applying it needs the implementation behind that registration.
public class ServiceTypeResolverTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void RegisterMapsBothWays()
    {
        var resolver = new ServiceTypeResolver();
        resolver.Register(typeof(IService), typeof(Service));

        resolver.TryResolveServiceType(typeof(Service)).Should().Be(typeof(IService));
        resolver.TryResolveImplementationType(typeof(IService)).Should().Be(typeof(Service));
    }

    [Fact]
    public void RegisterAliasMapsForwardOnly()
    {
        // The shape AddAuthService has: IAuth and IAuthBackend both point at one implementation,
        // but only one of them is the type that implementation's recorded calls are identified by
        var resolver = new ServiceTypeResolver();
        resolver.Register(typeof(IService), typeof(Service));
        resolver.RegisterAlias(typeof(IBackend), typeof(Service));

        resolver.TryResolveImplementationType(typeof(IBackend)).Should().Be(typeof(Service));
        // The alias must not steal the implementation's primary service type
        resolver.TryResolveServiceType(typeof(Service)).Should().Be(typeof(IService));
    }

    [Fact]
    public void UnknownTypesResolveToNull()
    {
        var resolver = new ServiceTypeResolver();

        resolver.TryResolveServiceType(typeof(Service)).Should().BeNull();
        resolver.TryResolveImplementationType(typeof(IService)).Should().BeNull();
    }

    [Fact]
    public void RegisteringAgainReplacesTheMapping()
    {
        var resolver = new ServiceTypeResolver();
        resolver.Register(typeof(IService), typeof(Service));
        resolver.Register(typeof(IService), typeof(OtherService));

        resolver.TryResolveImplementationType(typeof(IService)).Should().Be(typeof(OtherService));
        // The first implementation keeps its own mapping - only the forward one was overwritten
        resolver.TryResolveServiceType(typeof(Service)).Should().Be(typeof(IService));
        resolver.TryResolveServiceType(typeof(OtherService)).Should().Be(typeof(IService));
    }

    // Nested types

    public interface IService;
    public interface IBackend;
    public class Service : IService, IBackend;
    public class OtherService : IService;
}
