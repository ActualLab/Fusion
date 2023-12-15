using AutoFixture;
using AutoFixture.AutoMoq;

namespace ActualLab.Fusion.Tests.Server;

public abstract class BaseFixture<T>
{
    protected IFixture Fixture { get; }

    protected BaseFixture()
    {
        Fixture = new Fixture()
            .Customize(new AutoMoqCustomization {ConfigureMembers = true});
    }

    public T Create() => Fixture.Create<T>();
}
