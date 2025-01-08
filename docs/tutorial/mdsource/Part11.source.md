# Part 11: Authentication in Fusion

**NOTE:** This part of Tutorial is slightly outdated - it "targets" pre-v6.1 versions of Fusion, but v6.1 brought
pretty dramatic changes across the board, and some of them impact Fusion authentication.
You can use the code provided here assuming you adjust it accordingly with [Part 13: Migration to Fusion 6.1+](./Part13.md).

We'll eventually update this part, of course.

## Fusion Session

One of the important elements in this authentication system is Fusion's own session. A session is essentially a string value, that is stored in HTTP only cookie. If the client sends this cookie with a request then we use the session specified there; if not, `SessionMiddleware` creates it. 

To enable Fusion session we need to call `UseFusionSession` inside the `Configure` method of the `Startup` class.
This adds `SessionMiddleware` to the request pipeline. The actual class contains a bit more logic, but the important parts for now are the following:

```cs
public async Task InvokeAsync(HttpContext httpContext, RequestDelegate next)
    {
        // Note that now it's slightly more complex due to
        // newly introduced multitenancy support in Fusion 3.x.
        // But you'll get the idea.

        var cookies = httpContext.Request.Cookies;
        var cookieName = Cookie.Name ?? "";
        cookies.TryGetValue(cookieName, out var sessionId);
        var session = string.IsNullOrEmpty(sessionId) ? null : new Session(sessionId);

        if (session == null) {
            session = SessionFactory.CreateSession();
            var responseCookies = httpContext.Response.Cookies;
            responseCookies.Append(cookieName, session.Id, Cookie.Build(httpContext));
        }
        SessionProvider.Session = session;
        await next(httpContext).ConfigureAwait(false);
    }
```

The `Session` class in itself is very simple, it stores a single `Symbol Id` value. `Symbol` is a struct storing a string with its cached `HashCode`, its only role is to speedup dictionary lookups when it's used. Besides that, `Session` overrides equality - they're compared by `Id`.

```cs
public sealed class Session : IHasId<Symbol>, IEquatable<Session>,
    IConvertibleTo<string>, IConvertibleTo<Symbol>
{
    public static Session Null { get; } = null!; // To gracefully bypass some nullability checks
    public static Session Default { get; } = new("~"); // We'll cover this later

    [DataMember(Order = 0)]
    public Symbol Id { get; }
    ...
}
```

When you call `fusion.AddAuthentication()`, a number of services registered in you dependency injection container, and the most crucial ones are:

```cs
Services.TryAddSingleton<ISessionFactory, SessionFactory>();
Services.TryAddScoped<ISessionProvider, SessionProvider>();
Services.TryAddTransient(c => (ISessionResolver) c.GetRequiredService<ISessionProvider>());
Services.TryAddTransient(c => c.GetRequiredService<ISessionProvider>().Session);
```

Here is what you need to know about these services:
- `ISessionFactory` generates new sessions; you may want to override it to e.g. make all of your sessions digitally signed
- `ISessionProvider` keeps track of the current session; it implements `ISessionResolver`
- `ISessionResolver` allows to get the current session
- And finally, `Session` is registered as a transient service as well - it's mapped to a session resolved by `ISessionResolver`: `c => c.GetRequiredService<ISessionProvider>().Session`.

We'll cover how they're used in Blazor apps later, for now let's just remember they exist.

# Authentication services in the backend application

`Session`'s role is quite similar to ASP.NET sessions - it allows to identify everything related to the current user. Technically it's up to you what to associate with it, but Fusion's built-in services address a single kind of this information: authentication info.

If the session is authenticated, it allows you to get the user information, claims associated with this user, etc.
On the server side the following Fusion services interact with authentication data.

- `InMemoryAuthService`
- `DbAuthService<...>`

They implement the same interfaces, so they can be used interchangeably - the only difference between them is where they store the data: in memory on in the database. `InMemoryAuthService` is there primarily for debugging or quick prototyping - you don't want to use it in the real app.

