using System.Data;

namespace ActualLab.Fusion.Authentication.Services;

/// <summary>
/// Selects the database isolation level for authentication-related commands.
/// </summary>
public static class DbAuthIsolationLevelSelector
{
    public static IsolationLevel IsolationLevel { get; set; } = IsolationLevel.ReadCommitted;

    public static IsolationLevel SelectIsolationLevel(CommandContext context)
        => context.UntypedCommand switch {
            AuthBackend_SignIn
                or Auth_SignOut
                or AuthBackend_SetupSession
                or AuthBackend_SetSessionOptions => IsolationLevel,
            _ => IsolationLevel.Unspecified
        };
}
