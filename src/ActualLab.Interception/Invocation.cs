using ActualLab.Interception.Internal;

namespace ActualLab.Interception;

[method: MethodImpl(MethodImplOptions.NoInlining)]
public readonly record struct Invocation(
    object Proxy,
    MethodInfo Method,
    ArgumentList Arguments,
    Delegate InterceptedDelegate,
    object? Context = null)
{
    public object? InterfaceProxyTarget => (Proxy as InterfaceProxy)?.ProxyTarget;

    public override string ToString()
        => $"{nameof(Invocation)}({Proxy}, {Method.Name}, {Arguments})";

    public string Format()
        => $"{Proxy.GetType().NonProxyType().GetName()}.{Method.Name}{Arguments}";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Intercepted()
    {
        if (InterceptedDelegate is Action<ArgumentList> action)
            action.Invoke(Arguments);
        else
            throw Errors.InvalidInterceptedDelegate();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult Intercepted<TResult>()
        => InterceptedDelegate is Func<ArgumentList, TResult> func
            ? func.Invoke(Arguments)
            : throw Errors.InvalidInterceptedDelegate();
};
