using ActualLab.Rpc;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Tests.Rpc;

public class TestRpcCallTracer(RpcMethodDef methodDef) : RpcCallTracer(methodDef)
{
    private long _enterCount;
    private long _exitCount;
    private long _errorCount;

    public long EnterCount => Interlocked.Read(ref _enterCount);
    public long ExitCount => Interlocked.Read(ref _exitCount);
    public long ErrorCount => Interlocked.Read(ref _errorCount);

    public override RpcInboundCallTrace? StartInboundTrace(RpcInboundCall call)
        => new InboundTrace(this);

    public override RpcOutboundCallTrace? StartOutboundTrace(RpcOutboundCall call)
        => null;

    // Nested types

    private sealed class InboundTrace : RpcInboundCallTrace
    {
        private readonly TestRpcCallTracer _tracer;

        public InboundTrace(TestRpcCallTracer tracer) : base(null)
        {
            _tracer = tracer;
            Interlocked.Increment(ref _tracer._enterCount);
        }

        public override void Complete(RpcInboundCall call)
        {
            Interlocked.Increment(ref _tracer._exitCount);
            var resultTask = call.ResultTask;
            if (resultTask is not null && !resultTask.IsCompletedSuccessfully)
                Interlocked.Increment(ref _tracer._errorCount);
        }
    }
}
