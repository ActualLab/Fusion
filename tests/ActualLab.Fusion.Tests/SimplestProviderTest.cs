using ActualLab.Fusion.Tests.Services;
using ActualLab.Resilience;

namespace ActualLab.Fusion.Tests;

public class SimplestProviderTest(ITestOutputHelper @out) : FusionTestBase(@out)
{
    [Fact]
    public async Task BasicTest()
    {
        var p = Services.GetRequiredService<ISimplestProvider>();
        var pImpl = (ISimpleProviderImpl)p;
        pImpl.SetValue("");
        var (gv, gcc) = (pImpl.GetValueCallCount, pImpl.GetCharCountCallCount);

        (await p.GetValue()).Should().Be("");
        (await p.GetCharCount()).Should().Be(0);
        pImpl.GetValueCallCount.Should().Be(++gv);
        pImpl.GetCharCountCallCount.Should().Be(++gcc);

        pImpl.SetValue("1");
        (await p.GetValue()).Should().Be("1");
        (await p.GetCharCount()).Should().Be(1);
        pImpl.GetValueCallCount.Should().Be(++gv);
        pImpl.GetCharCountCallCount.Should().Be(++gcc);

        // Retrying the same - call counts shouldn't change
        (await p.GetValue()).Should().Be("1");
        (await p.GetCharCount()).Should().Be(1);
        pImpl.GetValueCallCount.Should().Be(gv);
        pImpl.GetCharCountCallCount.Should().Be(gcc);
    }

    [Fact]
    public async Task ScopedTest()
    {
        var p = Services.GetRequiredService<ISimplestProvider>();
        var pImpl = (ISimpleProviderImpl)p;
        pImpl.SetValue("");
        var (gv, gcc) = (pImpl.GetValueCallCount, pImpl.GetCharCountCallCount);
        (await p.GetValue()).Should().Be("");
        (await p.GetCharCount()).Should().Be(0);
        pImpl.GetValueCallCount.Should().Be(++gv);
        pImpl.GetCharCountCallCount.Should().Be(++gcc);

        using (var s1 = Services.CreateScope()) {
            p = s1.ServiceProvider.GetRequiredService<ISimplestProvider>();
            pImpl = (ISimpleProviderImpl)p;
            (gv, gcc) = (pImpl.GetValueCallCount, pImpl.GetCharCountCallCount);
            (await p.GetValue()).Should().Be("");
            (await p.GetCharCount()).Should().Be(0);
            pImpl.GetValueCallCount.Should().Be(++gv);
            pImpl.GetCharCountCallCount.Should().Be(++gcc);
        }
        using (var s2 = Services.CreateScope()) {
            p = s2.ServiceProvider.GetRequiredService<ISimplestProvider>();
            pImpl = (ISimpleProviderImpl)p;
            (gv, gcc) = (pImpl.GetValueCallCount, pImpl.GetCharCountCallCount);
            (await p.GetValue()).Should().Be("");
            (await p.GetCharCount()).Should().Be(0);
            pImpl.GetValueCallCount.Should().Be(++gv);
            pImpl.GetCharCountCallCount.Should().Be(++gcc);
        }
    }

    [Fact]
    public async Task ExceptionCachingTest()
    {
        var p = Services.GetRequiredService<ISimplestProvider>();
        var pImpl = (ISimpleProviderImpl)p;
        pImpl.SetValue("");
        var (gv, gcc) = (pImpl.GetValueCallCount, pImpl.GetCharCountCallCount);

        pImpl.SetValue(null!); // Will cause an exception in GetCharCount
        (await p.GetValue()).Should().Be(null);
        pImpl.GetValueCallCount.Should().Be(++gv);
        pImpl.GetCharCountCallCount.Should().Be(gcc);

        await Assert.ThrowsAsync<TransientException>(() => p.GetCharCount());
        pImpl.GetValueCallCount.Should().Be(gv);
        pImpl.GetCharCountCallCount.Should().Be(++gcc);

        // Exceptions are also cached, so counts shouldn't change here
        await Assert.ThrowsAsync<TransientException>(() => p.GetCharCount());
        pImpl.GetValueCallCount.Should().Be(gv);
        pImpl.GetCharCountCallCount.Should().Be(gcc);

        // But if we wait for 0.3s+, it should recompute again
        await Task.Delay(1100);
        await Assert.ThrowsAsync<TransientException>(() => p.GetCharCount());
        pImpl.GetValueCallCount.Should().Be(gv);
        pImpl.GetCharCountCallCount.Should().Be(++gcc);
    }

    [Fact]
    public async Task ExceptionCaptureTest()
    {
        var p = Services.GetRequiredService<ISimplestProvider>();
        var pImpl = (ISimpleProviderImpl)p;
        pImpl.SetValue(null!); // Will cause an exception in GetCharCount
        var c1Opt = await Computed.TryCapture(() => p.GetCharCount());
        var c2 = await Computed.Capture(() => p.GetCharCount());
        c1Opt!.Error!.GetType().Should().Be(typeof(TransientException));
        c2.Should().BeSameAs(c1Opt.Value);
    }

    [Fact]
    public async Task OptionsTest()
    {
        var d = ComputedOptions.Default;
        var p = Services.GetRequiredService<ISimplestProvider>();
        var pImpl = (ISimpleProviderImpl)p;
        pImpl.SetValue("");

        var c1 = await Computed.Capture(() => p.GetValue());
        c1.Options.MinCacheDuration.Should().Be(TimeSpan.FromSeconds(10));
        c1.Options.TransientErrorInvalidationDelay.Should().Be(d.TransientErrorInvalidationDelay);
        c1.Options.AutoInvalidationDelay.Should().Be(d.AutoInvalidationDelay);

        var c2 = await Computed.Capture(() => p.GetCharCount());
        c2.Options.MinCacheDuration.Should().Be(TimeSpan.FromSeconds(0.5));
        c2.Options.TransientErrorInvalidationDelay.Should().Be(TimeSpan.FromSeconds(0.5));
        c2.Options.AutoInvalidationDelay.Should().Be(d.AutoInvalidationDelay);
    }

    [Fact]
    public async Task FailTest()
    {
        var d = ComputedOptions.Default;
        var p = Services.GetRequiredService<ISimplestProvider>();
        var pImpl = (ISimpleProviderImpl)p;
        pImpl.SetValue("");

        var c1 = await Computed.Capture(() => p.Fail(typeof(TransientException)));
        c1.Error.Should().BeOfType<TransientException>();
        await c1.WhenInvalidated().WaitAsync(TimeSpan.FromSeconds(1));

        c1 = await Computed.Capture(() => p.Fail(typeof(ServiceException)));
        c1.Error.Should().BeOfType<ServiceException>();
        await Delay(1);
        var c2 = await c1.Update();
        c2.Should().BeSameAs(c1);
    }

    [Fact]
    public async Task CommandTest()
    {
        var p = Services.GetRequiredService<ISimplestProvider>();
        var pImpl = (ISimpleProviderImpl)p;
        pImpl.SetValue("");

        (await p.GetValue()).Should().Be("");
        await Services.Commander().Run(new SetValueCommand() { Value = "1" });
        (await p.GetValue()).Should().Be("");
        await Services.Commander().Run(new SetValueCommand() { Value = "2" });
        (await p.GetValue()).Should().Be("");
    }
}
