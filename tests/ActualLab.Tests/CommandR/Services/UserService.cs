using ActualLab.Fusion.EntityFramework;

namespace ActualLab.Tests.CommandR.Services;

public class UserService(IServiceProvider services) : DbServiceBase<TestDbContext>(services), ICommandService
{
    [CommandHandler]
    private async Task RecAddUsers(
        RecAddUsersCommand command,
        CommandContext context,
        CancellationToken cancellationToken)
    {
        CommandContext.GetCurrent().Should().Be(context);
        context.ExecutionState.Handlers.Length.Should().Be(6);

        var dbContext = await CreateCommandDbContext(cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);

        var anotherDbContext = await CreateCommandDbContext(cancellationToken).ConfigureAwait(false);
        await using var _2 = anotherDbContext.ConfigureAwait(false);

        dbContext.Should().NotBeNull();
        anotherDbContext.Should().NotBeNull();
        anotherDbContext.Should().NotBe(dbContext);

        Log.LogInformation("User count: {UserCount}", command.Users.Length);
        if (command.Users.Length == 0)
            return;

        await Services.Commander().Call(
                new RecAddUsersCommand() { Users = command.Users.Skip(1).ToArray() },
                cancellationToken)
            .ConfigureAwait(false);

        var user = command.Users[0];
        if (user.Id.IsNullOrEmpty())
            throw new InvalidOperationException("User.Id must be set.");
        await dbContext.Users.AddAsync(user, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
