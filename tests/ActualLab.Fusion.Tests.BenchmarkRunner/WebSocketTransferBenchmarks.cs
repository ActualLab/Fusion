using System.Net.WebSockets;
using BenchmarkDotNet.Attributes;

namespace ActualLab.Fusion.Tests.BenchmarkRunner;

[MemoryDiagnoser]
public class WebSocketReadBenchmarks
{
    private readonly SimulatedReadWebSocket _webSocket = new();
    private readonly byte[] _buffer = new byte[512];

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public async ValueTask<int> ReadTaskResult()
    {
        var totalLength = 0;
        var buffer = new ArraySegment<byte>(_buffer);
        for (var i = 0; i < BenchmarkSettings.OperationCount; i++) {
            var result = await _webSocket.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);
            totalLength += result.Count;
        }
        return totalLength;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public async ValueTask<int> ReadValueTaskResult()
    {
        var totalLength = 0;
        var buffer = _buffer.AsMemory();
        for (var i = 0; i < BenchmarkSettings.OperationCount; i++) {
            var result = await _webSocket.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);
            totalLength += result.Count;
        }
        return totalLength;
    }
}

[MemoryDiagnoser]
public class WebSocketWriteBenchmarks
{
    private readonly SimulatedWriteWebSocket _webSocket = new();
    private readonly byte[] _buffer = new byte[256];

    [Params(false, true)]
    public bool CompleteSynchronously { get; set; }

    [GlobalSetup]
    public void Setup()
        => _webSocket.CompleteSynchronously = CompleteSynchronously;

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public async ValueTask<int> WriteAsTask()
    {
        var buffer = _buffer.AsMemory();
        for (var i = 0; i < BenchmarkSettings.OperationCount; i++) {
            var writeTask = _webSocket.SendAsync(
                buffer,
                WebSocketMessageType.Binary,
                endOfMessage: true,
                CancellationToken.None);
            await writeTask.AsTask().ConfigureAwait(false);
        }
        return _webSocket.WriteCount;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public async ValueTask<int> WriteValueTask()
    {
        var buffer = _buffer.AsMemory();
        for (var i = 0; i < BenchmarkSettings.OperationCount; i++)
            await _webSocket.SendAsync(
                    buffer,
                    WebSocketMessageType.Binary,
                    endOfMessage: true,
                    CancellationToken.None)
                .ConfigureAwait(false);
        return _webSocket.WriteCount;
    }
}

internal abstract class SimulatedWebSocket : WebSocket
{
    private WebSocketState _state = WebSocketState.Open;

    public override WebSocketCloseStatus? CloseStatus => null;
    public override string? CloseStatusDescription => null;
    public override WebSocketState State => _state;
    public override string? SubProtocol => null;

    public override void Abort()
        => _state = WebSocketState.Aborted;

    public override Task CloseAsync(
        WebSocketCloseStatus closeStatus,
        string? statusDescription,
        CancellationToken cancellationToken)
    {
        _state = WebSocketState.Closed;
        return Task.CompletedTask;
    }

    public override Task CloseOutputAsync(
        WebSocketCloseStatus closeStatus,
        string? statusDescription,
        CancellationToken cancellationToken)
        => CloseAsync(closeStatus, statusDescription, cancellationToken);

    public override void Dispose()
        => Abort();
}

internal sealed class SimulatedReadWebSocket : SimulatedWebSocket
{
    private const int PayloadSize = 256;

    public override Task<WebSocketReceiveResult> ReceiveAsync(
        ArraySegment<byte> buffer,
        CancellationToken cancellationToken)
    {
        buffer.AsSpan(0, PayloadSize).Fill(1);
        return Task.FromResult(new WebSocketReceiveResult(
            PayloadSize,
            WebSocketMessageType.Binary,
            endOfMessage: true));
    }

    public override ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        buffer.Span[..PayloadSize].Fill(1);
        return ValueTask.FromResult(new ValueWebSocketReceiveResult(
            PayloadSize,
            WebSocketMessageType.Binary,
            endOfMessage: true));
    }

    public override Task SendAsync(
        ArraySegment<byte> buffer,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    public override ValueTask SendAsync(
        ReadOnlyMemory<byte> buffer,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken)
        => ValueTask.CompletedTask;
}

internal sealed class SimulatedWriteWebSocket : SimulatedWebSocket
{
    private int _writeCount;

    public bool CompleteSynchronously { get; set; }
    public int WriteCount => Volatile.Read(ref _writeCount);

    public override Task<WebSocketReceiveResult> ReceiveAsync(
        ArraySegment<byte> buffer,
        CancellationToken cancellationToken)
        => Task.FromResult(new WebSocketReceiveResult(
            0,
            WebSocketMessageType.Close,
            endOfMessage: true));

    public override ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken)
        => ValueTask.FromResult(new ValueWebSocketReceiveResult(
            0,
            WebSocketMessageType.Close,
            endOfMessage: true));

    public override Task SendAsync(
        ArraySegment<byte> buffer,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken)
        => SendAsync(buffer.AsMemory(), messageType, endOfMessage, cancellationToken).AsTask();

    public override ValueTask SendAsync(
        ReadOnlyMemory<byte> buffer,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _writeCount);
        return CompleteSynchronously ? ValueTask.CompletedTask : CompleteAsync();
    }

    private static async ValueTask CompleteAsync()
        => await Task.Yield();
}
