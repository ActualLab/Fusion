using ActualLab.Interception.Internal;

namespace ActualLab.Interception;

public readonly record struct Invocation(
    object Proxy,
    MethodInfo Method,
    ArgumentList Arguments,
    Delegate InterceptedDelegate,
    object? Context = null)
{
    public object? ProxyTarget => (Proxy as InterfaceProxy)?.ProxyTarget;

    public override string ToString()
        => $"{nameof(Invocation)}({Proxy}, {Method.Name}, {Arguments})";

    public string Format()
        => $"{Proxy.GetType().NonProxyType().GetName()}.{Method.Name}{Arguments}";

    public void InterceptedVoid()
    {
        if (InterceptedDelegate is Action<ArgumentList> action)
            action.Invoke(Arguments);
        else
            throw Errors.InvalidInterceptedDelegate();
    }

    public TResult Intercepted<TResult>()
        => InterceptedDelegate is Func<ArgumentList, TResult> func
            ? func.Invoke(Arguments)
            : throw Errors.InvalidInterceptedDelegate();
};
