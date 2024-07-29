using ActualLab.Rpc;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Tests.Rpc;

public class TestRpcCallTracer(RpcMethodDef method) : RpcCallTracer(method)
{
    private long _enterCount;
    private long _exitCount;
    private long _errorCount;

    public long EnterCount => Interlocked.Read(ref _enterCount);
    public long ExitCount => Interlocked.Read(ref _exitCount);
    public long ErrorCount => Interlocked.Read(ref _errorCount);

    public override RpcInboundCallTrace? StartInboundTrace(RpcInboundCall call)
        => new Trace(this);

    // Nested types

    private sealed class Trace : RpcInboundCallTrace
    {
        private readonly TestRpcCallTracer _tracer;

        public Trace(TestRpcCallTracer tracer)
        {
            _tracer = tracer;
            Interlocked.Increment(ref _tracer._enterCount);
        }

        public override void Complete(RpcInboundCall call, double durationMs)
        {
            Interlocked.Increment(ref _tracer._exitCount);
            if (!call.UntypedResultTask.IsCompletedSuccessfully())
                Interlocked.Increment(ref _tracer._errorCount);
        }
    }
}
