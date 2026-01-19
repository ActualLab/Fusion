# Authentication: Diagrams

ASCII diagrams illustrating Fusion's authentication architecture and flows.


## Authentication Service Architecture

```
┌───────────────────────────────────────────────────────────────┐
│                      Client (Blazor)                          │
├───────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐  ┌─────────────────┐  ┌────────────────┐ │
│  │ ClientAuthHelper│  │AuthStateProvider│  │PresenceReporter│ │
│  └────────┬────────┘  └────────┬────────┘  └───────┬────────┘ │
│           │                    │                   │          │
│           ▼                    ▼                   ▼          │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │                  IAuth (RPC Client)                     │  │
│  └─────────────────────────────────────────────────────────┘  │
└───────────────────────────────────────────────────────────────┘
                               │
                     WebSocket RPC
                               │
                               ▼
┌───────────────────────────────────────────────────────────────┐
│                       Server                                  │
├───────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐  ┌─────────────────┐  ┌───────────────┐  │
│  │SessionMiddleware│  │ ServerAuthHelper│  │  AuthEndpoint │  │
│  └────────┬────────┘  └────────┬────────┘  └───────┬───────┘  │
│           │                    │                   │          │
│           ▼                    ▼                   ▼          │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │                 IAuth / IAuthBackend                    │  │
│  ├─────────────────────────────────────────────────────────┤  │
│  │    InMemoryAuthService  │  DbAuthService<TDbContext>    │  │
│  └─────────────────────────────────────────────────────────┘  │
│                               │                               │
│                               ▼                               │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │                     Database                            │  │
│  │  ┌────────────┐  ┌─────────────┐  ┌──────────────────┐  │  │
│  │  │   Users    │  │ _Sessions   │  │  UserIdentities  │  │  │
│  │  └────────────┘  └─────────────┘  └──────────────────┘  │  │
│  └─────────────────────────────────────────────────────────┘  │
└───────────────────────────────────────────────────────────────┘
```


## Sign-In Flow

```
┌─────────┐  ┌────────────┐ ┌──────────────┐ ┌─────────────────┐
│ Browser │  │AuthEndpoint│ │OAuth Provider│ │ServerAuthHelper │
└────┬────┘  └─────┬──────┘ └──────┬───────┘ └────────┬────────┘
     │             │               │                  │
     │ GET /signIn │               │                  │
     │────────────>│               │                  │
     │             │               │                  │
     │             │ Challenge     │                  │
     │             │──────────────>│                  │
     │             │               │                  │
     │  Redirect to OAuth          │                  │
     │<────────────────────────────│                  │
     │             │               │                  │
     │ User authenticates          │                  │
     │────────────────────────────>│                  │
     │             │               │                  │
     │ Callback with tokens        │                  │
     │<────────────────────────────│                  │
     │             │               │                  │
     │ Redirect to app             │                  │
     │────────────>│               │                  │
     │             │               │                  │
     │ _HostPage loads             │                  │
     │───────────────────────────────────────────────>│
     │             │               │                  │
     │             │               │  UpdateAuthState │
     │             │               │  (syncs to IAuth)│
     │             │               │                  │
     │ Page rendered (authenticated)                  │
     │<───────────────────────────────────────────────│
     │             │               │                  │
```


## Session Resolution Flow

```
┌───────────────────────────────────────────────────────────────┐
│                    HTTP Request                               │
└───────────────────────────────────────────────────────────────┘
                               │
                               ▼
┌───────────────────────────────────────────────────────────────┐
│                   SessionMiddleware                           │
├───────────────────────────────────────────────────────────────┤
│                                                               │
│  1. Read session cookie ─────────────────────────────────┐    │
│                                                          │    │
│  2. Cookie exists? ─────────────────────────────────┐    │    │
│     │                                               │    │    │
│     ├─ Yes ─> Check IsSignOutForced                 │    │    │
│     │         │                                     │    │    │
│     │         ├─ Yes ─> Handle forced  ────┐        │    │    │
│     │         │         sign-out           ▼        │    │    │
│     │         │                  Create new session │    │    │
│     │         │                                │    │    │    │
│     │         └─ No ──> Use existing session ─>|    │    │    │
│     │                                          │    │    │    │
│     └─ No ────────────────────────────────────>│    │    │    │
│                                                │    │    │    │
│  3. Apply tags (if TagProvider configured) <───┴────┘    │    │
│                                                          │    │
│  4. Update cookie (if AlwaysUpdateCookie = true)         │    │
│                                                          │    │
│  5. Set SessionResolver.Session <────────────────────────┘    │
│                                                               │
└───────────────────────────────────────────────────────────────┘
                               │
                               ▼
┌───────────────────────────────────────────────────────────────┐
│                     Next Middleware                           │
└───────────────────────────────────────────────────────────────┘
```


## Default Session Replacement

