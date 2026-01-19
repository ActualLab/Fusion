# Authentication: Cheat Sheet

Quick reference for Fusion authentication.

## Get Current User

In compute service:

```cs
public class MyService : IMyService
{
    private IAuth Auth { get; }
    private ISessionResolver SessionResolver { get; }

    public MyService(IAuth auth, ISessionResolver sessionResolver)
    {
        Auth = auth;
        SessionResolver = sessionResolver;
    }

    [ComputeMethod]
    public virtual async Task<MyData> GetUserData(CancellationToken cancellationToken)
    {
        var session = SessionResolver.Session;
        var user = await Auth.GetUser(session, cancellationToken);

        if (!user.IsAuthenticated)
            throw new SecurityException("Not authenticated");

        return await GetDataForUser(user.Id, cancellationToken);
    }
}
```

## Sign In / Sign Out

Sign in:

```cs
var command = new AuthBackend_SignIn(session, user, authenticatedIdentity);
await Commander.Call(command, cancellationToken);
```

Sign out:

```cs
var command = new Auth_SignOut(session);
await Commander.Call(command, cancellationToken);
```

## Blazor Authentication

Require authentication:

```razor
@inherits ComputedStateComponent<User>
@inject IAuth Auth
@inject Session Session

@if (!State.HasValue) {
    <Loading />
} else if (!State.Value.IsAuthenticated) {
    <RedirectToLogin />
} else {
    <div>Welcome, @State.Value.Name!</div>
}

@code {
    protected override Task<User> ComputeState(CancellationToken cancellationToken)
        => Auth.GetUser(Session, cancellationToken);
}
```

## Permission Checks

```cs
[ComputeMethod]
public virtual async Task<bool> CanEditOrder(long orderId, CancellationToken ct)
{
    var session = SessionResolver.Session;
    var user = await Auth.GetUser(session, ct);

    if (!user.IsAuthenticated)
        return false;

    var order = await GetOrder(orderId, ct);
    return order?.UserId == long.Parse(user.Id);
}
```

## User Properties

```cs
var user = await Auth.GetUser(session, cancellationToken);

user.IsAuthenticated  // bool
user.Id               // string (user ID)
user.Name             // string (display name)
user.Claims           // ImmutableDictionary<string, string>
user.Identities       // User's auth identities
```

## Session Setup (Server)

```cs
// In Startup/Program.cs
var fusion = services.AddFusion();
var fusionAuth = fusion.AddAuthentication();
fusionAuth.AddServer(
    signInControllerSettingsFactory: _ => SignInControllerSettings.Default with {
        DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme,
    });
```

## Session Setup (Client)

```cs
var fusion = services.AddFusion();
var fusionAuth = fusion.AddAuthentication();
fusionAuth.AddClient();
```
