using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Tests.CommandR.Services;

namespace ActualLab.Tests.CommandR;

public class DbOperationTest : CommandRTestBase
{
    public DbOperationTest(ITestOutputHelper @out) : base(@out)
    {
        UseDbContext = true;
    }

    [Fact]
    public async Task RecAddUsersTest()
    {
        var services = CreateServices();
        var command = new RecAddUsersCommand() {
            Users = [
                new User() { Id = "a", Name = "Alice" },
                new User() { Id = "b", Name = "Bob" }
            ]
        };
        await services.Commander().Call(command);

        var f = services.GetRequiredService<IDbContextFactory<TestDbContext>>();
        // ReSharper disable once MethodHasAsyncOverload
        await using var dbContext = f.CreateDbContext().ReadWrite(false);
        (await dbContext.Users.AsQueryable().CountAsync()).Should().Be(2);
        // The command declares no invalidation and adds no events, so it needs no _Operations row -
        // only the _Events commit verifier every committed operation gets
        (await dbContext.Operations.AsQueryable().CountAsync()).Should().Be(0);
        (await dbContext.Events.AsQueryable().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RecAddUserFailTest()
    {
        var services = CreateServices();
        var command = new RecAddUsersCommand() {
            Users = [
                new User() { Id = "a", Name = "Alice" },
                new User() { Id = "", Name = "Fail" },
                new User() { Id = "b", Name = "Bob" }
            ]
        };
        await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            await services.Commander().Call(command);
        });

        var f = services.GetRequiredService<IDbContextFactory<TestDbContext>>();
        // ReSharper disable once MethodHasAsyncOverload
        await using var dbContext = f.CreateDbContext().ReadWrite(false);
        (await dbContext.Users.AsQueryable().CountAsync()).Should().Be(0);
        (await dbContext.Operations.AsQueryable().CountAsync()).Should().Be(0);
    }
}
