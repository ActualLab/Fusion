using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using ActualLab.Internal;

namespace ActualLab.Rpc.WebSockets;

public class WebSocketOwner(
    string name,
    WebSocket webSocket,
    IServiceProvider services
    ) : SafeAsyncDisposableBase
{
    [field: AllowNull, MaybeNull]
    protected ILogger Log => field ??= Services.LogFor(GetType());

    public IServiceProvider Services { get; } = services;
    public string Name { get; } = name;
    public WebSocket WebSocket { get; } = webSocket;
    public object? Handler { get; init; }
    public LogLevel LogLevel { get; init; } = LogLevel.Information;

    public virtual Task ConnectAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        if (WebSocket is not ClientWebSocket webSocket)
            throw Errors.MustBeAssignableTo<ClientWebSocket>(WebSocket.GetType());

        Log.IfEnabled(LogLevel)?.Log(LogLevel, "'{Name}': connecting to {Uri}", Name, uri);
#if NET7_0_OR_GREATER
        if (Handler is HttpMessageInvoker invoker)
            return webSocket.ConnectAsync(uri, invoker, cancellationToken);
        if (Handler is HttpMessageHandler handler)
            return webSocket.ConnectAsync(uri, new HttpMessageInvoker(handler), cancellationToken);
#endif
        return webSocket.ConnectAsync(uri, cancellationToken);
    }

    protected override async Task DisposeAsync(bool disposing)
    {
        if (!disposing)
            return;

        WebSocket.Dispose();
        if (Handler is IAsyncDisposable ad)
            await ad.DisposeAsync().ConfigureAwait(false);
        else if (Handler is IDisposable d)
            d.Dispose();
    }
}
