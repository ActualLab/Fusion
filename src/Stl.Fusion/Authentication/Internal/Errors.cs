using System;
using System.Security.Authentication;

namespace Stl.Fusion.Authentication.Internal
{
    public static class Errors
    {
        public static Exception InvalidSessionId(string parameterName)
            => new ArgumentOutOfRangeException(parameterName, "Provided Session.Id is invalid.");

        public static Exception NoSessionProvided(string? parameterName = null)
            => new InvalidOperationException("No Session provided.");

        public static Exception ForcedSignOut()
            => new AuthenticationException("Sign-out was forced for this session.");
    }
}
