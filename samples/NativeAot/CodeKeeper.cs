using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using ActualLab.CommandR.Interception;
using ActualLab.Fusion.Client.Interception;
using ActualLab.Fusion.Client.Internal;
using ActualLab.Fusion.Interception;
using ActualLab.Generators;
using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLabProxies;
using MemoryPack;

namespace Samples.NativeAot;

public static class CodeKeeper
{
    public static bool AlwaysTrue { get; }
        = !RandomShared.NextDouble().ToString("F1").Contains('@'); // Always true

    public static void UseEverything()
    {
        if (RuntimeCodegen.NativeMode == RuntimeCodegenMode.DynamicMethods)
            return;

        // IRpcSystemCalls
        UseRpcCall<TypeRef>();
        UseRpcCall<RpcNoWait, RpcHandshake>();
        UseRpcCall<byte[], int, Dictionary<int, byte[]>, CancellationToken>();
        UseRpcCall<RpcNoWait>();
        UseRpcCall<RpcNoWait, object>();
        UseRpcCall<RpcNoWait, ExceptionInfo>();
        UseRpcCall<Unit, string, string>();
        UseRpcCall<RpcNoWait, long[]>();
        UseRpcCall<RpcNoWait, long, Guid>();
        UseRpcCall<RpcNoWait, Guid>();
        UseRpcCall<RpcNoWait, long, object>();
        UseRpcCall<RpcNoWait, long, ExceptionInfo>();

        // ITestService
        Use<ITestServiceProxy>();
        Use<TestServiceProxy>();
        UseRpcCall<Moment, CancellationToken>();
        UseRpcCall<string, SayHelloCommand, CancellationToken>();
    }

    public static void UseRpcCall<TResult>()
        => UseRpcCallResult<TResult>();

    public static void UseRpcCall<TResult, T0>()
    {
        UseRpcCallResult<TResult>();
        UseRpcCallArgument<T0>();
        var l = ArgumentList.New<T0>(default!);
        UseArgumentList<T0>(l);
    }

    public static void UseRpcCall<TResult, T0, T1>()
    {
        UseRpcCall<TResult, T0>();
        UseRpcCallArgument<T1>();
        var l = ArgumentList.New<T0, T1>(default!, default!);
        UseArgumentList<T1>(l);
    }

    public static void UseRpcCall<TResult, T0, T1, T2>()
    {
        UseRpcCall<TResult, T0, T1>();
        UseRpcCallArgument<T2>();
        var l = ArgumentList.New<T0, T1, T2>(default!, default!, default!);
        UseArgumentList<T2>(l);
    }

    public static void UseRpcCall<TResult, T0, T1, T2, T3>()
    {
        UseRpcCall<TResult, T0, T1, T2>();
        UseRpcCallArgument<T3>();
        var l = ArgumentList.New<T0, T1, T2, T3>(default!, default!, default!, default!);
        UseArgumentList<T3>(l);
    }

    public static void UseRpcCall<TResult, T0, T1, T2, T3, T4>()
    {
        UseRpcCall<TResult, T0, T1, T2, T3>();
        UseRpcCallArgument<T4>();
        var l = ArgumentList.New<T0, T1, T2, T3, T4>(default!, default!, default!, default!, default!);
        UseArgumentList<T4>(l);
    }

    public static void UseRpcCall<TResult, T0, T1, T2, T3, T4, T5>()
    {
        UseRpcCall<TResult, T0, T1, T2, T3, T4>();
        UseRpcCallArgument<T5>();
        var l = ArgumentList.New<T0, T1, T2, T3, T4, T5>(default!, default!, default!, default!, default!, default!);
        UseArgumentList<T5>(l);
    }

    public static void UseRpcCall<TResult, T0, T1, T2, T3, T4, T5, T6>()
    {
        UseRpcCall<TResult, T0, T1, T2, T3, T4, T5>();
        UseRpcCallArgument<T6>();
        var l = ArgumentList.New<T0, T1, T2, T3, T4, T5, T6>(default!, default!, default!, default!, default!, default!, default!);
        UseArgumentList<T6>(l);
    }

