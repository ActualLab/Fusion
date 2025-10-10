using ActualLab.CommandR.Operations;
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
        context.ExecutionState.Handlers.Length.Should().Be(7);

        var dbContext = await DbHub.CreateOperationDbContext(cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);

        var anotherDbContext = await DbHub.CreateOperationDbContext(cancellationToken).ConfigureAwait(false);
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
        var mustFail = user.Id.IsNullOrEmpty();
        context.Operation.AddCompletionHandler(scope => {
            var hasFailed = !scope.IsCommitted!.Value;
            Log.LogInformation("Completion handler: {Expected}, {Actual}", mustFail, hasFailed);
            hasFailed.Should().Be(mustFail);
            return Task.CompletedTask;
        });
        if (user.Id.IsNullOrEmpty())
            throw new InvalidOperationException("User.Id must be set.");

        await dbContext.Users.AddAsync(user, cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
