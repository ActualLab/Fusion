using System.Runtime.CompilerServices;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Infrastructure.ActualLabProxies;
using ActualLabProxies;

namespace Samples.NativeAot;

public static class ModuleCodeKeeper
{
    [ModuleInitializer]
    public static void KeepCode()
        => ActualLab.Trimming.CodeKeeper.AddAction(static () => {
            var c = ActualLab.Trimming.CodeKeeper.Get<ActualLab.Interception.Trimming.ProxyCodeKeeper>();

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
}
