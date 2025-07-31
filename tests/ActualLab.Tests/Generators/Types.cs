using System.Reflection;
using ActualLab.Interception;
using ActualLab.Interception.Internal;

namespace ActualLab.Tests.Generators;

public interface ITestInterfaceBase : IRequiresFullProxy
{
    public Task Proxy1();
    public Task<int> Proxy2(int a, Task<bool> b);
}

public interface ITestInterface : ITestInterfaceBase
{
    public Task<int> Proxy3();
    public void Proxy4(int a, string b);
}

public class TestClassBase : IRequiresAsyncProxy
{
    public TestClassBase(int x) { }

    public virtual Task Proxy5() => Task.CompletedTask;
    public virtual Task<int> Proxy6(int a, string b) => Task.FromResult(a);

    // Must be ignored in proxy
    public virtual int NoProxy1(int a, string b) => 1; // Non-async
    public virtual Task<T> NoProxy2<T>(T argument) => throw new NotSupportedException(); // Generic
    private Task NoProxy3() => throw new NotSupportedException(); // Private
    [ProxyIgnore]
    public virtual Task<int> NoProxy4(int a) => Task.FromResult(a);
}

internal class TestClass(int x) : TestClassBase(x), ITestInterface
{
    public virtual Task Proxy1() => Task.CompletedTask;
    public virtual Task<int> Proxy2(int a, Task<bool> b) => Task.FromResult(1);
    public virtual Task<int> Proxy3() => Task.FromResult(0);
    public virtual void Proxy4(int a, string b) { }
    public virtual Task<Type> Proxy7(int a) => Task.FromResult(a.GetType());
    public string Proxy8(string x) => x;

    // Must be ignored in proxy
    public virtual Task<T> NoProxyA1<T>(T argument) => throw new NotSupportedException();
    public Task<T> NoProxyA2<T>(T argument) => throw new NotSupportedException();
    public override Task<int> NoProxy4(int a) => Task.FromResult(a);
}

public interface IInterfaceProxy : IRequiresFullProxy
{
    public void VoidMethod();
    public Task Method0();
    public Task Method1(CancellationToken cancellationToken);
    public Task Method2(int x, CancellationToken cancellationToken);
}

public class ClassProxy : IInterfaceProxy, INotifyInitialized
{
    public bool IsInitialized { get; private set; }

    public ClassProxy()
    {
        IsInitialized.Should().BeFalse();
    }

    public void Initialized()
    {
        IsInitialized = true;
    }

    public virtual void VoidMethod() { }
    public virtual Task Method0() => Task.CompletedTask;
    public virtual Task Method1(CancellationToken cancellationToken) => Task.CompletedTask;
    public virtual Task Method2(int x, CancellationToken cancellationToken) => Task.CompletedTask;
}

public class AltClassProxy
{
    private MethodInfo? _cachedMethodInfo;
    private Func<ArgumentList, Task>? _cachedIntercepted;
    private Func<Invocation, Task>? _cachedIntercept;
    private Interceptor _interceptor;

    public AltClassProxy(Interceptor interceptor)
    {
        _interceptor = interceptor;
        _cachedIntercept = _interceptor.Intercept<Task>;
        _cachedMethodInfo = ProxyHelper.GetMethodInfo(typeof(ClassProxy), "Method2", [typeof(int), typeof(CancellationToken)]);
    }

    public virtual Task Method2(int x, CancellationToken cancellationToken)
    {
        var intercepted = _cachedIntercepted ??= args =>
        {
            if (args is ArgumentListG2<int, CancellationToken> ga)
                return Method2Base(ga.Item0, ga.Item1);
            var sa = (ArgumentListS2)args;
            return Method2Base((int)sa.Item0!, (CancellationToken)sa.Item1!);
        };
        var invocation = new Invocation(this, _cachedMethodInfo!,
            ArgumentList.New(x, cancellationToken),
            intercepted);
        if (_cachedIntercept is null)
            throw new InvalidOperationException("No interceptor!.");
        return _cachedIntercept.Invoke(invocation);
    }

    public Task Method2Base(int x, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