```
┌───────────────────────────────────────────────────────────────┐
│                  Blazor WASM Client                           │
├───────────────────────────────────────────────────────────────┤
│                                                               │
│  SessionResolver.Session = Session.Default  ("~")             │
│                                                               │
│  ┌──────────────────────────────────────────────────────────┐ │
│  │ await Auth.GetUser(Session.Default, ct)                  │ │
│  └──────────────────────────────────────────────────────────┘ │
│                                                               │
└───────────────────────────────────────────────────────────────┘
                               │
                      RPC Call: GetUser(session: "~")
                               │
                               ▼
┌───────────────────────────────────────────────────────────────┐
│                RpcDefaultSessionReplacer                      │
├───────────────────────────────────────────────────────────────┤
│                                                               │
│  Session parameter == "~" ?                                   │
│    │                                                          │
│    ├── Yes ──> Replace with SessionResolver.Session           │
│    │           (real session from cookie)                     │
│    │                                                          │
│    └── No ───> Pass through unchanged                         │
│                                                               │
└───────────────────────────────────────────────────────────────┘
                               │
                      GetUser(session: "abc123...")
                               │
                               ▼
┌───────────────────────────────────────────────────────────────┐
│                   IAuth Implementation                        │
└───────────────────────────────────────────────────────────────┘
```


## IAuth vs IAuthBackend

```
┌───────────────────────────────────────────────────────────────┐
│                       IAuth                                   │
│                   (Client-Facing)                             │
├───────────────────────────────────────────────────────────────┤
│                                                               │
│  Commands:                                                    │
│  ├── SignOut(session)         - Sign out current session      │
│  ├── EditUser(session, name)  - Edit current user             │
│  └── UpdatePresence(session)  - Update last-seen              │
│                                                               │
│  Queries (all require Session):                               │
│  ├── GetUser(session)         - Get current user              │
│  ├── GetSessionInfo(session)  - Get session details           │
│  ├── GetAuthInfo(session)     - Get auth info                 │
│  ├── IsSignOutForced(session) - Check forced sign-out         │
│  └── GetUserSessions(session) - Get all user's sessions       │
│                                                               │
│  Exposed via RPC: YES                                         │
│                                                               │
└───────────────────────────────────────────────────────────────┘

┌───────────────────────────────────────────────────────────────┐
│                     IAuthBackend                              │
│                    (Server-Only)                              │
├───────────────────────────────────────────────────────────────┤
│                                                               │
│  Commands:                                                    │
│  ├── SignIn(session, user, identity) - Authenticate session   │
│  ├── SetupSession(session, ip, ua)   - Create/update session  │
│  └── SetOptions(session, options)    - Set session options    │
│                                                               │
│  Queries (NO Session required):                               │
│  └── GetUser(shard, userId) - Get any user by ID              │
│                                                               │
│  Exposed via RPC: NO (marked as IBackendService)              │
│                                                               │
└───────────────────────────────────────────────────────────────┘
```


## Authentication State Sync

```
┌───────────────────────────────────────────────────────────────┐
│                ServerAuthHelper.UpdateAuthState               │
└───────────────────────────────────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────┐         ┌─────────────────────────┐
│  ASP.NET Core State     │         │    Fusion State         │
├─────────────────────────┤         ├─────────────────────────┤
│ HttpContext.User        │         │ IAuth.GetUser(session)  │
│ (ClaimsPrincipal)       │ Compare │ (User)                  │
└────────────┬────────────┘         └────────────┬────────────┘
             │                                   │
             └─────────────┬─────────────────────┘
                           │
                           ▼
              ┌────────────────────────┐
              │   States Match?        │
              └────────────┬───────────┘
                           │
          ┌────────────────┼────────────────┐
          │                │                │
          ▼                ▼                ▼
    ┌──────────┐    ┌──────────┐    ┌──────────┐
    │ ASP.NET  │    │   Both   │    │ Fusion   │
    │Signed In │    │  Match   │    │Signed In │
    │Fusion Not│    │          │    │ASP.NET No│
    └────┬─────┘    └────┬─────┘    └────┬─────┘
         │               │               │
         ▼               ▼               ▼
┌────────────────┐ ┌──────────┐ ┌────────────────┐
│AuthBackend_    │ │  No      │ │Auth_SignOut    │
│SignIn          │ │  Action  │ │                │
│(sync to Fusion)│ │          │ │(sign out       │
│                │ │          │ │ from Fusion)   │
└────────────────┘ └──────────┘ └────────────────┘
```


## Database Entity Relationships

```
┌───────────────────────────────────────────────────────────────┐
│                    Users Table                                │
├───────────────────────────────────────────────────────────────┤
│  Id            │  TDbUserId (PK)                              │
│  Version       │  long                                        │
│  Name          │  string                                      │
│  ClaimsJson    │  string (JSON)                               │
└───────────────────────────────────────────────────────────────┘
         │
         │ 1:N
         ▼
┌───────────────────────────────────────────────────────────────┐
│               UserIdentities Table                            │
├───────────────────────────────────────────────────────────────┤
│  Id            │  string (PK)  e.g. "Google/abc123"           │
│  DbUserId      │  TDbUserId (FK -> Users)                     │
│  Secret        │  string                                      │
└───────────────────────────────────────────────────────────────┘

┌───────────────────────────────────────────────────────────────┐
│                 _Sessions Table                               │
├───────────────────────────────────────────────────────────────┤
│  Id                    │  string (PK)                         │
│  Version               │  long                                │
│  CreatedAt             │  DateTime                            │
│  LastSeenAt            │  DateTime                            │
│  IPAddress             │  string                              │
│  UserAgent             │  string                              │
│  AuthenticatedIdentity │  string                              │
│  UserId                │  TDbUserId? (FK -> Users, nullable)  │
│  IsSignOutForced       │  bool                                │
│  OptionsJson           │  string (JSON)                       │
└───────────────────────────────────────────────────────────────┘

Indexes:
  - (CreatedAt, IsSignOutForced)
  - (LastSeenAt, IsSignOutForced)  <- Used by DbSessionInfoTrimmer
  - (UserId, IsSignOutForced)
  - (IPAddress, IsSignOutForced)
```


