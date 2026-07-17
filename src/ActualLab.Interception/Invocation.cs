using ActualLab.Interception.Internal;

namespace ActualLab.Interception;

/// <summary>
/// Describes a single intercepted method invocation, including the proxy,
/// the table-qualified method slot, arguments, and the delegate to the original implementation.
/// </summary>
public readonly struct Invocation
{
    public readonly object Proxy;
    public readonly ProxyMethodTable MethodTable;
    public readonly int MethodIndex;
    public readonly ArgumentList Arguments;
    public readonly Delegate InterceptedDelegate;

    public MethodInfo Method {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MethodTable.GetMethodUnchecked(MethodIndex);
    }

    public ProxyMethodRef MethodRef {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(MethodTable, MethodIndex);
    }

    public object? InterfaceProxyTarget => (Proxy as InterfaceProxy)?.ProxyTarget;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Invocation(
        object proxy, ProxyMethodTable methodTable, int methodIndex,
        ArgumentList arguments, Delegate interceptedDelegate)
    {
        if ((uint)methodIndex >= (uint)methodTable.Methods.Length)
            throw new ArgumentOutOfRangeException(nameof(methodIndex));

        Proxy = proxy;
        MethodTable = methodTable;
        MethodIndex = methodIndex;
        Arguments = arguments;
        InterceptedDelegate = interceptedDelegate;
    }

    private Invocation(
        ProxyMethodTable methodTable, int methodIndex,
        object proxy, ArgumentList arguments, Delegate interceptedDelegate)
    {
        Proxy = proxy;
        MethodTable = methodTable;
        MethodIndex = methodIndex;
        Arguments = arguments;
        InterceptedDelegate = interceptedDelegate;
    }

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
        => new(MethodTable, MethodIndex, Proxy, arguments, InterceptedDelegate);
}
