# TodoApp Auth Refactoring: Remove ActualLab.Fusion.Ext.* Dependency

## Goal
Remove `ActualLab.Fusion.Ext.Contracts` and `ActualLab.Fusion.Ext.Services` (and `Blazor.Authentication`) dependencies from the TodoApp sample. Replace with local types, interfaces, and implementations.

## New Interface Design

### ISessionsBackend (backend, IBackendService)
- `[CommandHandler] SetupSession(Sessions_SetupSession command)` → `SessionInfo`
- `[CommandHandler] SignIn(Sessions_SignIn command)` → void (takes `Session` + `string UserId`)
- `[CommandHandler] SignOut(Sessions_SignOut command)` → void
- `[ComputeMethod] GetSessionInfo(Session session)` → `SessionInfo?`
- `UpdatePresence(Session session)` → void (calls SetupSession internally)
- `[ComputeMethod] GetUserSessions(string shard, string userId)` → internal compute method for invalidation

### IUsersBackend (backend, IBackendService)
- `[CommandHandler] Upsert(Users_Upsert command)` → `User`
- `[ComputeMethod] Get(string shard, string userId)` → `User?`

### IUsers (client-facing, IComputeService)
- `[ComputeMethod] GetOwn(Session session)` → `User?`
- `[ComputeMethod] ListOwnSessions(Session session)` → `ImmutableArray<SessionInfo>`

---

## Files to Create/Modify

### Phase 1: Local Contract Types (`Abstractions/`)

**1.1 `Abstractions/Auth/User.cs`** — NEW
- Copy from `src/ActualLab.Fusion.Ext.Contracts/Authentication/User.cs`
- Namespace: `Samples.TodoApp.Abstractions`
- Keep: Id, Name, Version, Claims, Identities, IsAuthenticated(), IsGuest(), ToClaimsPrincipal(), ToClientSideUser()
- Keep serialization attrs (DataContract, MemoryPackable, MessagePackObject)
- Add `OrGuest()` as instance method or extension method

**1.2 `Abstractions/Auth/UserIdentity.cs`** — NEW
- Copy from `src/ActualLab.Fusion.Ext.Contracts/Authentication/UserIdentity.cs`
- Keep as-is (readonly record struct with Schema/SchemaBoundId parsing)

**1.3 `Abstractions/Auth/SessionInfo.cs`** — NEW
- Copy from `src/ActualLab.Fusion.Ext.Contracts/Authentication/SessionInfo.cs`
- Keep: Version, CreatedAt, LastSeenAt, IPAddress, UserAgent, Options, SessionHash, AuthenticatedIdentity, UserId, IsSignOutForced
- Keep `ToAuthInfo()`, `IsAuthenticated()`, `MustBeAuthenticated`
- Merge `SessionAuthInfo` base class INTO `SessionInfo` (simplify: single type)

**1.4 `Abstractions/Auth/IUsers.cs`** — NEW
- Client-facing compute service interface
```csharp
public interface IUsers : IComputeService
{
    [ComputeMethod(MinCacheDuration = 10)]
    Task<User?> GetOwn(Session session, CancellationToken ct = default);
    [ComputeMethod]
    Task<ImmutableArray<SessionInfo>> ListOwnSessions(Session session, CancellationToken ct = default);
}
```

**1.5 `Abstractions/Auth/Commands.cs`** — NEW
- Command records: `Sessions_SetupSession`, `Sessions_SignIn`, `Sessions_SignOut`, `Users_Upsert`
- `Sessions_SignIn(Session, string UserId)` — simplified, just userId
- `Sessions_SignOut(Session, bool Force = false)` — no kick variants for now, or keep KickUserSessionHash/KickAllUserSessions if AuthenticationPage needs them
- `Users_Upsert(User User)` — general-purpose

**1.6 `Abstractions/Abstractions.csproj`** — MODIFY
- Remove: `<ProjectReference ... ActualLab.Fusion.Ext.Contracts ...>`

### Phase 2: DB Entities (`Services/Db/`)

