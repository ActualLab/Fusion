using ActualLab.Collections;
using ActualLab.Interception;
using BenchmarkDotNet.Attributes;

namespace ActualLab.Fusion.Tests.BenchmarkRunner;

public class RpcIntegerCodecBenchmarks : RpcBenchmarkBase
{
    private ArgumentList _longArguments = null!;
    private ArgumentList _stringArguments = null!;
    private ArgumentList _threeArguments = null!;
    private ArrayPoolBuffer<byte> _buffer = null!;
    private ReadOnlyMemory<byte> _longData;
    private ReadOnlyMemory<byte> _stringData;
    private ReadOnlyMemory<byte> _threeData;

    protected override void OnSetup()
    {
        _longArguments = ArgumentList.New<long, CancellationToken>(16_384, default);
        _stringArguments = ArgumentList.New<string, CancellationToken>("benchmark-key", default);
        _threeArguments = ArgumentList.New<string, long, string, CancellationToken>(
            "benchmark-prefix",
            16_384,
            "benchmark-suffix",
            default);
        _buffer = new ArrayPoolBuffer<byte>(128);
        _longData = SerializeOnce(_longArguments);
        _stringData = SerializeOnce(_stringArguments);
        _threeData = SerializeOnce(_threeArguments);
    }

    protected override void OnCleanup()
        => _buffer.Dispose();

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public int SerializeLong()
    {
        var totalLength = 0;
        for (var i = 0; i < BenchmarkSettings.OperationCount; i++) {
            _buffer.Reset();
            Peer.ArgumentSerializer.Serialize(_longArguments, false, _buffer);
            totalLength += _buffer.WrittenCount;
        }
        return totalLength;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public long DeserializeLong()
    {
        var arguments = _longArguments;
        for (var i = 0; i < BenchmarkSettings.OperationCount; i++)
            Peer.ArgumentSerializer.Deserialize(ref arguments, false, _longData);
        return arguments.Get<long>(0);
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public int SerializeString()
    {
        var totalLength = 0;
        for (var i = 0; i < BenchmarkSettings.OperationCount; i++) {
            _buffer.Reset();
            Peer.ArgumentSerializer.Serialize(_stringArguments, false, _buffer);
            totalLength += _buffer.WrittenCount;
        }
        return totalLength;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public string DeserializeString()
    {
        var arguments = _stringArguments;
        for (var i = 0; i < BenchmarkSettings.OperationCount; i++)
            Peer.ArgumentSerializer.Deserialize(ref arguments, false, _stringData);
        return arguments.Get<string>(0);
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public int SerializeStringLongString()
    {
        var totalLength = 0;
        for (var i = 0; i < BenchmarkSettings.OperationCount; i++) {
            _buffer.Reset();
            Peer.ArgumentSerializer.Serialize(_threeArguments, false, _buffer);
            totalLength += _buffer.WrittenCount;
        }
        return totalLength;
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public long DeserializeStringLongString()
    {
        var arguments = _threeArguments;
        for (var i = 0; i < BenchmarkSettings.OperationCount; i++)
            Peer.ArgumentSerializer.Deserialize(ref arguments, false, _threeData);
        return arguments.Get<long>(1);
    }

    private ReadOnlyMemory<byte> SerializeOnce(ArgumentList arguments)
    {
        _buffer.Reset();
        Peer.ArgumentSerializer.Serialize(arguments, false, _buffer);
        return _buffer.WrittenMemory.ToArray();
    }
}