## Session Lifecycle

```
┌───────────────────────────────────────────────────────────────┐
│                   Session Lifecycle                           │
└───────────────────────────────────────────────────────────────┘

   ┌─────────────┐
   │   Create    │  User visits site
   └──────┬──────┘
          │
          ▼
   ┌─────────────┐
   │ Anonymous   │  SessionMiddleware creates new session
   │ Session     │  Cookie set, session stored in DB
   └──────┬──────┘
          │
          ▼
   ┌─────────────┐
   │   Sign In   │  User authenticates via OAuth
   │             │  AuthBackend_SignIn links session to user
   └──────┬──────┘
          │
          ▼
   ┌─────────────┐
   │Authenticated│  Session has UserId set
   │ Session     │  User can access protected resources
   └──────┬──────┘
          │
          ├────────────────────────────┐
          │                            │
          ▼                            ▼
   ┌─────────────┐              ┌──────────────┐
   │  Sign Out   │              │Force Sign Out│
   │  (normal)   │              │              │
   └──────┬──────┘              └──────┬───────┘
          │                            │
          ▼                            ▼
   ┌─────────────┐              ┌──────────────┐
   │ Anonymous   │              │  Invalidated │
   │ Session     │              │  Session     │
   │(can sign in │              │(IsSignOutFor │
   │ again)      │              │ced = true)   │
   └──────┬──────┘              └──────┬───────┘
          │                            │
          │                            ▼
          │                     ┌─────────────┐
          │                     │ New Session │
          │                     │ Created     │
          │                     └─────────────┘
          │
          └──────────────┐
                         │
                         ▼
                  ┌─────────────┐
                  │   Expire    │  LastSeenAt > MaxSessionAge
                  │             │  DbSessionInfoTrimmer deletes
                  └─────────────┘
```


## Presence Reporting

```
┌───────────────────────────────────────────────────────────────┐
│                 PresenceReporter Loop                         │
└───────────────────────────────────────────────────────────────┘

         ┌────────────────────┐
         │ Start              │
         └─────────┬──────────┘
                   │
                   ▼
         ┌────────────────────┐
         │ Wait UpdatePeriod  │  Default: 3 min ± 5%
         │ (randomized)       │
         └─────────┬──────────┘
                   │
                   ▼
         ┌─────────────────────┐
         │ Call                │
         │ IAuth.UpdatePresence│
         │ (session)           │
         └─────────┬───────────┘
                   │
          ┌────────┴────────┐
          │                 │
          ▼                 ▼
   ┌────────────┐    ┌────────────┐
   │  Success   │    │  Failure   │
   └─────┬──────┘    └─────┬──────┘
         │                 │
         │                 ▼
         │          ┌────────────────────┐
         │          │ Wait RetryDelay    │
         │          │ (exponential)      │
         │          └─────────┬──────────┘
         │                    │
         └────────────────────┘
                   │
                   ▼
         ┌────────────────────┐
         │ Loop (forever)     │
         └────────────────────┘

Server-side effect:
  - DbSessionInfo.LastSeenAt updated
  - Prevents session from being trimmed
```


## Multi-Session Management

```
┌───────────────────────────────────────────────────────────────┐
│                    User with Multiple Sessions                │
└───────────────────────────────────────────────────────────────┘

                    ┌──────────────┐
                    │    User      │
                    │   Id: 123    │
                    └──────┬───────┘
                           │
           ┌───────────────┼───────────────┐
           │               │               │
           ▼               ▼               ▼
    ┌────────────┐  ┌────────────┐  ┌────────────┐
    │ Session A  │  │ Session B  │  │ Session C  │
    │ Desktop    │  │ Mobile     │  │ Tablet     │
    │ Chrome     │  │ Safari     │  │ Firefox    │
    └────────────┘  └────────────┘  └────────────┘

Sign Out Options:
━━━━━━━━━━━━━━━━

1. Sign out current session only:
   Auth_SignOut(session: A)
   └── Only Session A signed out

2. Sign out specific session:
   Auth_SignOut(session: A, kickSessionHash: B.Hash)
   └── Session B signed out

3. Sign out all sessions:
   Auth_SignOut(session: A, kickAllUserSessions: true)
   └── Sessions A, B, C all signed out

4. Force sign out (invalidates session):
   Auth_SignOut(session: A, force: true)
   └── Session A permanently invalidated
       (cannot be reused, new session created)
```
