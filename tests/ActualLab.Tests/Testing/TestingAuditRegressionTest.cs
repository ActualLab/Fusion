#if !NETFRAMEWORK
using ActualLab.Testing.Web;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Hosting;
#endif

namespace ActualLab.Tests.Testing;

public class TestingAuditRegressionTest
{
    [Fact]
    public void EqualitySerializerOverloadShouldUseTheCompleteSerializerMatrix()
    {
        var equalityOutput = new CapturingOutput();
        var passThroughOutput = new CapturingOutput();

        _ = 1.AssertPassesThroughAllSerializers(equalityOutput);
        _ = 1.PassThroughAllSerializers(passThroughOutput);

        equalityOutput.Lines.Should().Equal(passThroughOutput.Lines);
    }

#if !NETFRAMEWORK
    [Fact]
    public async Task ServingCleanupShouldAwaitAndSurfaceHostDisposal()
    {
        var host = new DelayedDisposalHost();
        var webHost = new StubTestWebHost(host);
        var serving = await webHost.Serve();

        var disposalTask = serving.DisposeAsync().AsTask();
        await host.WhenDisposalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        disposalTask.IsCompleted.Should().BeFalse();

        host.WhenDisposalAllowed.TrySetResult(default);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => disposalTask);
        error.Message.Should().Be(DelayedDisposalHost.DisposalErrorMessage);
        host.IsSynchronousDisposeCalled.Should().BeFalse();
    }
#endif

#if NETFRAMEWORK
    [Fact]
    public void OwinDependencyScopesShouldOwnScopedServices()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedService>();
        using var serviceProvider = services.BuildServiceProvider();
        using var resolver = new DefaultDependencyResolver(serviceProvider);

        var rootService = resolver.GetService(typeof(ScopedService));
        var scope1 = resolver.BeginScope();
        var scope2 = resolver.BeginScope();
        var service1 = (ScopedService)scope1.GetService(typeof(ScopedService));
        var service2 = (ScopedService)scope2.GetService(typeof(ScopedService));

        service1.Should().NotBeSameAs(rootService);
        service2.Should().NotBeSameAs(rootService);
        service2.Should().NotBeSameAs(service1);
        scope1.Dispose();
        service1.IsDisposed.Should().BeTrue();
        service2.IsDisposed.Should().BeFalse();
        scope2.Dispose();
        service2.IsDisposed.Should().BeTrue();
    }
#endif

    private sealed class CapturingOutput : ITestOutputHelper
    {
        public List<string> Lines { get; } = [];

        public void WriteLine(string message)
            => Lines.Add(message);

        public void WriteLine(string format, params object[] args)
            => Lines.Add(string.Format(format, args));
    }

#if !NETFRAMEWORK
    private sealed class StubTestWebHost(IHost host) : TestWebHostBase
    {
        protected override IHost CreateHost()
            => host;
    }

    private sealed class DelayedDisposalHost : IHost, IAsyncDisposable
    {
        public const string DisposalErrorMessage = "Host disposal failed.";

        public TaskCompletionSource<Unit> WhenDisposalStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<Unit> WhenDisposalAllowed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsSynchronousDisposeCalled { get; private set; }
        public IServiceProvider Services { get; }

        public DelayedDisposalHost()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IServer, StubServer>();
            Services = services.BuildServiceProvider();
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Dispose()
        {
            IsSynchronousDisposeCalled = true;
            WhenDisposalStarted.TrySetResult(default);
            WhenDisposalAllowed.Task.GetAwaiter().GetResult();
            throw new InvalidOperationException(DisposalErrorMessage);
        }

        public async ValueTask DisposeAsync()
        {
            WhenDisposalStarted.TrySetResult(default);
            await WhenDisposalAllowed.Task.ConfigureAwait(false);
            throw new InvalidOperationException(DisposalErrorMessage);
        }
    }

    private sealed class StubServer : IServer
    {
        public IFeatureCollection Features { get; } = new FeatureCollection();

        public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
#if NET7_0_OR_GREATER
            where TContext : notnull
#endif
            => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;

        public void Dispose() { }
    }
#endif

#if NETFRAMEWORK
    private sealed class ScopedService : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
            => IsDisposed = true;
    }
#endif
}
