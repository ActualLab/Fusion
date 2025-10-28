using ActualLab.Fusion.Tests.Model;
using ActualLab.Fusion.Tests.Services;

namespace ActualLab.Fusion.Tests;

public class UserProviderTest(ITestOutputHelper @out) : FusionTestBase(@out)
{
    protected override void ConfigureTestServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureTestServices(services, isClient);
        var fusion = services.AddFusion();
        if (!isClient) {
            fusion.AddService<ITimeService, TimeService>();
            fusion.AddService<IUserService, UserService>();
        }
        else {
            fusion.AddClient<ITimeService>();
            fusion.AddClient<IUserService>();
        }
    }

    [Fact]
    public async Task InvalidateEverythingTest()
    {
        var commander = Services.Commander();
        var users = Services.GetRequiredService<IUserService>();
        // We need at least 1 user to see count invalidation messages
        await commander.Call(new UserService_Add(new User() {
            Id = int.MaxValue,
            Name = "Chuck Norris",
        }));

        var u1 = await users.Get(int.MaxValue);
        var c1 = await Computed.Capture(() => users.Count());

        await users.Invalidate();

        var u2 = await users.Get(int.MaxValue);
        var c2 = await Computed.Capture(() => users.Count());

        u2.Should().NotBeSameAs(u1);
        u2!.Id.Should().Be(u1!.Id);
        u2.Name.Should().Be(u1.Name);

        c2.Should().NotBeSameAs(c1);
        c2!.Value.Should().Be(c1!.Value);
    }

    [Fact]
    public async Task InvalidationTest()
    {
        var commander = Services.Commander();
        var users = Services.GetRequiredService<IUserService>();
        // We need at least 1 user to see count invalidation messages
        await commander.Call(new UserService_Add(new User() {
            Id = int.MaxValue,
            Name = "Chuck Norris",
        }));
        var userCount = await users.Count();

        var u = new User() {
            Id = 1000,
            Name = "Bruce Lee"
        };
        // This delete won't do anything, since the user doesn't exist
        (await commander.Call(new UserService_Delete(u))).Should().BeFalse();
        // Thus count shouldn't change
        (await users.Count()).Should().Be(userCount);
        // But after this line the could should change
        await commander.Call(new UserService_Add(u));

        var u1 = await users.Get(u.Id);
        u1.Should().NotBeNull();
        u1.Should().NotBeSameAs(u); // Because it's fetched
        u1!.Id.Should().Be(u.Id);
        u1.Name.Should().Be(u.Name);
        (await users.Count()).Should().Be(++userCount);

        var u2 = await users.Get(u.Id);
        u2.Should().BeSameAs(u1);

        u = u with { Name = "Jackie Chan" };
        await commander.Call(new UserService_Update(u)); // u.Name change

        var u3 = await users.Get(u.Id);
        u3.Should().NotBeNull();
        u3.Should().NotBeSameAs(u2);
        u3!.Id.Should().Be(u.Id);
        u3.Name.Should().Be(u.Name);
        (await users.Count()).Should().Be(userCount);
    }

    [Fact]
    public async Task StandaloneComputedTest()
    {
        var commander = Services.Commander();
        var stateFactory = Services.StateFactory();
        var users = Services.GetRequiredService<IUserService>();
        var time = Services.GetRequiredService<ITimeService>();

        var u = new User() {
            Id = int.MaxValue,
            Name = "Chuck Norris",
        };
        await commander.Call(new UserService_Add(u));

        using var sText = stateFactory.NewComputed<string>(
            FixedDelayer.YieldUnsafe,
            async ct => {
                var norris = await users.Get(int.MaxValue, ct).ConfigureAwait(false);
                var now = await time.GetTime().ConfigureAwait(false);
                return $"@ {now:hh:mm:ss.fff}: {norris?.Name ?? "(none)"}";
            });
        await sText.Update();
        sText.Updated += (s, _) => Log?.LogInformation($"{s.Value}");

        for (var i = 1; i <= 10; i += 1) {
            u = u with { Name = $"Chuck Norris Lvl{i}" };
            await commander.Call(new UserService_Add(u, true));
            await Task.Delay(100);
        }

        var text = await sText.Use();
        text.Should().EndWith("Lvl10");
    }

    [Fact]
    public async Task SuppressTest()
    {
        var stateFactory = Services.StateFactory();
        var time = Services.GetRequiredService<ITimeService>();
        var count1 = 0;
        var count2 = 0;

#pragma warning disable 1998
        using var s1 = stateFactory.NewComputed(
            FixedDelayer.YieldUnsafe,
            async _ => count1++
        );
        await s1.Update();

        using var s2 = stateFactory.NewComputed(
            FixedDelayer.YieldUnsafe,
            async _ => count2++
        );
        await s2.Update();
#pragma warning restore 1998

        using var s12 = stateFactory.NewComputed<(int, int)>(
            FixedDelayer.YieldUnsafe,
            async ct => {
                var a = await s1.Use(ct);
                using var _ = Computed.BeginIsolation();
                var b = await s2.Use(ct);
                return (a, b);
            });
        await s12.Update();

        var v12a = await s12.Use();
        s1.Computed.Invalidate(); // Should increment c1 & impact c12
        var v12b = await s12.Use();
        v12b.Should().Be((v12a.Item1 + 1, v12a.Item2));
        s2.Computed.Invalidate(); // Should increment c2, but shouldn't impact c12
        var v12c = await s12.Use();
        v12c.Should().Be(v12b);
    }

    [Fact]
    public async Task MultiHostInvalidationTest()
    {
        var users = Services.GetRequiredService<IUserService>();
        await using var _ = await WebHost.Serve();
        var webUsers = WebServices.GetRequiredService<IUserService>();
        var syncTimeout = TimeSpan.FromSeconds(1);

        async Task PingPong(IUserService users1, IUserService users2, User user)
        {
            var count0 = await users1.Count();
            var cCount = await Computed.Capture(() => users2.Count());
            cCount = await cCount.When(x => x == count0).WaitAsync(syncTimeout);

            var commander = users1.GetCommander();
            await commander.Call(new UserService_Add(user));
            var count1 = count0 + 1;
            (await users1.Count()).Should().Be(count1);

            var cUser2 = await Computed.Capture(() => users2.Get(user.Id));
            cUser2 = await cUser2.When(x => x is not null).WaitAsync(syncTimeout);
            var user2 = cUser2.Value;
            user2.Should().NotBeNull();
            user2!.Id.Should().Be(user.Id);

            await cCount.When(x => x == count1).WaitAsync(syncTimeout);
        }

        for (var i = 0; i < 5; i++) {
            var id1 = i * 2;
            var id2 = id1 + 1;
            Out.WriteLine($"{i}: ping...");
            await PingPong(users, webUsers, new User() { Id = id1, Name = id1.ToString()});
            Out.WriteLine($"{i}: pong...");
            await PingPong(webUsers, users, new User() { Id = id2, Name = id2.ToString()});
            // await PingPong(webUsers, users, new User() { Id = id2, Name = id2.ToString()});
        }
    }
}
