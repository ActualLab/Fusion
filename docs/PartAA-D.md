# Authentication: Diagrams

Diagrams illustrating Fusion's authentication architecture and flows.


## Authentication Service Architecture

```mermaid
flowchart TD
    subgraph Client ["Client&nbsp;(Blazor)"]
        direction LR
        CAH["ClientAuthHelper"] ~~~ ASP["AuthStateProvider"] ~~~ PR["PresenceReporter"]
        CAH --> IAuthC["IAuth (RPC Client)"]
        ASP --> IAuthC
        PR --> IAuthC
    end

    IAuthC -->|WebSocket RPC| IAuth

    subgraph Server
        direction TB
        SM["SessionMiddleware"] ~~~ SAH["ServerAuthHelper"] ~~~ AE["AuthEndpoint"]
        SM --> IAuth["IAuth / IAuthBackend"]
        SAH --> IAuth
        AE --> IAuth
        IAuth --> Impl["InMemoryAuthService | DbAuthService"]
        Impl --> DB["Database"]
    end

    subgraph DB ["Database"]
        direction LR
        Users ~~~ Sessions["_Sessions"] ~~~ UI["UserIdentities"]
    end
```


## IAuth vs IAuthBackend

| Aspect | `IAuth` (Client-Facing) | `IAuthBackend` (Server-Only) |
|--------|-------------------------|------------------------------|
| **Exposed via RPC** | Yes | No (`IBackendService`) |
| **Session required** | Yes (all queries) | No |

### IAuth Commands

| Command | Description |
|---------|-------------|
| `SignOut(session)` | Sign out current session |
| `EditUser(session, name)` | Edit current user |
| `UpdatePresence(session)` | Update last-seen |

### IAuth Queries

| Query | Description |
|-------|-------------|
| `GetUser(session)` | Get current user |
| `GetSessionInfo(session)` | Get session details |
| `GetAuthInfo(session)` | Get auth info |
| `IsSignOutForced(session)` | Check forced sign-out |
| `GetUserSessions(session)` | Get all user's sessions |

### IAuthBackend Commands

| Command | Description |
|---------|-------------|
| `SignIn(session, user, identity)` | Authenticate session |
| `SetupSession(session, ip, ua)` | Create/update session |
| `SetOptions(session, options)` | Set session options |

### IAuthBackend Queries

| Query | Description |
|-------|-------------|
| `GetUser(shard, userId)` | Get any user by ID (no session required) |


## Sign-In Flow

```mermaid
sequenceDiagram
    participant Browser
    participant AuthEndpoint
    participant OAuth as OAuth Provider
    participant SAH as ServerAuthHelper

    Browser->>AuthEndpoint: GET /signIn
    AuthEndpoint->>OAuth: Challenge
    OAuth-->>Browser: Redirect to OAuth
    Browser->>OAuth: User authenticates
    OAuth-->>Browser: Callback with tokens
    Browser->>AuthEndpoint: Redirect to app
    AuthEndpoint->>SAH: _HostPage loads
    SAH->>SAH: UpdateAuthState (syncs to IAuth)
    SAH-->>Browser: Page rendered (authenticated)
```


## Session Resolution Flow

```mermaid
flowchart TD
    Req["HTTP Request"] --> MW["SessionMiddleware"]

    MW --> Read["1. Read session cookie"]
    Read --> Exists{"2. Cookie exists?"}

    Exists -->|No| Create["Create new session"]
    Exists -->|Yes| CheckForced{"Check IsSignOutForced"}

    CheckForced -->|Yes| Create
    CheckForced -->|No| Use["Use existing session"]

    Create --> Tags["3. Apply tags (if TagProvider configured)"]
    Use --> Tags

    Tags --> Update["4. Update cookie (if AlwaysUpdateCookie = true)"]
    Update --> Set["5. Set SessionResolver.Session"]
    Set --> Next["Next Middleware"]
```


## Default Session Replacement

```mermaid
flowchart TD
    subgraph Client ["Blazor&nbsp;WASM&nbsp;Client"]
        SR["SessionResolver.Session = Session.Default (~)"]
        Call["await Auth.GetUser(Session.Default, ct)"]
        SR --> Call
    end

    Call -->|"RPC Call: GetUser(session: ~)"| Replacer

    subgraph Server
        Replacer["RpcDefaultSessionReplacer"]
        Check{"Session == ~ ?"}
        Replacer --> Check
        Check -->|Yes| Replace["Replace with SessionResolver.Session<br/>(real session from cookie)"]
        Check -->|No| Pass["Pass through unchanged"]
        Replace --> IAuth["IAuth Implementation"]
        Pass --> IAuth
    end
```


