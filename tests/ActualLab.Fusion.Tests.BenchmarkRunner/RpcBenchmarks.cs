using ActualLab.Interception;
using ActualLab.Rpc.Infrastructure;
using BenchmarkDotNet.Attributes;

namespace ActualLab.Fusion.Tests.BenchmarkRunner;

public class RpcInboundCallBenchmarks : RpcBenchmarkBase
{
    private RpcInboundMessage[] _messages = null!;

    [IterationSetup]
    public void Prepare()
    {
        _messages = new RpcInboundMessage[BenchmarkSettings.OperationCount];
        for (var i = 0; i < _messages.Length; i++) {
            var arguments = ArgumentList.New<long, CancellationToken>(i, default);
            var argumentData = Serialize(arguments, GetMethodDef.HasPolymorphicArguments);
            _messages[i] = new RpcInboundMessage(
                GetMethodDef.CallType.Id,
                i + 1,
                GetMethodDef.Ref,
                argumentData,
                headers: null);
        }
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public RpcInboundContext? ProcessMessage_Long()
    {
        RpcInboundContext? result = null;
        for (var i = 0; i < _messages.Length; i++)
            result = Peer.Dispatch(_messages[i]);
        return result;
    }
}

public class RpcSystemOkBenchmarks : RpcBenchmarkBase
{
    private RpcInboundMessage[] _messages = null!;

    [IterationSetup]
    public void Prepare()
    {
        _messages = new RpcInboundMessage[BenchmarkSettings.OperationCount];
        for (var i = 0; i < _messages.Length; i++) {
            var call = PrepareOutboundCall(i);
            var arguments = ArgumentList.New((long)i);
            var argumentData = Serialize(arguments, needsPolymorphism: false);
            _messages[i] = new RpcInboundMessage(
                OkMethodDef.CallType.Id,
                call.Id,
                OkMethodDef.Ref,
                argumentData,
                headers: null);
        }
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public RpcInboundContext? ProcessMessage_SystemOk_Long()
    {
        RpcInboundContext? result = null;
        for (var i = 0; i < _messages.Length; i++)
            result = Peer.Dispatch(_messages[i]);
        return result;
    }
}

public class RpcOutboundCallBenchmarks : RpcBenchmarkBase
{
    private RpcOutboundCall[] _calls = null!;

    [IterationSetup]
    public void Prepare()
        => _calls = new RpcOutboundCall[BenchmarkSettings.OperationCount];

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.OperationCount)]
    public RpcOutboundMessage PrepareCallAndMessage()
    {
        RpcOutboundMessage result = null!;
        for (var i = 0; i < _calls.Length; i++) {
            var call = PrepareOutboundCall(i);
            _calls[i] = call;
            result = call.CreateOutboundMessage(
                call.Id,
                GetMethodDef.HasPolymorphicArguments,
                sendHandler: null);
        }
        return result;
    }

    [IterationCleanup]
    public void CompleteCalls()
    {
        for (var i = 0; i < _calls.Length; i++)
            _calls[i]?.SetResult((long)i, context: null);
    }
}
