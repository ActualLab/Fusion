using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.Tests.Model;

namespace ActualLab.Fusion.Tests;

public class DbContextTest(ITestOutputHelper @out) : FusionTestBase(@out)
{
    [Fact]
    public async Task BasicTest()
    {
        await using var dbContext1 = CreateDbContext();
        var count = await dbContext1.Users.AsQueryable().CountAsync();
        count.Should().Be(0);

        var u1 = new User() {
            Id = 1,
            Name = "realDonaldTrump"
        };

        var c1 = new Chat() {
            Id = 2,
            Author = u1,
            Title = "Chinese Corona"
        };

        var m1 = new Message() {
            Id = 3,
            Text = "Covfefe",
            Author = u1,
            Chat = c1,
        };

        await dbContext1.AddRangeAsync(u1, c1, m1);
        await dbContext1.SaveChangesAsync();

        await using var dbContext2 = CreateDbContext();
        (await dbContext2.Users.AsQueryable().CountAsync()).Should().Be(1);
        (await dbContext2.Messages.AsQueryable().CountAsync()).Should().Be(1);
        u1 = await dbContext2.Users.FindAsync(u1.Id);
        u1!.Name.Should().Be("realDonaldTrump");

        m1 = await dbContext2.Messages.AsQueryable()
            .Where(p => p.Id == p.Id)
            .Include(p => p.Author)
            .SingleAsync();
        m1.Author.Id.Should().Be(u1.Id);
    }
}
