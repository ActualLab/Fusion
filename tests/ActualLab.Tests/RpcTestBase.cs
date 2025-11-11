using System.Diagnostics.CodeAnalysis;
using ActualLab.Diagnostics;
using ActualLab.Locking;
using ActualLab.RestEase;
using ActualLab.Rpc;
using ActualLab.Rpc.Clients;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.WebSockets;
using ActualLab.Testing.Collections;
using ActualLab.Time.Testing;
using Xunit.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace ActualLab.Tests;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public abstract class RpcTestBase(ITestOutputHelper @out) : TestBase(@out)
{
    public static string DefaultSerializationFormat => "mempack4c";

    private static readonly AsyncLock InitializeLock = new(LockReentryMode.CheckedFail);
    protected static readonly RpcPeerRef ClientPeerRef = RpcPeerRef.GetDefaultPeerRef();
    protected static readonly RpcPeerRef BackendClientPeerRef = RpcPeerRef.GetDefaultPeerRef(true);

    private IServiceProvider? _services;
    private IServiceProvider? _clientServices;

    protected RpcPeerConnectionKind ConnectionKind { get; init; } = RpcPeerConnectionKind.Remote;
    protected RpcFrameDelayerFactory? RpcFrameDelayerFactory { get; set; } = () => FrameDelayers.Delay(1); // Just for testing
    protected string SerializationFormat { get; set; } = DefaultSerializationFormat;
    protected bool ExposeBackend { get; init; } = false;
    protected bool UseTestClock { get; init; }
    protected bool UseLogging { get; init; } = true;
    protected bool UseDebugLog { get; set; } = true;
    protected bool IsLogEnabled { get; init; } = true;

    public IServiceProvider Services => _services ??= CreateServices();
    public IServiceProvider ClientServices => _clientServices ??= CreateServices(true);
    public IServiceProvider WebServices => WebHost.Services;

    [field: AllowNull, MaybeNull]
    public RpcWebHost WebHost => field ??= Services.GetRequiredService<RpcWebHost>();
    public ILogger? Log => (field ??= Services.LogFor(GetType())).IfEnabled(LogLevel.Debug, IsLogEnabled);

    public override async Task InitializeAsync()
    {
        using var releaser = await InitializeLock.Lock().ConfigureAwait(false);
        releaser.MarkLockedLocally();
        await Services.HostedServices().Start();
    }

    public override async Task DisposeAsync()
    {
        if (_clientServices is IAsyncDisposable adcs)
            await adcs.DisposeAsync();
        if (_clientServices is IDisposable dcs)
            dcs.Dispose();

        try {
            var hostedServices = _services?.HostedServices();
            if (hostedServices is not null)
                await hostedServices.Stop();
        }
        catch {
            // Intended
        }

        if (_services is IAsyncDisposable ads)
            await ads.DisposeAsync();
        if (_services is IDisposable ds)
            ds.Dispose();
    }

    protected IServiceProvider CreateServices(bool isClient = false)
    {
        var services = (IServiceCollection)new ServiceCollection();
        ConfigureServices(services, isClient);
        ConfigureTestServices(services, isClient);
        return services.BuildServiceProvider();
    }

    protected virtual void ConfigureTestServices(IServiceCollection services, bool isClient)
    { }

    protected virtual void ConfigureServices(IServiceCollection services, bool isClient)
    {
        if (UseTestClock)
            services.AddSingleton(_ => new MomentClockSet(new TestClock()));

        services.AddSingleton(Out);

        // Logging
        if (UseLogging)
            services.AddLogging(logging => {
                var debugCategories = new List<string> {
                    "ActualLab.Rpc",
                    "ActualLab.Fusion",
                    // "ActualLab.Fusion.EntityFramework",
                    // "ActualLab.Fusion.EntityFramework.LogProcessing",
                    // "ActualLab.Fusion.EntityFramework.Operations",
                    "ActualLab.CommandR",
                    "ActualLab.Tests",
                    "ActualLab.Tests.Fusion",
                    // DbLoggerCategory.Database.Transaction.Name,
                    // DbLoggerCategory.Database.Connection.Name,
                    // DbLoggerCategory.Database.Command.Name,
                    // DbLoggerCategory.Query.Name,
                    // DbLoggerCategory.Update.Name,
                };

                bool LogFilter(string? category, LogLevel level)
                    => debugCategories.Any(x => category?.StartsWith(x) ?? false)
                        && level >= LogLevel.Debug;

                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Debug);
                logging.AddFilter(LogFilter);
                if (UseDebugLog)
                    logging.AddDebug();
                // XUnit logging requires weird setup b/c otherwise it filters out
                // everything below LogLevel.Information
                logging.AddProvider(
#pragma warning disable CS0618
                    new XunitTestOutputLoggerProvider(
                        new TestOutputHelperAccessor() { Output = Out },
                        LogFilter));
#pragma warning restore CS0618
            });

        var rpc = services.AddRpc();
        rpc.AddWebSocketClient(_ => RpcWebSocketClient.Options.Default with {
            HostUrlResolver = (_, _) => WebHost.ServerUri.ToString(),
        });
        services.AddSingleton<RpcCallRouterFactory>(
            _ => method => args => {
                if (method.Kind is RpcMethodKind.Command && Invalidation.IsActive)
                    return RpcPeerRef.Local; // Commands in invalidation mode must always execute locally

                return RpcPeerRef.GetDefaultPeerRef(ConnectionKind, method.IsBackend);
            });
        services.AddSingleton<RpcSerializationFormatResolver>(
            _ => new RpcSerializationFormatResolver(SerializationFormat, RpcSerializationFormat.All.ToArray()));
        services.AddSingleton<RpcWebSocketChannelOptionsProvider>(_ => {
            return (peer, _) => WebSocketChannel<RpcMessage>.Options.Default with {
                Serializer = peer.Hub.SerializationFormats.Get(peer.Ref).MessageSerializerFactory.Invoke(peer),
                FrameDelayerFactory = RpcFrameDelayerFactory,
            };
        });
        if (!isClient) {
            services.AddSingleton(_ => new RpcWebHost(services, GetType().Assembly) {
                ExposeBackend = ExposeBackend,
            });
        }
        else {
            var restEase = services.AddRestEase();
            restEase.ConfigureHttpClient((_, _, options) => {
                var apiUri = new Uri($"{WebHost.ServerUri}api/");
                options.HttpClientActions.Add(c => c.BaseAddress = apiUri);
            });
        }
    }
}
