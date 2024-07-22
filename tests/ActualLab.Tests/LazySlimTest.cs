namespace ActualLab.Tests;

public class LazySlimTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void BasicTest()
    {
        var fn = JustOnce();
        var l = (ILazySlim<Unit>)LazySlim.New(fn);
        _ = l.Value;
        _ = l.Value;

        fn = JustOnce();
        // ReSharper disable once HeapView.CanAvoidClosure
        // ReSharper disable once AccessToModifiedClosure
        l = LazySlim.New(0, _ => fn.Invoke());
        _ = l.Value;
        _ = l.Value;

        fn = JustOnce();
        // ReSharper disable once HeapView.CanAvoidClosure
        l = LazySlim.New(0, 1, (_, _) => fn.Invoke());
        _ = l.Value;
        _ = l.Value;
    }

    private Func<Unit> JustOnce()
    {
        var count = 0;
        return () => {
            Interlocked.Increment(ref count).Should().Be(1);
            return default;
        };
    }
}
