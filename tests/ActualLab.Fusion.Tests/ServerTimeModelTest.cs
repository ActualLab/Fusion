using ActualLab.Fusion.Tests.Services;
using ActualLab.Fusion.Tests.UIModels;

namespace ActualLab.Fusion.Tests;

public class ServerTimeModelTest(ITestOutputHelper @out) : FusionTestBase(@out)
{
    protected override void ConfigureTestServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureTestServices(services, isClient);
        var fusion = services.AddFusion();
        if (!isClient)
            fusion.AddService<ITimeService, TimeService>();
        else
            fusion.AddClient<ITimeService>();
        services.AddSingleton<ComputedState<ServerTimeModel1>, ServerTimeModel1State>();
    }

    protected override void ConfigureServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureServices(services, isClient);
        if (isClient) {
            services.AddSingleton(c => c.StateFactory().NewComputed<ServerTimeModel2>(
                new() { InitialValue = new(default) },
                async (_, cancellationToken) => {
                    var client = c.GetRequiredService<ITimeService>();
                    var time = await client.GetTime(cancellationToken).ConfigureAwait(false);
                    return new ServerTimeModel2(time);
                }));
        }
    }

    [Fact]
    public async Task ServerTimeModelTest1()
    {
        await ResetClientServices();
        await using var serving = await WebHost.Serve();
        using var stm = ClientServices.GetRequiredService<ComputedState<ServerTimeModel1>>();

        var c = stm.Computed;
        c.IsConsistent().Should().BeFalse();
        c.Value.Time.Should().Be(default);

        Debug.WriteLine("0");
        await stm.Update();
        Debug.WriteLine("1");
        await c.UpdateUntyped();
        Debug.WriteLine("2");

        c = stm.Computed;
        c.IsConsistent().Should().BeTrue();
        (DateTime.Now - c.Value.Time).Should().BeLessThan(TimeSpan.FromSeconds(1));

        Debug.WriteLine("3");
        await Task.Delay(TimeSpan.FromSeconds(3));
        Debug.WriteLine("4");
        await stm.Update();
        Debug.WriteLine("5");
        await Task.Delay(300); // Let's just wait for the updates to happen
        Debug.WriteLine("6");
        c = stm.Computed;
        Debug.WriteLine("7");

        // c.IsConsistent.Should().BeTrue(); // Hard to be sure here
        var delta = DateTime.Now - c.Value.Time;
        Debug.WriteLine(delta.TotalSeconds);
        delta.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ServerTimeModelTest2()
    {
        await ResetClientServices();
        await using var serving = await WebHost.Serve();
        using var stm = ClientServices.GetRequiredService<ComputedState<ServerTimeModel2>>();

        var c = stm.Computed;
        c.IsConsistent().Should().BeFalse();
        c.Value.Time.Should().Be(default);

        Debug.WriteLine("0");
        await stm.Update();
        Debug.WriteLine("1");
        await c.UpdateUntyped();
        Debug.WriteLine("2");

        c = stm.Computed;
        c.IsConsistent().Should().BeTrue();
        (DateTime.Now - c.Value.Time).Should().BeLessThan(TimeSpan.FromSeconds(1));

        Debug.WriteLine("3");
        await Task.Delay(TimeSpan.FromSeconds(3));
        Debug.WriteLine("4");
        await stm.Update();
        Debug.WriteLine("5");
        await Task.Delay(300); // Let's just wait for the updates to happen
        Debug.WriteLine("6");
        c = stm.Computed;
        Debug.WriteLine("7");

        // c.IsConsistent.Should().BeTrue(); // Hard to be sure here
        var delta = DateTime.Now - c.Value.Time;
        Debug.WriteLine(delta.TotalSeconds);
        delta.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }
}
