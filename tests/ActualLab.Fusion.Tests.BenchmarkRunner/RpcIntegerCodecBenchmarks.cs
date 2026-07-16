using ActualLab.Collections;
using ActualLab.Interception;
using BenchmarkDotNet.Attributes;

namespace ActualLab.Fusion.Tests.BenchmarkRunner;

public class RpcIntegerCodecBenchmarks : RpcBenchmarkBase
{
    private ArgumentList _arguments = null!;
    private ArrayPoolBuffer<byte> _buffer = null!;
    private ReadOnlyMemory<byte> _data;

    protected override void OnSetup()
    {
        _arguments = ArgumentList.New<long, CancellationToken>(16_384, default);
        _buffer = new ArrayPoolBuffer<byte>(16);
        Peer.ArgumentSerializer.Serialize(_arguments, false, _buffer);
        _data = _buffer.WrittenMemory.ToArray();
        _buffer.Reset();
    }

    protected override void OnCleanup()
        => _buffer.Dispose();

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public int SerializeLong()
    {
        var totalLength = 0;
        for (var i = 0; i < BenchmarkSettings.OperationCount; i++) {
            _buffer.Reset();
            Peer.ArgumentSerializer.Serialize(_arguments, false, _buffer);
            totalLength += _buffer.WrittenCount;
        }
        return totalLength;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public long DeserializeLong()
    {
        var arguments = _arguments;
        for (var i = 0; i < BenchmarkSettings.OperationCount; i++)
            Peer.ArgumentSerializer.Deserialize(ref arguments, false, _data);
        return arguments.Get<long>(0);
    }
}
