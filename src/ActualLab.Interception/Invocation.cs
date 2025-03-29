using ActualLab.Interception.Internal;

namespace ActualLab.Interception;

public readonly struct Invocation(
    object proxy,
    MethodInfo method,
    ArgumentList arguments,
    Delegate interceptedDelegate,
    object? context = null)
{
    public readonly object Proxy = proxy;
    public readonly MethodInfo Method = method;
    public readonly ArgumentList Arguments = arguments;
    public readonly Delegate InterceptedDelegate = interceptedDelegate;
    public readonly object? Context = context;
    public object? InterfaceProxyTarget => (Proxy as InterfaceProxy)?.ProxyTarget;

    public override string ToString()
        => $"{nameof(Invocation)}({Proxy}, {Method.Name}, {Arguments})";

    public string Format()
        => $"{Proxy.GetType().NonProxyType().GetName()}.{Method.Name}{Arguments}";

    public void InvokeIntercepted()
    {
        if (InterceptedDelegate is Action<ArgumentList> action)
            action.Invoke(Arguments);
        else
            throw Errors.InvalidInterceptedDelegate();
    }

    public TResult InvokeIntercepted<TResult>()
        => InterceptedDelegate is Func<ArgumentList, TResult> func
            ? func.Invoke(Arguments)
            : throw Errors.InvalidInterceptedDelegate();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Invocation With(ArgumentList arguments)
        => new(Proxy, Method, arguments, InterceptedDelegate, Context);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Invocation With(object? context)
        => new(Proxy, Method, Arguments, InterceptedDelegate, context);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Invocation With(ArgumentList arguments, object? context)
        => new(Proxy, Method, arguments, InterceptedDelegate, context);
}