Speaking of interfaces, these services implement two of them:
[`IAuth`](https://github.com/ActualLab/Fusion/blob/master/src/ActualLab.Fusion.Ext.Contracts/Authentication/IAuth.cs) and [`IAuthBackend`](https://github.com/ActualLab/Fusion/blob/master/src/ActualLab.Fusion.Ext.Services/Authentication/IAuthBackend.cs). The first one is intended to be used on the client; the second one must be used on the server side.

The key difference is:
- `IAuth` allows to just read the data associated with the current session
- `IAuthBackend` allows to modify it and read the information about any user.

This, btw, is a recommended way for designing Fusion services:
- `IXxx` is your front-end, it gets `Session` as the very first parameter and provides only the data current user is allowed to access
- `IXxxBackend` doesn't require `Session` and allows to access everything.

When you add authentication, `InMemoryAuthService` is registered as `IAuth` and `IAuthBackend` implementation by default. In order to register the `DbAuthService` in the DI container, we need to call the `AddAuthentication` method in a similar way
to the following code snippet. 

The Operations Framework is also needed for any of these services -
hopefully you read [Part 10](./Part10.md), which covers it.

```cs
services.AddDbContextServices<FusionDbContext>(dbContext => {
    db.AddOperations(operations => {
        operations.ConfigureOperationLogReader(_ => new() {
            UnconditionalCheckPeriod = TimeSpan.FromSeconds(10).ToRandom(0.05),
        });
        operations.AddFileBasedOperationLogChangeTracking();
    });
    dbContext.AddAuthentication<long>();
});
```

Our `DbContext` needs to contain `DbSet`-s for the classes provided here as type parameters.
The `DbSessionInfo` and `DbUser` classes are very simple entities provided by Fusion for storing authentication data.

```cs
public class AppDbContext : DbContextBase
{
    // Authentication-related tables
    public DbSet<DbUser<long>> Users { get; protected set; } = null!;
    public DbSet<DbUserIdentity<long>> UserIdentities { get; protected set; } = null!;
    public DbSet<DbSessionInfo<long>> Sessions { get; protected set; } = null!;
    // Operations Framework's operation log
    public DbSet<DbOperation> Operations { get; protected set; } = null!;

    public AppDbContext(DbContextOptions options) : base(options) { }
}

```

And that's how these entity types look:

```cs
public class DbSessionInfo<TDbUserId> : IHasId<string>, IHasVersion<long>
{
    [Key] [StringLength(32)] public string Id { get; set; }
    [ConcurrencyCheck] public long Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public string IPAddress { get; set; }
    public string UserAgent { get; set; }
    public string AuthenticatedIdentity { get; set; }
    public TDbUserId UserId { get; set; }
    public bool IsSignOutForced { get; set; }
    public string OptionsJson { get; set; }
}
```

`DbSessionInfo` stores our sessions, and these sessions (if authenticated) can be associated with a `DbUser`

```cs
public class DbUser<TDbUserId> : IHasId<TDbUserId>, IHasVersion<long> where TDbUserId : notnull
{
    public DbUser();

    [Key] public TDbUserId Id { get; set; }
    [ConcurrencyCheck] public long Version { get; set; }
    [MinLength(3)] public string Name { get; set; }
    public string ClaimsJson { get; set; }
    public List<DbUserIdentity<TDbUserId>> Identities { get; }
    
    [JsonIgnore, NotMapped]
    public ImmutableDictionary<string, string> Claims { get; set; }
}
```

## Using session in Compute Services for authorization

Our Compute Services can receive a `Session` object that we can use to decide if we are authenticated or not and who the signed in user is:

```cs
[ComputeMethod]
public virtual async Task<List<OrderHeaderDto>> GetMyOrders(Session session, CancellationToken cancellationToken = default)
{
    // We assume that _auth is of IAuth type here.
    var sessionInfo = await _auth.GetSessionInfo(session, cancellationToken);
    // You can use any of such methods
    var user = await _authService.RequireUser(session, true, CancellationToken);

    await using var dbContext = CreateDbContext();

    if (user.IsAuthenticated && user.Claims.ContainsKey("read_orders")) {
        // Read orders
    }
```

`RequireUser` here calls `GetUser` and throws an error if the result of this call is `null`; `true` argument passed to it indicates it has to wrap `ArgumentNullException` into `ResultException`, which is viewed by Fusion as a "normal" result, so it won't auto-invalidate this result in 1 second (which by default happens for any other exception thrown from compute method - Fusion assumes any of such failures might be transient). You can read more about this behavior in [our Discord channel](https://discord.com/channels/729970863419424788/729971920476307554/995865256201027614).

`GetSessionInfo`, `GetUser` and all other `IAuth` and `IAuthBackend` methods are compute methods, which means that the result of `GetMyOrders` call will invalidate once you sign-in into the provided `session` or sign out - generally, whenever a change that impacts on their result happens.

## Synchronizing Fusion and ASP.NET Core authentication states

If you look at `IAuth` and `IAuthBackend` APIs, it's easy to conclude there is no authentication per se:
- `IAuth` allows to retrieve the authentication state - i.e. get `SessionInfo`, `User` and session options (key-value pairs represented as `ImmutableOptionSet`) associated with a `Session`
- `IAuthBackend`, on contrary, allows to set them.

So in fact, these APIs just maintain the authentication state. It's assumed that you authenticate users using something else, and use these services in "Fusion world" to access the authentication info. Since these are compute services, they'll ensure that compute services calling them will invalidate their results once authentication info changes.

The proposed way to sync the authentication state between ASP.NET Core and Fusion is to embed this logic into `Host.cshtml`, which is typically mapped to every unmapped route in Blazor apps, and simply propagate the authentication state from ASP.NET Core to Fusion right when it loads. We assume here that when user signs in or signs out, `Host.cshtml` gets loaded by the end of any of these flows, so it's the best place to sync.

The synchronization is done by the `ServerAuthHelper.UpdateAuthState` method. [`ServerAuthHelper`](https://github.com/ActualLab/Fusion/blob/master/src/ActualLab.Fusion.Server/Authentication/ServerAuthHelper.cs) is a built-in Fusion helper doing exactly what's described above. It compares the authentication state exposed by `IAuth` for the current `Session` vs the state exposed in `HttpContext` and states calls `IAuthBackend.SignIn()` / `IAuthBackend.SignOut` to sync it.

The following code snippet shows how you embed it into `Host.cshtml`:

```xml
@page "/"
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@namespace Templates.TodoApp.Host.Pages
@using ActualLab.Fusion.Blazor
@using ActualLab.Fusion.Server.Authentication
@using ActualLab.Fusion.Server.Endpoints
@using Templates.TodoApp.UI
@inject ServerAuthHelper ServerAuthHelper
@inject BlazorCircuitContext BlazorCircuitContext
@{
    await ServerAuthHelper.UpdateAuthState(HttpContext);
    var authSchemas = await ServerAuthHelper.GetSchemas(HttpContext);
    var sessionId = ServerAuthHelper.Session.Id.Value;
    var isBlazorServer = BlazorModeEndpoint.IsBlazorServer(HttpContext);
    var isCloseWindowRequest = ServerAuthHelper.IsCloseWindowRequest(HttpContext, out var closeWindowFlowName);
    Layout = null;
}
<head>
    // This part has to be somewhere in <head> section
    <script src="_content/ActualLab.Fusion.Blazor.Authentication/scripts/fusionAuth.js"></script>
    <script>
        window.FusionAuth.schemas = "@authSchemas";
    </script>
</head>
<body>
// And this part has to be somewhere in the beginning of <body> section
@if (isCloseWindowRequest) {
    <script>
        setTimeout(function () {
            window.close();
        }, 500)
    </script>
    <div class="alert alert-primary">
        @(closeWindowFlowName) completed, you can close this window.
    </div>
}
```

Notice that it assumes there is [`fusionAuth.js`](https://github.com/ActualLab/Fusion/blob/master/src/ActualLab.Fusion.Blazor.Authentication/wwwroot/scripts/fusionAuth.js) - a small script embedded into `ActualLab.Fusion.Blazor` assembly, which is responsible for opening authentication window or performing a redirect.

Besides that, you need to add a couple extras to your ASP.NET Core app service container configuration:

```cs
var fusion = services.AddFusion();
var fusionServer = fusion.AddWebServer();
var fusionAuth = fusion.AddAuthentication().AddServer(
    signInControllerOptionsFactory: _ => new() {
        // Set to the desired one
        DefaultSignInScheme = MicrosoftAccountDefaults.AuthenticationScheme, 
        SignInPropertiesBuilder = (_, properties) => {
            properties.IsPersistent = true;
        }
    },
    serverAuthHelperOptionsFactory: _ => new() {
        // These are the claims mapped to User.Name once a new
        // User is created on sign-in; if they absent or this list
        // is empty, ClaimsPrincipal.Identity.Name is used.
        NameClaimKeys = Array.Empty<string>(),
    });

// You need this only if you plan to use Blazor WASM
var fusionClient = fusion.AddRestEaseClient();
// Configure Fusion client here

// Configure ASP.NET Core authentication providers:
services.AddAuthentication(options => {
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
}).AddCookie(options => {
    // You can use whatever you prefer to store the authentication info
    // in ASP.NET Core, this specific example uses a cookie.
    options.LoginPath = "/signIn"; // Mapped to 
    options.LogoutPath = "/signOut";
    if (Env.IsDevelopment())
        options.Cookie.SecurePolicy = CookieSecurePolicy.None;
    // This controls the expiration time stored in the cookie itself
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
    // And this controls when the browser forgets the cookie
    options.Events.OnSigningIn = ctx => {
        ctx.CookieOptions.Expires = DateTimeOffset.UtcNow.AddDays(28);
        return Task.CompletedTask;
    };
}).AddGitHub(options => {
    // Again, this is just an example of using GitHub account
    // OAuth provider to authenticate. There is nothing specific
    // to Fusion in the code below.
    options.ClientId = "...";
    options.ClientSecret = "..."
    options.Scope.Add("read:user");
    options.Scope.Add("user:email");
    options.CorrelationCookie.SameSite = SameSiteMode.Lax;
});
```

Notice that we use `/signIn` and `/signOut` paths above - they're mapped to the Fusion's [`AuthController`](https://github.com/ActualLab/Fusion/blob/master/src/ActualLab.Fusion.Server/Controllers/AuthController.cs).

If you want to use some other logic for these actions, you can map them to similar actions in another controller & update the paths (+ set `window.FusionAuth.signInPath` and `window.FusionAuth.signInPath` in JS as well), or replace this controller. There is a handy helper for this: `services.AddFusion().AddServer().AddControllerFilter(...)`.

And finally, you need a bit of extras in app configuration:

```cs
// You need this only if you use Blazor WASM w/ Fusion client
app.UseWebSockets(new WebSocketOptions() {
    KeepAliveInterval = TimeSpan.FromSeconds(30),
});
app.UseFusionSession();

// Required by Blazor
app.UseBlazorFrameworkFiles(); 
// Required by Blazor + it serves embedded content, such as  `fusionAuth.js`
app.UseStaticFiles(); 

// Endpoints
app.UseRouting();
app.UseAuthentication();
app.UseEndpoints(endpoints => {
    endpoints.MapBlazorHub();
    endpoints.MapRpcWebSocketServer();
    endpoints.MapFusionAuth();
    endpoints.MapFusionBlazorMode();
    // endpoints.MapControllers();
    endpoints.MapFallbackToPage("/_Host"); // Maps every unmapped route to _Host.cshtml
});
```

## Using Fusion authentication in a Blazor WASM components

As you know, client-side Compute Service Clients have the same interface as their server-side Compute Service counterparts, so the client needs to pass the `Session` as a parameter for methods that require it. However the `Session` is stored in a http-only cookie, so the client can't read its value directly. This is intentional - since `Session` allows anyone to impersonate as a user associated with it, ideally we don't want it to be available on the client side.

Fusion uses so-called "default session" to make it work. Let's quote the beginning of `Session` class code again:

```cs
public sealed class Session : IHasId<Symbol>, IEquatable<Session>,
    IConvertibleTo<string>, IConvertibleTo<Symbol>
{
    public static Session Null { get; } = null!; // To gracefully bypass some nullability checks
    public static Session Default { get; } = new("~"); // Default session
    
    // ...
```

Default session is a specially named `Session` which is automatically substituted by `SessionModelBinder` to the one provided by `ISessionResolver`. In other words, if you pass `Session.Default` as an argument to some Compute Service client, it will get its true value on controller method invocation on the server side.

All of this means your Blazor WASM client doesn't need to know the actual `Session` value to work - all you need is to configure `ISessionResolver` there to return `Session.Default` as the current session.

And you want your Blazor components to work on Blazor Server, you need to use the right `Session`, which is available there.

Now, if you still remember the beginning of this document, there is a number of services managing `Session` in Fusion:

```cs
Services.TryAddScoped<ISessionProvider, SessionProvider>();
Services.TryAddTransient(c => (ISessionResolver) c.GetRequiredService<ISessionProvider>());
Services.TryAddTransient(c => c.GetRequiredService<ISessionProvider>().Session);
```

So all we need is to make `ISessionResolver` to resolve `Session.Default` on the Blazor WASM client. One of ways to do this is to use this `App.razor` (your root Blazor component):

```xml
@using ActualLab.OS
@implements IDisposable
@inject BlazorCircuitContext BlazorCircuitContext
@inject ISessionProvider SessionProvider

<CascadingAuthState>
    <Router AppAssembly="@typeof(Program).Assembly">
        <Found Context="routeData">
            <RouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)"/>
        </Found>
        <NotFound>
            <LayoutView Layout="@typeof(MainLayout)">
                <p>Sorry, there's nothing at this address.</p>
            </LayoutView>
        </NotFound>
    </Router>
</CascadingAuthState>

@code {
    private Theme Theme { get; } = new() { IsGradient = true, IsRounded = false };

    [Parameter]
    public string SessionId { get; set; } = Session.Default.Id;

    protected override void OnInitialized()
    {
        SessionProvider.Session = OSInfo.IsWebAssembly 
            ? Session.Default 
            : new Session(SessionId);
        if (!BlazorCircuitContext.IsPrerendering)
            BlazorCircuitContext.RootComponent = this;
    }

    public void Dispose()
        => BlazorCircuitContext.Dispose();
}
```

You can see that when this component is initialized, it sets `SessionProvider.Session` to the value it gets as a parameter &ndash; unless we're running Blazor WASM. In this case it sets it to `Session.Default`. Any attempt to resolve `Session` (either via `ISessionResolver`, or via service provider) will return this value.  

You may notice that `App.razor` wraps its content into [`CascadingAuthState`](https://github.com/ActualLab/Fusion/blob/master/src/ActualLab.Fusion.Blazor.Authentication/CascadingAuthState.razor), which makes Blazor authentication to work as expected as well by embedding its `ChildContent` into Blazor's `<CascadingAuthenticationState>`.

All of this implies you also need a bit special logic in `_Host.cshtml` to spawn `App.razor` on the server side:

```xml
<app id="app">
    @{
        using var prerendering = BlazorCircuitContext.Prerendering();
        var prerenderedApp = await Html.RenderComponentAsync<App>(
            isBlazorServer ? RenderMode.ServerPrerendered : RenderMode.WebAssemblyPrerendered,
            isBlazorServer ? new { SessionId = sessionId } : null);
    }
    @(prerenderedApp)
</app>
```

The most important part here is that we pass `new { SessionId = sessionId }` parameter to the `Html.RenderComponentAsync<App>(...)` call in case Blazor Server is used, and `null` instead.

This also explains why we use `BlazorCircuitContext` here - it's a handy helper embedded in Fusion allowing, in particular, to detect if Blazor circuit runs in prerendering mode.

Ok, now all preps are done, and we're ready to write our first Blazor component relying on `IAuth`:

```xml
@page "/myOrders"
@inherits ComputedStateComponent<List<OrderHeaderDto>>
@inject IOrderService OrderService
@inject IAuth Auth
@inject Session Session // We resolve the Session via DI container
@{
    var orders = State.Value;
}

// Rendering orders

@code {
    protected override async Task<List<OrderHeader>> ComputeState(CancellationToken cancellationToken)
    {
        var user = await Auth.RequireUser(Session, true, cancellationToken);
        var sessionInfo = await Auth.GetSessionInfo(Session, cancellationToken);

        if (!user.Claims.ContainsKey("required-claim"))
            return new List<OrderHeader>();

        return await OrderService.GetMyOrders(Session);
    }
}
```

Note that to make it work with Blazor WASM, you need a controller like this:

```cs
[Route("api/[controller]/[action]")]
[ApiController, UseDefaultSession] // <<< You need UseDefaultSession filter here!
public class OrderController : ControllerBase, IOrderService
{
    private readonly IOrderService _orderService;

    public OrderController(IOrderService orderService, ISessionResolver sessionResolver)
        => _orderService = orderService;

    [HttpGet, Publish]
    public async Task<List<OrderHeader>> GetMyOrders(Session session, CancellationToken cancellationToken = default)
        => await _orderService.GetMyOrders(Session, cancellationToken);
}
```

## Signing out

As you already know, Fusion's authentication state is synced once `_Host.cshtml` is requested. Since this happens on almost any request, typical sign-out flow implies:
- First, you run a regular sign-out by e.g. redirecting a browser to `~/signOut` page
- Second, you redirect the browser to some regular page, which loads `_Host.cshtml`.

Since Fusion auth state change instantly hits all the clients, you can do all of this in e.g. a separate window - this is enough to make sure every browser window that shares the same session gets signed out.

[`ClientAuthHelper`](https://github.com/ActualLab/Fusion/blob/master/src/ActualLab.Fusion.Blazor.Authentication/ClientAuthHelper.cs) is a helper embedded into `ActualLab.Fusion.Blazor` that helps to run these flows by triggering corresponding methods on `window.fusionAuth`.

This is how `Authentication.razor` page in `TodoApp` template uses it:

```xml
<Button Color="Color.Warning"
        @onclick="_ => ClientAuthHelper.SignOut()">Sign out</Button>
<Button Color="Color.Danger"
        @onclick="_ => ClientAuthHelper.SignOutEverywhere()">Sign out everywhere</Button>
```

And if you are curious, `SignOutEverywhere()`  signs out _every_ session of the current user. This is possible, since `IAuthBackend` actually has a method allowing to enumerate these sessions. Because... Why not?

## Creating your own registration/login system with ASP.NET Core Identity

You can use Fusion's authentication on top of ASP.NET Core Identity. If you follow this approach then you will need to synchronize the authentication state between the two frameworks, the following code shows a very a basic implementation of this.

Our `SignIn` methods needs to receive the username and password of the user that wants to sign in, and also the current Fusion session. Then we can check if the provided data is valid in the example we basically check if
- The user exists
- If the user is already signed in or not in Fusions authentication state
- Whether the password/email pair is correct

```cs
public async Task SignIn(Session session, EmailPasswordDto signInDto, CancellationToken cancellationToken)
{
    var user = await _userManager.FindByNameAsync(signInDto.Email);
    if (user is ApplicationUser) {
        var sessionInfo = await _authService.GetSessionInfo(session, cancellationToken);
        if (sessionInfo.IsAuthenticated)
            throw new InvalidOperationException("You are already signed in!");

        var signInResult = await _signInManager.CheckPasswordSignInAsync(user, signInDto.Password, lockoutOnFailure: false);
    }
}
```

If everything was correct then we can proceed with signing the user in. The basic idea here is that we store what the claims and roles of each user with the Identity framework, inside the database, and during the sign-in process we query these roles and claims from here using the `UserManager` service that Identity provides and we can create a `ClaimsPrincipal` from these values that we can pass to the Fusion SignIn method.

```cs
if (signInResult.Succeeded) {
    var claims = await _userManager.GetClaimsAsync(user);
    var roles = await _userManager.GetRolesAsync(user);
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    foreach (var role in roles)
        identity.AddClaim(new Claim(ClaimTypes.Role, role));

    identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
    identity.AddClaim(new Claim(ClaimTypes.Name, user.UserName));
    var principal = new ClaimsPrincipal(identity);

    var ipAddress = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
    var userAgent = _httpContextAccessor.HttpContext.Request.Headers.TryGetValue("User-Agent", out var userAgentValues)
                    ? userAgentValues.FirstOrDefault() ?? ""
                    : "";

    var mustUpdateSessionInfo =
        !StringComparer.Ordinal.Equals(sessionInfo.IPAddress, ipAddress)
        || !StringComparer.Ordinal.Equals(sessionInfo.UserAgent, userAgent);
    if (mustUpdateSessionInfo) {
        var setupSessionCommand = new SetupSessionCommand(session, ipAddress, userAgent);
        await _auth.SetupSession(setupSessionCommand, cancellationToken);
    }

    var fusionUser = new User(session.Id);
    var (newUser, authenticatedIdentity) = CreateFusionUser(fusionUser, principal, CookieAuthenticationDefaults.AuthenticationScheme);
    var signInCommand = new SignInCommand(session, newUser, authenticatedIdentity);
    signInCommand.IsServerSide = true;
    await _authBackend.SignIn(signInCommand, cancellationToken);
    }
    
    protected virtual (User User, UserIdentity AuthenticatedIdentity) CreateFusionUser(User user, ClaimsPrincipal httpUser, string schema)
    {
        var httpUserIdentityName = httpUser.Identity?.Name ?? "";
        var claims = httpUser.Claims.ToImmutableDictionary(c => c.Type, c => c.Value);
        var id = FirstClaimOrDefault(claims, IdClaimKeys) ?? httpUserIdentityName;
        var name = FirstClaimOrDefault(claims, NameClaimKeys) ?? httpUserIdentityName;
        var identity = new UserIdentity(schema, id);
        var identities = ImmutableDictionary<UserIdentity, string>.Empty.Add(identity, "");

        user = new User("", name) {
            Claims = claims,
            Identities = identities
        };
        return (user, identity);
    }

    protected static string? FirstClaimOrDefault(IReadOnlyDictionary<string, string> claims, string[] keys)
    {
        foreach (var key in keys)
            if (claims.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
                return value;
        return null;
    }
```

After calling Fusion's `IAuthBackend.SignIn()` the authentication state will be stored inside Fusion's storage and a cookie will also be created, so we can proceed as usual. 

One thing we need to be careful with is if we edit the roles/claims of a certain user inside Identity, we will need to invalidate this inside Fusion's storage or maybe even force the user to sign out, in order to keep the two frameworks in sync. To update the authentication state inside Fusion we can simply call `IAuthBackend.SignIn` with a newly constructed `ClaimsPrincipal` object containing the updated roles/claims.

#### [Part 12: ActualLab.Rpc in Fusion 6.1+ &raquo;](./Part12.md) | [Tutorial Home](./README.md)
