using System.Data;

namespace ActualLab.Fusion.Authentication.Services;

public static class DbAuthIsolationLevelSelector
{
    public static IsolationLevel GetIsolationLevel(CommandContext commandContext)
    {
        var command = commandContext.UntypedCommand;
        switch (command) {
        case AuthBackend_SignIn:
        case Auth_SignOut:
        case AuthBackend_SetupSession:
        case Auth_SetSessionOptions:
            return IsolationLevel.RepeatableRead;
        }
        return IsolationLevel.Unspecified;
    }
}