## Session Lifecycle

```mermaid
stateDiagram-v2
    direction LR
    [*] --> Anonymous: User visits site

    Anonymous: Anonymous Session
    Anonymous: Cookie set, session stored in DB

    Authenticated: Authenticated Session
    Authenticated: Session has UserId set

    Anonymous --> Authenticated: Sign In (OAuth)

    Authenticated --> Anonymous: Sign Out (normal)
    Authenticated --> Invalidated: Force Sign Out

    Invalidated: Invalidated Session
    Invalidated: IsSignOutForced = true

    Invalidated --> NewSession: New Session Created

    NewSession --> Anonymous

    Anonymous --> Expired: LastSeenAt > MaxSessionAge
    Authenticated --> Expired: LastSeenAt > MaxSessionAge

    Expired: Session Expired
    Expired: DbSessionInfoTrimmer deletes
```


## Database Entity Relationships

```mermaid
erDiagram
    Users ||--o{ UserIdentities : "1:N"
    Users ||--o{ _Sessions : "1:N (nullable)"

    Users {
        TDbUserId Id PK
        long Version
        string Name
        string ClaimsJson
    }

    UserIdentities {
        string Id PK "e.g. Google/abc123"
        TDbUserId DbUserId FK
        string Secret
    }

    _Sessions {
        string Id PK
        long Version
        DateTime CreatedAt
        DateTime LastSeenAt
        string IPAddress
        string UserAgent
        string AuthenticatedIdentity
        TDbUserId UserId FK "nullable"
        bool IsSignOutForced
        string OptionsJson
    }
```

**Indexes on `_Sessions`:**
- `(CreatedAt, IsSignOutForced)`
- `(LastSeenAt, IsSignOutForced)` - Used by `DbSessionInfoTrimmer`
- `(UserId, IsSignOutForced)`
- `(IPAddress, IsSignOutForced)`


## Authentication State Sync

```mermaid
flowchart TD
    Start["ServerAuthHelper.UpdateAuthState"]

    subgraph Compare ["Compare&nbsp;States"]
        direction LR
        ASP["ASP.NET Core State<br/>HttpContext.User"]
        Fusion["Fusion State<br/>IAuth.GetUser(session)"]
    end

    Start --> Compare
    Compare --> Match{"States Match?"}

    Match -->|"ASP.NET Signed In<br/>Fusion Not"| SignIn["AuthBackend_SignIn<br/>(sync to Fusion)"]
    Match -->|"Both Match"| NoAction["No Action"]
    Match -->|"Fusion Signed In<br/>ASP.NET Not"| SignOut["Auth_SignOut<br/>(sign out from Fusion)"]
```


## Presence Reporting

```mermaid
flowchart TD
    Start([Start]) --> Wait["Wait UpdatePeriod<br/>(3 min ± 5%)"]
    Wait --> Call["IAuth.UpdatePresence(session)"]

    Call --> Success
    Call --> Failure

    Success --> Wait
    Failure --> Retry["Wait RetryDelay<br/>(exponential backoff)"]
    Retry --> Call
```

**Server-side effect:**
- `DbSessionInfo.LastSeenAt` updated
- Prevents session from being trimmed


## Multi-Session Management

```mermaid
flowchart TD
    User["User (Id: 123)"]
    User --> A["Session A<br/>Desktop / Chrome"]
    User --> B["Session B<br/>Mobile / Safari"]
    User --> C["Session C<br/>Tablet / Firefox"]
```

| Sign Out Option | Code | Effect |
|-----------------|------|--------|
| Current session only | `Auth_SignOut(session: A)` | Only Session A signed out |
| Specific session | `Auth_SignOut(session: A, kickSessionHash: B.Hash)` | Session B signed out |
| All sessions | `Auth_SignOut(session: A, kickAllUserSessions: true)` | Sessions A, B, C all signed out |
| Force sign out | `Auth_SignOut(session: A, force: true)` | Session A permanently invalidated (new session created) |
