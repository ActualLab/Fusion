using ActualLab.Interception;
using ActualLab.Reflection;
using ServiceProviderExt = ActualLab.DependencyInjection.ServiceProviderExt;

namespace ActualLab.Tests.Generators;

public class ProxyTest(ITestOutputHelper @out) : BenchmarkTestBase(@out)
{
    [Theory]
    [InlineData(100_000)]
    [InlineData(1000_000)]
    [InlineData(10_000_000)]
    public async Task BenchmarkAll(int iterationCount)
    {
        var interceptor = new TestInterceptor(new(), ServiceProviderExt.Empty) {
            MustInterceptSyncCalls = true,
            MustInterceptAsyncCalls = true,
        };
        var noProxy = new ClassProxy();
        var altProxy = new AltClassProxy(interceptor);
        var classProxy = (ClassProxy)Proxies.New(typeof(ClassProxy), interceptor);
        var interfaceProxy = (IInterfaceProxy)Proxies.New(typeof(IInterfaceProxy), interceptor, noProxy);

        classProxy.GetType().NonProxyType().Should().Be(typeof(ClassProxy));
        classProxy.IsInitialized.Should().BeTrue();
        interfaceProxy.GetType().NonProxyType().Should().Be(typeof(IInterfaceProxy));

        await RunOne("NoProxy.VoidMethod", opCount => {
            for (; opCount > 0; opCount--)
                noProxy.VoidMethod();
            return 0;
        });
        await RunOne("NoProxy.Method0", opCount => {
            for (; opCount > 0; opCount--)
                _ = noProxy.Method0();
            return 0;
        });
        await RunOne("NoProxy.Method1", opCount => {
            for (; opCount > 0; opCount--)
                _ = noProxy.Method1(default);
            return 0;
        });
        await RunOne("NoProxy.Method2", opCount => {
            for (; opCount > 0; opCount--)
                _ = noProxy.Method2(0, default);
            return 0;
        });

        await RunOne("ClassProxy.VoidMethod", opCount => {
            for (; opCount > 0; opCount--)
                classProxy.VoidMethod();
            return 0;
        });
        await RunOne("ClassProxy.Method0", opCount => {
            for (; opCount > 0; opCount--)
                _ = classProxy.Method0();
            return 0;
        });
        await RunOne("ClassProxy.Method1", opCount => {
            for (; opCount > 0; opCount--)
                _ = classProxy.Method1(default);
            return 0;
        });
        await RunOne("ClassProxy.Method2", opCount => {
            for (; opCount > 0; opCount--)
                _ = classProxy.Method2(0, default);
            return 0;
        });
        await RunOne("AltClassProxy.Method2", opCount => {
            for (; opCount > 0; opCount--)
                _ = altProxy.Method2(0, default);
            return 0;
        });

        await RunOne("InterfaceProxy.VoidMethod", opCount => {
            for (; opCount > 0; opCount--)
                interfaceProxy.VoidMethod();
            return 0;
        });
        await RunOne("InterfaceProxy.Method0", opCount => {
            for (; opCount > 0; opCount--)
                _ = interfaceProxy.Method0();
            return 0;
        });
        await RunOne("InterfaceProxy.Method1", opCount => {
            for (; opCount > 0; opCount--)
                _ = interfaceProxy.Method1(default);
            return 0;
        });
        await RunOne("InterfaceProxy.Method2", opCount => {
            for (; opCount > 0; opCount--)
                _ = interfaceProxy.Method2(0, default);
            return 0;
        });

        Task RunOne<T>(string title, Func<int, T> action)
            => Benchmark(title, iterationCount, c => action(c));
    }
}
