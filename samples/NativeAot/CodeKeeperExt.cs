using ActualLab.Fusion.Interception;
using ActualLab.Generators;
using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Serialization;
using ActualLab.Time;
using ActualLabProxies;

namespace Samples.NativeAot;

public static class CodeKeeperExt
{
    private static volatile int _isInvoked = 0;

    public static bool MustInvoke() // Always returns false, but compiler assumes it doesn't
    {
        if (_isInvoked != 0)
            return false;

        Interlocked.Exchange(ref _isInvoked, 1);
        return RandomShared.NextDouble() > 10; // Always false
    }

    public static void KeepCode()
    {
        if (!MustInvoke())
            return; // Always returns here

        ArgumentList.New<RpcNoWait>(default).KeepCode();
        ArgumentList.New<long, Guid>(default, default).KeepCode();
        ArgumentList.New<ExceptionInfo>(default).KeepCode();
        ArgumentList.New<long, ExceptionInfo>(default, default).KeepCode();
        ArgumentList.New<Moment>(default).KeepCode();
        ArgumentList.New<CancellationToken>(default).KeepCode();
        Use(new ITestServiceProxy());
        Use(new TestServiceProxy());
        var i = new ComputeServiceInterceptor(null!, null!);
        i.CodeTouch<Moment>();
        var m = new MethodDef(null!, null!);
        m.CodeTouch<Moment>();
        // Still doesn't work.
    }

    private static void Use(object o)
    {
        o.GetHashCode();
        o.GetType();
    }

    public static TArgumentList KeepCode<TArgumentList>(this TArgumentList x)
        where TArgumentList : ArgumentList
    {
        if (!MustInvoke())
            return x; // Always returns here

        x.Get<int>(0);
        x.Get<string>(0);
        return x;
    }
}