**2.1 `Services/Db/DbSessionInfo.cs`** — NEW
- Concrete type (not generic): `class DbSessionInfo : IHasId<string>, IHasVersion<long>`
- Table `_Sessions`, same columns as `DbSessionInfo<string>` but `UserId` is `string?`

**2.2 `Services/Db/DbUser.cs`** — NEW
- Concrete type: `class DbUser : IHasId<string>, IHasVersion<long>`
- Table `Users`, same columns as `DbUser<string>`

**2.3 `Services/Db/DbUserIdentity.cs`** — NEW
- Concrete type: `class DbUserIdentity : IHasId<string>`
- Table `UserIdentities`, same columns as `DbUserIdentity<string>`

**2.4 `Services/Db/AppDbContext.cs`** — MODIFY
- Change `DbUser<string>` → `DbUser`
- Change `DbUserIdentity<string>` → `DbUserIdentity`
- Change `DbSessionInfo<string>` → `DbSessionInfo`
- Remove `using ActualLab.Fusion.Authentication.Services;`

### Phase 3: Backend Interfaces (`Services/Auth/`)

**3.1 `Services/Auth/ISessionsBackend.cs`** — NEW
```csharp
public interface ISessionsBackend : IComputeService, IBackendService
{
    [CommandHandler]
    Task<SessionInfo> SetupSession(Sessions_SetupSession command, CancellationToken ct = default);
    [CommandHandler]
    Task SignIn(Sessions_SignIn command, CancellationToken ct = default);
    [CommandHandler]
    Task SignOut(Sessions_SignOut command, CancellationToken ct = default);
    [ComputeMethod(MinCacheDuration = 10)]
    Task<SessionInfo?> GetSessionInfo(Session session, CancellationToken ct = default);
    Task UpdatePresence(Session session, CancellationToken ct = default);
}
```

**3.2 `Services/Auth/IUsersBackend.cs`** — NEW
```csharp
public interface IUsersBackend : IComputeService, IBackendService
{
    [CommandHandler]
    Task<User> Upsert(Users_Upsert command, CancellationToken ct = default);
    [ComputeMethod(MinCacheDuration = 10)]
    Task<User?> Get(string shard, string userId, CancellationToken ct = default);
}
```

### Phase 4: Backend Implementation (`Services/Auth/`)

**4.1 `Services/Auth/DbSessionsBackend.cs`** — NEW
- Extends `DbServiceBase<AppDbContext>`, implements `ISessionsBackend`
- Inlined EF queries (no separate repo classes):
  - GetOrCreateSession: query + create if null
  - UpsertSession: query + update/insert
  - ListByUser: query sessions by userId
- Invalidation logic from DbAuthService
- Uses `IDbShardResolver<AppDbContext>` and `IDbEntityResolver<string, DbSessionInfo>`

**4.2 `Services/Auth/DbUsersBackend.cs`** — NEW
- Extends `DbServiceBase<AppDbContext>`, implements `IUsersBackend`
- Inlined EF queries:
  - Get: query user + include identities
  - Upsert: create new or update existing
  - GetByUserIdentity: query identity table → load user
- Uses `IDbEntityResolver<string, DbUser>`

**4.3 `Services/Auth/AppUsers.cs`** — NEW
- Implements `IUsers`
- Constructor: `(ISessionsBackend sessions, IUsersBackend users)`
- `GetOwn`: calls `sessions.GetSessionInfo` → extract userId → `users.Get`
- `ListOwnSessions`: calls `GetOwn` → get userId → query sessions
- Also implements `ISessionValidator` for session validation

### Phase 5: ServerAuthHelper (`Services/Auth/`)

**5.1 `Services/Auth/ServerAuthHelper.cs`** — NEW
- Copy from `src/ActualLab.Fusion.Ext.Services/Authentication/ServerAuthHelper.cs`
- Adjust to use `IUsers`, `ISessionsBackend`, `IUsersBackend` instead of `IAuth`/`IAuthBackend`
- `UpdateAuthState`:
  1. Get SessionInfo via `IUsers` (or `ISessionsBackend.GetSessionInfo`)
  2. Setup session if needed (call `Sessions_SetupSession`)
  3. Get user via `IUsers.GetOwn`
  4. If HTTP is signed in but Fusion isn't: call `Users_Upsert` (create/update user from claims), then `Sessions_SignIn(session, userId)`
  5. If HTTP is signed out but Fusion is signed in: call `Sessions_SignOut`
  6. Update presence