    public static void UseRpcCall<TResult, T0, T1, T2, T3, T4, T5, T6, T7>()
    {
        UseRpcCall<TResult, T0, T1, T2, T3, T4, T5, T6>();
        UseRpcCallArgument<T7>();
        var l = ArgumentList.New<T0, T1, T2, T3, T4, T5, T6, T7>(default!, default!, default!, default!, default!, default!, default!, default!);
        UseArgumentList<T7>(l);
    }

    public static void UseRpcCall<TResult, T0, T1, T2, T3, T4, T5, T6, T7, T8>()
    {
        UseRpcCall<TResult, T0, T1, T2, T3, T4, T5, T6, T7>();
        UseRpcCallArgument<T8>();
        var l = ArgumentList.New<T0, T1, T2, T3, T4, T5, T6, T7, T8>(default!, default!, default!, default!, default!, default!, default!, default!, default!);
        UseArgumentList<T8>(l);
    }

    public static void UseRpcCallArgument<T>()
    {
        var l = ArgumentList.New<T>(default!);
        l.Get<T>(0);
        UseSerialiable<T>();
    }

    public static void UseRpcCallResult<T>()
    {
        // Key things
        UseSerialiable<T>();
        UseArgumentList<T>(ArgumentList.New<T>(default!));

        // MethodDef
        var m = EvalFn(() => new MethodDef(null!, null!));
        Eval(() => m.CodeTouch<T>());

        // Interceptors
        UseInterceptor<RpcNonRoutingInterceptor, T>();
        UseInterceptor<RpcRoutingInterceptor, T>();
        UseInterceptor<RpcSwitchInterceptor, T>();
        UseInterceptor<CommandServiceInterceptor, T>();
        UseInterceptor<ComputeServiceInterceptor, T>();
        UseInterceptor<RemoteComputeServiceInterceptor, T>();

        // RpcInbound/OutboundXxx
        var outboundContext = EvalFn(() => new RpcOutboundContext());
        var inboundContext = EvalFn(() => new RpcInboundContext(null!, null!, default));
        Eval(() => new RpcOutboundCall<T>(outboundContext));
        Eval(() => new RpcOutboundComputeCall<T>(outboundContext));
        Eval(() => new RpcInboundCall<T>(inboundContext, null!));
        Eval(() => new RpcInboundComputeCall<T>(inboundContext, null!));
    }

    public static void UseInterceptor<T, TResult>()
        where T : Interceptor
    {
        var i = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        Eval(() => i.CodeTouch<TResult>());
    }

    public static void UseSerialiable<T>()
    {
        Use<T>();
        Use<UniSerialized<T>>();
        Use<MemoryPackSerialized<T>>();
        Use<MemoryPackByteSerializer<T>>();
        Eval(() => MemoryPackSerializer.Deserialize<T>(ReadOnlySpan<byte>.Empty));
        Eval(() => MemoryPackSerializer.Deserialize<T>(ReadOnlySequence<byte>.Empty));
        Eval(() => MemoryPackSerializer.Serialize<T>(default));
    }

    public static void UseArgumentList<T>(ArgumentList list)
    {
        Eval(() => list.Get<T>(0));
        Eval(() => list.GetCancellationToken(0));
        Eval(() => list.SetCancellationToken(0, default));
    }

    public static object Use([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
        => EvalFn(() => RuntimeHelpers.GetUninitializedObject(type));

    public static T Use<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
        => EvalFn(() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T))) ?? default(T)!;

    public static void Eval(Action action)
    {
        try {
            action.Invoke();
        }
        catch {
            // Intended
        }
    }

    public static T EvalFn<T>(Func<T> func)
    {
        try {
            return func.Invoke();
        }
        catch {
            // Intended
        }
        return default!;
    }
}
