using System.Diagnostics.CodeAnalysis;
using ActualLab.Trimming;

namespace ActualLab.Interception.Trimming;

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "CodeKeepers are used only to retain the code")]
public class MethodDefCodeKeeper : CodeKeeper
{
    public virtual void KeepCodeForResult<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>()
    {
        if (AlwaysTrue)
            return;

        // TResult, TUnwrapped, Result<...> code
        Keep<TResult>();
        Keep<TUnwrapped>();
        Keep<Result>();
        Keep<Result<TUnwrapped>>();
        Keep<Result<Task<TUnwrapped>>>();
        Keep<Result<ValueTask<TUnwrapped>>>();
        Keep<Result>(); // Untyped

        var methodDef = Keep<MethodDef>();
        methodDef.WrapResult<TUnwrapped>(default!);
        methodDef.WrapAsyncInvokerResult<TUnwrapped>(default!);
        methodDef.WrapResultOfAsyncMethod<TUnwrapped>(default!);
        methodDef.WrapAsyncInvokerResultOfAsyncMethod<TUnwrapped>(default!);
        methodDef.SelectAsyncInvoker<TUnwrapped>(null!);
        methodDef.GetCachedFunc<TUnwrapped>(null!);

        Keep<MethodDef.TargetAsyncInvokerFactory<TUnwrapped>>();
        Keep<MethodDef.InterceptorAsyncInvokerFactory<TUnwrapped>>();
        Keep<MethodDef.InterceptedAsyncInvokerFactory<TUnwrapped>>();
        Keep<MethodDef.TargetObjectAsyncInvokerFactory<TUnwrapped>>();
        Keep<MethodDef.InterceptorObjectAsyncInvokerFactory<TUnwrapped>>();
        Keep<MethodDef.InterceptedObjectAsyncInvokerFactory<TUnwrapped>>();
        Keep<MethodDef.UniversalAsyncResultConverterFactory<TUnwrapped>>();
    }
}