- `GetSchemas`: same as original
- `CreateOrUpdateUser`: extract claims from HttpContext, call `Users_Upsert` command

### Phase 6: Blazor Auth Components (`UI/Services/`)

**6.1 `UI/Services/AuthStateProvider.cs`** — NEW
- Copy from `src/ActualLab.Fusion.Blazor.Authentication/AuthStateProvider.cs`
- Use `IUsers.GetOwn` instead of `IAuth.GetUser`
- Remove `IAuth.IsSignOutForced` check (simplify, or query session info)
- Local `AuthState` class (minimal)

**6.2 `UI/Services/ClientAuthHelper.cs`** — NEW
- Copy from `src/ActualLab.Fusion.Blazor.Authentication/ClientAuthHelper.cs`
- Use `IUsers` instead of `IAuth`
- Sign-out commands: `Sessions_SignOut` instead of `Auth_SignOut`
- Keep JS interop for schema discovery, sign-in popup, sign-out redirect

**6.3 `UI/Services/CascadingAuthState.razor`** — NEW
- Copy from `src/ActualLab.Fusion.Blazor.Authentication/CascadingAuthState.razor`
- Adjust `using` to local namespace
- PresenceReporter: inline as simple timer calling `ISessionsBackend.UpdatePresence` or skip

**6.4 Copy `fusionAuth.js`** → `UI/wwwroot/scripts/fusionAuth.js`
- JS interop for sign-in/sign-out popup windows

### Phase 7: Auth Endpoints (`Services/Auth/`)

**7.1 `Services/Auth/AuthEndpoints.cs`** — NEW
- Copy from `src/ActualLab.Fusion.Ext.Services/Authentication/Endpoints/AuthEndpoints.cs`
- Simple sign-in/sign-out HTTP handlers using ASP.NET Core authentication

**7.2 `Services/Auth/HttpContextExt.cs`** — NEW
- Copy `GetAuthenticationSchemas` and `GetRemoteIPAddress` from original

### Phase 8: Registration & Wiring (`Host/Program.cs`)

**8.1 `Host/Program.cs`** — MODIFY
- Remove: `fusion.AddDbAuthService<AppDbContext, string>()`
- Remove: `fusionServer.AddAuthEndpoints()`
- Remove: `fusionServer.ConfigureServerAuthHelper()`
- Remove: `fusionServer.ConfigureAuthEndpoint()`
- Remove: `fusion.AddClient<IAuth>()`, `fusion.AddClient<IAuthBackend>()`
- Remove: `fusion.Configure<IAuth>()...`, `fusion.Configure<IAuthBackend>()...`
- Remove: `app.MapFusionAuthEndpoints()`
- Add: Register `DbSessionsBackend` as `ISessionsBackend`
- Add: Register `DbUsersBackend` as `IUsersBackend`
- Add: Register `AppUsers` as `IUsers`
- Add: Register entity resolvers for `DbSessionInfo`, `DbUser`
- Add: Register entity converters (or inline conversion)
- Add: Register `ServerAuthHelper` (local)
- Add: Register `AuthEndpoints` (local)
- Add: Map auth endpoints
- Add: For `ApiServer` mode: `fusion.AddClient<ISessionsBackend>()`, `fusion.AddClient<IUsersBackend>()`, etc.
- Add: `fusion.AddBlazor().AddAuthentication()` → replace with local registration of AuthStateProvider

**8.2 `UI/ClientStartup.cs`** — MODIFY
- Remove: `fusion.AddAuthClient()` (was `fusion.AddClient<IAuth>()`)
- Remove: `fusion.AddBlazor().AddAuthentication().AddPresenceReporter()`
- Add: `fusion.AddClient<IUsers>()` (client-facing compute service)
- Add: Register local `AuthStateProvider`, `ClientAuthHelper`, `CascadingAuthState`

