using System.Data;

namespace ActualLab.Fusion.Authentication.Services;

public static class DbAuthIsolationLevelSelector
{
    public static IsolationLevel IsolationLevel { get; set; } = IsolationLevel.ReadCommitted;

    public static IsolationLevel SelectIsolationLevel(CommandContext context)
        => context.UntypedCommand switch {
            AuthBackend_SignIn
                or Auth_SignOut
                or AuthBackend_SetupSession
                or Auth_SetSessionOptions => IsolationLevel,
            _ => IsolationLevel.Unspecified
        };
}
