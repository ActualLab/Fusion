using Stl.Fusion.Tests.Services;
using Stl.Fusion.Tests.UIModels;

namespace Stl.Fusion.Tests;

public class KeyValueModelTest : FusionTestBase
{
    public KeyValueModelTest(ITestOutputHelper @out) : base(@out) { }

    [Fact]
    public async Task BasicTest()
    {
        await using var _ = await WebHost.Serve();

        var kv = WebServices.GetRequiredService<IKeyValueService<string>>();
        (await kv.TryGet("")).Should().Be(Option.None<string>());
        (await kv.Get("")).Should().BeNull();
        await kv.Set("", "1");
        (await kv.TryGet("")).Should().Be(Option.Some("1"));
        (await kv.Get("")).Should().Be("1");

        using var kvm = ClientServices.GetRequiredService<IComputedState<KeyValueModel<string>>>();
        var kvc = ClientServices.GetRequiredService<IKeyValueService<string>>();

        // First read
        var c = kvm.Computed;
        c.IsConsistent().Should().BeFalse();
        c.Value.Should().Be(null);

        await TestExt.WhenMet(() => {
            var snapshot = kvm.Snapshot;
            snapshot.Computed.HasValue.Should().BeTrue();
            var c = snapshot.Computed;
            c.IsConsistent().Should().BeTrue();
            c.Value.Key.Should().Be("");
            c.Value.Value.Should().Be("1");
            c.Value.UpdateCount.Should().Be(1);
        }, TimeSpan.FromSeconds(1));

        // Update
        await kvc.Set(kvm.Computed.Value.Key, "2");
        await Task.Delay(300);
        c = kvm.Computed;
        c.IsConsistent().Should().BeFalse();
        c.Value.Value.Should().Be("1");
        c.Value.UpdateCount.Should().Be(1);

        await Task.Delay(1000);
        c = kvm.Computed;
        c.IsConsistent().Should().BeTrue();
        c.Value.Value.Should().Be("2");
        c.Value.UpdateCount.Should().Be(2);
    }

    [Fact]
    public async Task CommandTest()
    {
        await using var _ = await WebHost.Serve();

        // Server commands
        var kv = WebServices.GetRequiredService<IKeyValueService<string>>();
        var commander = WebServices.Commander();
        (await kv.Get("")).Should().BeNull();

        await commander.Call(new KeyValueService_Set<string>("", "1"));
        (await kv.Get("")).Should().Be("1");

        await commander.Call(new KeyValueService_Set<string>("", "2"));
        (await kv.Get("")).Should().Be("2");

        // Client commands
        var clientCommander = ClientServices.Commander();
        var kvc = ClientServices.GetRequiredService<IKeyValueService<string>>();
        (await kv.Get("")).Should().Be("2");

        await clientCommander.Call(new KeyValueService_Set<string>("", "1"));
        await Task.Delay(100); // Remote invalidation takes some time
        (await kvc.Get("")).Should().Be("1");

        await clientCommander.Call(new KeyValueService_Set<string>("", "2"));
#if NETCOREAPP
        await Task.Delay(100); // Remote invalidation takes some time
#else
        await Task.Delay(250); // Remote invalidation takes some time
#endif
        (await kvc.Get("")).Should().Be("2");
    }

    [Fact]
    public async Task ExceptionTest()
    {
        await using var _ = await WebHost.Serve();
        var kv = WebServices.GetRequiredService<IKeyValueService<string>>();

        try {
            await kv.Get("error");
        }
        catch (ArgumentException ae) {
            ae.Message.Should().StartWith("Error!");
        }

        var kvc = ClientServices.GetRequiredService<IKeyValueService<string>>();
        try {
            await kvc.Get("error");
        }
        catch (ArgumentException ae) {
            ae.Message.Should().StartWith("Error!");
        }
    }

    [Fact]
    public async Task ClientExceptionTest()
    {
        await using var _ = await WebHost.Serve();
        var kv = ClientServices.GetRequiredService<IKeyValueService<string>>();

        try {
            await kv.Get("error");
        }
        catch (ArgumentException ae) {
            ae.Message.Should().StartWith("Error!");
        }

        var kvc = ClientServices.GetRequiredService<IKeyValueService<string>>();
        try {
            await kvc.Get("error");
        }
        catch (ArgumentException ae) {
            ae.Message.Should().StartWith("Error!");
        }
    }
}
