using ActualLab.Fusion.EntityFramework;

namespace ActualLab.Fusion.Authentication;

public interface IAuthBackend : IComputeService
{
    // Commands
    [CommandHandler]
    Task SignIn(AuthBackend_SignIn command, CancellationToken cancellationToken = default);
    [CommandHandler]
    Task<SessionInfo> SetupSession(AuthBackend_SetupSession command, CancellationToken cancellationToken = default);
    [CommandHandler]
    Task SetOptions(AuthBackend_SetSessionOptions command, CancellationToken cancellationToken = default);

    // Queries
    [ComputeMethod(MinCacheDuration = 10)]
    Task<User?> GetUser(DbShard shard, Symbol userId, CancellationToken cancellationToken = default);
}
