namespace ActualLab.Tests.DependencyInjection;

public class ServiceProviderExtTest(ITestOutputHelper @out) : TestBase(@out)
{
    public class A(string x, string y)
    {
        public string X { get; } = x;
        public string Y { get; } = y;

        public A(string x) : this(x, string.Empty) { }
    }

    public class B
    {
        public string X { get; }
        public string Y { get; }

        public B(string x, string y)
        {
            X = x;
            Y = y;
        }

        [ServiceConstructor]
        public B(string x) : this(x, string.Empty) { }
    }

    public class C
    {
        public string X { get; }
        public string Y { get; }

        public C(int x, int y)
        {
            X = x.ToString();
            Y = y.ToString();
        }
    }

    [Fact]
    public void CreateInstanceTest()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton("S");
        var services = new DefaultServiceProviderFactory()
            .CreateServiceProvider(serviceCollection);

        var a = services.CreateInstance<A>();
        a.X.Should().Be("S");
        a.Y.Should().Be("S");

        var b = services.CreateInstance<B>();
        b.X.Should().Be("S");
        b.Y.Should().BeEmpty();

        ((Action) (() => {
            var c = services.CreateInstance<C>();
        })).Should().Throw<InvalidOperationException>();

        var c = services.CreateInstance<C>(1, 2);
        c.X.Should().Be("1");
        c.Y.Should().Be("2");
    }

    [Fact]
    public void IsDisposedOrDisposingTest()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        services.IsDisposedOrDisposing().Should().BeFalse();
        services.Dispose();
        services.IsDisposedOrDisposing().Should().BeTrue();
    }
}