**8.3 `ConsoleClient/Program.cs`** — MODIFY
- Remove: `fusion.AddAuthClient()`
- Add: `fusion.AddClient<IUsers>()` (if auth is needed in console client)

### Phase 9: UI Component Updates

**9.1 `UI/Shared/SignInDropdown.razor`** — MODIFY
- `@inject IAuth Auth` → `@inject IUsers Users`
- `Auth.GetUser(Session, ct)` → `Users.GetOwn(Session, ct)`

**9.2 `UI/Shared/BarAccount.razor`** — MODIFY
- Same changes as SignInDropdown

**9.3 `UI/Pages/AuthenticationPage.razor`** — MODIFY
- `@inject IAuth Auth` → `@inject IUsers Users`
- `Auth.GetUser(Session, ct)` → `Users.GetOwn(Session, ct)`
- `Auth.GetUserSessions(Session, ct)` → `Users.ListOwnSessions(Session, ct)`

**9.4 `UI/App.razor`** — MODIFY
- Keep `<CascadingAuthState>` but now from local component
- Verify `using` statements

**9.5 `Host/Components/Pages/_HostPage.razor`** — MODIFY
- `ServerAuthHelper` is now local type
- Update `using ActualLab.Fusion.Authentication` → `using Samples.TodoApp.Services.Auth`
- Adjust fusionAuth.js path if needed

### Phase 10: Service Layer Updates

**10.1 `Services/TodoApi.cs`** — MODIFY
- `IAuth auth` → `IUsers users`
- `auth.GetUser(session, ct)` → `users.GetOwn(session, ct)`

### Phase 11: Project File Cleanup

**11.1 `Abstractions/Abstractions.csproj`** — MODIFY
- Remove: `<ProjectReference ... ActualLab.Fusion.Ext.Contracts ...>`

**11.2 `Services/Services.csproj`** — MODIFY
- Remove: `<ProjectReference ... ActualLab.Fusion.Ext.Services ...>`
- Keep: `ActualLab.Fusion.Server` (for `DbServiceBase`, `IBackendService`, etc.)

**11.3 `UI/UI.csproj`** — MODIFY
- Remove: `<ProjectReference ... ActualLab.Fusion.Blazor.Authentication ...>`

**11.4 `Host/Host.csproj`** — MODIFY
- Remove: `<ProjectReference ... ActualLab.Fusion.Ext.Services ...>`

---

## Key Design Decisions

1. **Inlined repo logic** — No separate `IDbSessionInfoRepo` / `IDbUserRepo` classes. All EF queries are inlined in `DbSessionsBackend` and `DbUsersBackend`.

2. **SessionInfo = single type** — Merge `SessionAuthInfo` into `SessionInfo` (no inheritance). Simplifies the contract.

3. **SignIn takes string UserId** — `Sessions_SignIn(Session, string UserId)` is much simpler than the original which takes a full User object. The `ServerAuthHelper` first upserts the user via `Users_Upsert`, gets back the Id, then calls `Sessions_SignIn`.

4. **IUsers is client-facing** — `GetOwn` and `ListOwnSessions` are the only RPC-exposed queries. Backend queries (`ISessionsBackend.GetSessionInfo`, `IUsersBackend.Get`) are internal.

5. **fusionAuth.js** — Copy the JS file into TodoApp's wwwroot since we drop the Blazor.Authentication package.

6. **SignOut simplification** — Keep `Sessions_SignOut` with `Force` and `KickAllUserSessions` flags to support the AuthenticationPage UI features (SignOut, SignOutEverywhere, Kick).

---

## Verification

1. `dotnet build ActualLab.Fusion.sln` — ensure no compilation errors
2. Run the TodoApp host: verify sign-in with GitHub works
3. Verify AuthenticationPage shows user info and sessions
4. Verify sign-out and sign-out-everywhere work
5. Verify TodoApi still creates per-user/global folders
6. Verify Blazor WASM client auth state syncs in real-time
7. Grep for any remaining `ActualLab.Fusion.Ext` or `ActualLab.Fusion.Authentication` references in TodoApp
