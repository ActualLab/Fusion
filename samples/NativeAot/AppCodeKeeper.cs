using ActualLab.Fusion.Trimming;
using ActualLab.Interception.Trimming;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Infrastructure.ActualLabProxies;
using ActualLab.Trimming;
using ActualLabProxies;

namespace Samples.NativeAot;

public static class AppCodeKeeper
{
    public static void KeepEverything()
    {
        if (RuntimeCodegen.NativeMode == RuntimeCodegenMode.DynamicMethods)
            return;

        CodeKeeper.AddAction(static () => {
            var c = CodeKeeper.Get<ProxyCodeKeeper>();

            // IRpcSystemCalls
            c.KeepProxy<IRpcSystemCalls, IRpcSystemCallsProxy>();
            c.KeepProxy<RpcSystemCalls, RpcSystemCallsProxy>();
            c.KeepAsyncMethod<TypeRef>();
            c.KeepAsyncMethod<RpcNoWait, RpcHandshake>();
            c.KeepAsyncMethod<byte[], int, Dictionary<int, byte[]>, CancellationToken>();
            c.KeepAsyncMethod<RpcNoWait>();
            c.KeepAsyncMethod<RpcNoWait, object>();
            c.KeepAsyncMethod<RpcNoWait, ExceptionInfo>();
            c.KeepAsyncMethod<Unit, string, string>();
            c.KeepAsyncMethod<RpcNoWait, long[]>();
            c.KeepAsyncMethod<RpcNoWait, long, Guid>();
            c.KeepAsyncMethod<RpcNoWait, Guid>();
            c.KeepAsyncMethod<RpcNoWait, long, object>();
            c.KeepAsyncMethod<RpcNoWait, long, ExceptionInfo>();

            // ITestService
            c.KeepProxy<ITestService, ITestServiceProxy>();
            c.KeepProxy<TestService, TestServiceProxy>();
            c.KeepAsyncMethod<Moment, CancellationToken>();
            c.KeepAsyncMethod<string, SayHelloCommand, CancellationToken>();
        });
        CodeKeeper.Set<ProxyCodeKeeper, FusionProxyCodeKeeper>();
        CodeKeeper.RunActions();
    }
}
