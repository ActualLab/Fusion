# P4 — Server hosting endpoints (ASP.NET Core + .NET Framework)

Reviewer: Opus. Scope: `src/ActualLab.Rpc.Server/`, `src/ActualLab.Rpc.Server.NetFx/`,
`src/ActualLab.Fusion.Server/`, `src/ActualLab.Fusion.Server.NetFx/`, `src/ActualLab.RestEase/`.

Summary: 4 HIGH, 4 MEDIUM, 3 LOW.

---

### F1. WebSocket upgrade endpoint performs no `Origin` check — cross-site WebSocket hijacking of the cookie-bound session

- **Severity:** HIGH (CRITICAL in the `SameSite=None` configuration the docs recommend)
- **Confidence:** CONFIRMED (the absence of any origin check and the cookie→connection binding
  are both verified in source; the browser cookie-attachment behaviour is standard, not tested here)
- **Category:** auth-bypass / csrf
- **Location:**
  - `src/ActualLab.Rpc.Server/RpcWebSocketServer.cs:33` (the only pre-accept validation is `IsWebSocketRequest`)
  - `src/ActualLab.Rpc.Server/EndpointRouteBuilderExt.cs:22` (endpoint mapped with no auth/origin metadata)
  - `src/ActualLab.Rpc.Server/RpcWebSocketServerDefaultDelegates.cs:36` (the `ConfigureWebSocket` hook returns a bare `new WebSocketAcceptContext()`)
  - `src/ActualLab.Fusion.Server/Rpc/RpcPeerOptionsExt.cs:34` (falls back to the request's session cookie)
  - `src/ActualLab.Fusion.Server.NetFx/../ActualLab.Rpc.Server.NetFx/RpcWebSocketServer.cs:31` (same on OWIN)

- **What:** The RPC WebSocket endpoint accepts an upgrade from any origin. Nothing in
  `ActualLab.Rpc.Server` or `ActualLab.Fusion.Server` inspects the `Origin` header, sets
  `WebSocketOptions.AllowedOrigins`, or documents that the host must. At the same time
  `RpcPeerOptionsExt.ServerConnectionFactory` binds the browser's `FusionAuth.SessionId`
  **cookie** to the resulting RPC connection, and `RpcDefaultSessionReplacer` then substitutes
  that session into every inbound call that passes `Session.Default`. The WebSocket handshake is
  exempt from CORS and from preflight, so the same-origin policy does not stand in for the
  missing check.

- **Why it matters / attack path:**
  1. Victim is signed in to `https://app.example.com`; the browser holds `FusionAuth.SessionId`.
  2. Victim visits an attacker page and the page executes
     `new WebSocket("wss://app.example.com/rpc/ws?clientId=<random>&f=mempack3")`.
  3. The browser attaches the session cookie whenever cookie policy permits — which is the case
     both when the app follows the documented cross-origin recipe
     (`docs/PartAA-Server.md:498`: `SameSite = SameSiteMode.None`) and, under the default
     `SameSite=Lax`, whenever the attacker controls *any* origin under the same registrable
     domain (a sibling subdomain, a subdomain takeover, a customer-controlled subdomain):
     `Lax` gates on *site*, not *origin*.
  4. `RpcWebSocketServer.Invoke` accepts the socket, `ServerConnectionFactory` reads the cookie
     (`src/ActualLab.Fusion.Server/Rpc/RpcPeerOptionsExt.cs:34-36`) and produces a
     `SessionBoundRpcConnection` carrying the **victim's** session.
  5. The attacker page now speaks the RPC protocol directly and can invoke every compute
     service / command exposed to clients as the victim, and read every result. Full
     account takeover, bidirectional, for as long as the tab is open.

  Note the HTTP/2 transport (`RpcHttpServer`) is *not* equally exposed: it is a POST with
  `application/octet-stream` request streaming, which forces a CORS preflight. The WebSocket
  path is the gap.

- **Evidence:**
  ```csharp
  // src/ActualLab.Rpc.Server/RpcWebSocketServer.cs:33
  if (!context.WebSockets.IsWebSocketRequest) { ... }      // the ONLY request validation
  ...
  // :79
  var webSocketAcceptContext = Options.ConfigureWebSocket.Invoke(this, context, rpcRef);
  ```
  ```csharp
  // src/ActualLab.Rpc.Server/RpcWebSocketServerDefaultDelegates.cs:36
  public static RpcWebSocketServerAcceptContextFactory AcceptContextFactory { get; set; } =
      static (server, context, rpcRef) => new();
  ```
  ```csharp
  // src/ActualLab.Fusion.Server/Rpc/RpcPeerOptionsExt.cs:34
  var sessionMiddleware = httpContext.RequestServices.GetService<SessionMiddleware>();
  if (sessionMiddleware?.GetSession(httpContext) is { } session2 && session2.IsValid())
      return CreateSessionBoundRpcConnectionAsync(transport, properties, session2);
  ```
  No sample or doc sets `AllowedOrigins`: `samples/TodoApp/Host/Program.cs:310`,
  `samples/HelloCart/v4/AppV4.cs:44`, `docs/PartAA-Server.md:334` all call
  `app.UseWebSockets()` with only `KeepAliveInterval`. `grep -ni origin` over all four server
  projects returns nothing.

- **Fix:** Add an origin check owned by Fusion rather than delegating it to the host by
  omission. Concretely: add `Func<HttpContext, bool>? OriginValidator` (or
  `IReadOnlyList<string> AllowedOrigins`) to `RpcWebSocketServerOptions`, evaluate it in
  `RpcWebSocketServer.Invoke` before `Hub.GetServerPeer` and reject with 403 on mismatch, and
  default it to *same-origin only* (`Origin` absent → non-browser client → allow;
  `Origin` present and not equal to `Request.Host` → reject) so the safe behaviour is the
  default. Mirror it in the OWIN server. Also amend `docs/PartAA-Server.md`'s
  `SameSite=None` recipe to require an explicit allow-list.

---

### F2. Unauthenticated WebSocket upgrade pins a server peer for ≥3 minutes — unbounded peer-registry growth (pre-auth memory exhaustion)

- **Severity:** HIGH
- **Confidence:** CONFIRMED (source-traced end to end; not load-tested)
- **Category:** dos
- **Location:**
  - `src/ActualLab.Rpc.Server/RpcWebSocketServer.cs:62` (`Hub.GetServerPeer(rpcRef)` — before the socket is even accepted)
  - `src/ActualLab.Rpc.Server/RpcWebSocketServerDefaultDelegates.cs:30` (peer key = raw `clientId` query value)
  - `src/ActualLab.Rpc.Server/RpcHttpServer.cs:57` (same on the HTTP/2 endpoint)
  - `src/ActualLab.Rpc.Server.NetFx/RpcWebSocketServer.cs:45` (same on OWIN)
  - supporting: `src/ActualLab.Rpc/RpcHub.cs:124`, `src/ActualLab.Rpc/Configuration/Options/RpcPeerOptions.cs:54`

- **What:** Every distinct `?clientId=` value seen on the endpoint materialises a new
  `RpcServerPeer` in the process-wide `RpcHub.Peers` dictionary. The peer is created *before*
  the WebSocket is accepted, before any handshake, and before any authentication of any kind.
  When no connection is delivered (or the connection drops) the peer parks in
  `RpcServerPeer.GetConnection` waiting for a reconnect for
  `ServerPeerShutdownTimeoutProvider(peer)`, whose default is **clamped to a 3-minute minimum**.
  There is no cap on `RpcHub.Peers`, no per-IP limit, and no rate limit.

- **Why it matters / attack path:** An unauthenticated attacker issues bare WebSocket upgrade
  requests, each with a fresh random `clientId`, and drops the connection immediately (or never
  completes the upgrade at all — `GetServerPeer` at line 62 already ran). Each cheap request
  pins one `RpcServerPeer` for at least 3 minutes. Each peer allocates the peer object, an
  `RpcRoute`/`RpcRef`, a `MutablePropertyBag`, a message serializer, and **four**
  `ConcurrentDictionary<long, …>(ProcessorCountPo2, 131)` trackers
  (`src/ActualLab.Rpc/Infrastructure/RpcCallTrackers.cs:15`,
  `src/ActualLab.Rpc/RpcPeer.cs:129-136`) — order 10 KB each — plus a parked async state
  machine. At 1 000 requests/s the steady state is ~180 000 live peers ⇒ multiple GB ⇒ OOM.
  Secondary amplification: each peer emits ~5 log records, including
  `Log.LogWarning("'{Route}': peer is removed from RpcHub", …)`
  (`src/ActualLab.Rpc/RpcHub.cs:154`) and `RpcReconnectFailedException.ClientIsGone()`, so the
  same requests also flood the log pipeline at Warning level.

- **Evidence:**
  ```csharp
  // src/ActualLab.Rpc.Server/RpcWebSocketServer.cs:61-62 — before AcceptWebSocketAsync (line 84)
  Log.LogInformation("'{PeerRef}': Accepting RPC connection for {Request}", rpcRef, requestDescription);
  var peer = Hub.GetServerPeer(rpcRef);
  ```
  ```csharp
  // src/ActualLab.Rpc/Configuration/Options/RpcPeerOptions.cs:54
  protected static TimeSpan DefaultServerPeerShutdownTimeoutProvider(RpcServerPeer peer)
  {
      var peerLifetime = Moment.Now - peer.CreatedAt;          // ~0 for a fresh peer
      return peerLifetime.MultiplyBy(0.33).Clamp(TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(15));
  }
  ```
  ```csharp
  // src/ActualLab.Rpc/RpcServerPeer.cs:77-86 — parks for closeTimeout, then gives up
  var closeTimeout = Hub.PeerOptions.ServerPeerShutdownTimeoutProvider.Invoke(this);
  await nextConnection.When(x => x is not null, cancellationToken).WaitAsync(closeTimeout, ...);
  ```
  `RpcHub.GetPeer` (`src/ActualLab.Rpc/RpcHub.cs:124-147`) has no size check of any kind, and
  the empty `f` parameter resolves to the default format
  (`src/ActualLab.Rpc/Configuration/RpcSerializationFormatResolver.cs:57`), so
  `GET /rpc/ws?clientId=<n>` with the four standard upgrade headers is the entire request.

- **Fix:** (a) Do not create the peer until the transport actually exists — move
  `Hub.GetServerPeer` after `AcceptWebSocketAsync`; (b) add a configurable cap on the number of
  live server peers (and on peers per remote IP) in `RpcWebSocketServerOptions` /
  `RpcHttpServerOptions`, rejecting with 503 past the cap; (c) use a much shorter
  "never-connected" grace period than the 3-minute reconnect window — a peer that has never
  completed a handshake should expire in seconds. (b) belongs in this partition even if (c)
  lives in `RpcPeerOptions`.

---

### F3. Session ids and client ids are written to the server log on every RPC connection

- **Severity:** HIGH
- **Confidence:** CONFIRMED
- **Category:** info-leak
- **Location:**
  - `src/ActualLab.Rpc.Server/RpcWebSocketServer.cs:31` and `:61` (also `:45`, `:104`, `:110`, `:115`)
  - `src/ActualLab.Rpc.Server/RpcHttpServer.cs:31` and `:56` (also `:50`, `:101`, `:107`, `:112`)
  - `src/ActualLab.Rpc.Server.NetFx/RpcWebSocketServer.cs:108`
  - `src/ActualLab.Fusion.Server/Middlewares/SessionMiddleware.cs:95`

- **What:** `requestDescription` is built from the full request URI **including the query
  string** and is logged at `Information` on the success path of every accepted connection.
  Fusion's own session-passing convention puts the session id in that query string
  (`?session=…`, consumed at `src/ActualLab.Fusion.Server/Rpc/RpcPeerOptionsExt.cs:29-32`), and
  the `clientId` — which is a peer-identity capability, see F4 — is always there. A Fusion
  session id is a full bearer credential: possession is equivalent to being the user.

- **Why it matters / attack path:** Anyone with read access to application logs (log
  aggregation, an on-call engineer, a leaked log bundle, an SIEM export, a crash dump) obtains
  live session ids for every connected user and can impersonate any of them by opening an RPC
  connection with `?session=<stolen>` — which `ServerConnectionFactory` accepts verbatim. The
  same lines leak the `clientId`, enabling F4. `ActualLab.*` categories log at `Information`
  under the default `"Default": "Information"` filter, so this is on by default.

  The project already treats this as a real risk on the client side: `sanitizeUrl` was added to
  `@actuallab/rpc` specifically so "the connect-attempt log line no longer leaks bearer-style
  query parameters" (`docs/CHANGELOG.md:1024`, implementation at
  `ts/packages/rpc/src/rpc-peer.ts:144-153`). The server never got the equivalent treatment.

- **Evidence:**
  ```csharp
  // src/ActualLab.Rpc.Server/RpcWebSocketServer.cs:25-31
  var uri = new UriBuilder(request.Scheme, request.Host.Host, request.Host.Port ?? -1,
      request.Path, request.QueryString.ToString());     // <-- full query, unredacted
  var requestDescription = $"{request.Method} {uri}";
  // :61
  Log.LogInformation("'{PeerRef}': Accepting RPC connection for {Request}", rpcRef, requestDescription);
  ```
  ```csharp
  // src/ActualLab.Fusion.Server/Middlewares/SessionMiddleware.cs:95
  Log.LogError(e, "Session is unavailable: {Session}", session);   // Session.ToString() == Id
  ```
  ```ts
  // ts/packages/rpc/src/rpc-peer.ts:147-149  — the client-side counterpart that DOES redact
  if (!parsed.searchParams.has('session')) return url;
  parsed.searchParams.set('session', '<redacted>');
  ```

- **Fix:** Build `requestDescription` from `Path` plus a sanitized query (drop or redact
  `session`, and redact `clientId` to a short prefix/hash), in both ASP.NET Core servers and the
  OWIN one. Log `session.Hash` instead of `session` in `SessionMiddleware`. Longer term,
  deprecate `?session=` in favour of a header — a query parameter is logged by every reverse
  proxy, load balancer and APM agent in the path, none of which Fusion controls.

---

### F4. `clientId` from the query string is an unauthenticated peer-identity capability

- **Severity:** HIGH
- **Confidence:** CONFIRMED for the mechanism (peer binding + forced disconnect of the previous
  connection). The downstream data-exfiltration consequence is traced through source but was
  not executed against a live server.
- **Category:** auth-bypass / info-leak
- **Location:**
  - `src/ActualLab.Rpc.Server/RpcWebSocketServerDefaultDelegates.cs:27-33`
  - `src/ActualLab.Rpc.Server/RpcWebSocketServer.cs:62-76`
  - `src/ActualLab.Rpc.Server/RpcHttpServerDefaultDelegates.cs:17-23`, `src/ActualLab.Rpc.Server/RpcHttpServer.cs:57-65`
  - `src/ActualLab.Rpc.Server.NetFx/RpcWebSocketServerDefaultDelegates.cs:25-31`

- **What:** The server peer a connection attaches to is chosen solely by the client-supplied
  `clientId` query parameter. There is no proof-of-possession: the value is not signed, not
  bound to the session, and not checked against anything. A request that presents an existing
  `clientId` is treated as that peer *reconnecting*, and the server **actively tears down the
  existing connection** to make room for it.

- **Why it matters / attack path:** Given a victim's `clientId` (which F3 puts in the server
  log, and which every reverse proxy in the path also logs, since it travels in the URL):
  1. **Denial of service, unconditional.** Connect to `/rpc/ws?clientId=<victim>`. Lines 72-76
     call `peer.Disconnect(...)` on the victim's live connection. Repeat in a loop and the
     victim can never hold a connection. No credential is needed.
  2. **Peer takeover.** `RpcClientPeer.ClientId` is *literally* the base64url encoding of the
     peer's `Id` GUID (`src/ActualLab.Rpc/RpcClientPeer.cs:20`, `GuidExt.ToBase64Url` at
     `src/ActualLab.Core/Mathematics/GuidExt.cs:30`), and that same GUID is the
     `RpcHandshake.RemotePeerId` the client sends. So the attacker can reverse the `clientId`
     into the victim's `RemotePeerId` and replay it in its own handshake. `GetPeerChangeKind`
     compares nothing else (`src/ActualLab.Rpc/Infrastructure/RpcHandshake.cs:30-32`), so the
     server classifies the attacker as `Unchanged` and **skips `Reset()`**
     (`src/ActualLab.Rpc/RpcPeer.cs:369-377`). The peer's server-side state survives onto the
     attacker's socket: still-running inbound calls send their results through
     `RpcOutboundContext(peer, …)` → the peer's *current* transport
     (`src/ActualLab.Rpc/Infrastructure/RpcInboundCall.cs:320-321`,
     `RpcSystemCallSender.Ok`), and `SharedObjects.Maintain` keeps pumping the victim's
     server→client streams down the same transport (`src/ActualLab.Rpc/RpcPeer.cs:401-406`).
     Net effect: results and stream data computed for the victim's session are delivered to the
     attacker.

  Both clients generate the id with a CSPRNG (`Guid.NewGuid()`; TS `crypto.randomUUID()` at
  `ts/packages/rpc/src/rpc-peer.ts:194`), so it is not guessable — the exposure is entirely
  "this secret is carried in a URL and logged", which is exactly F3.

- **Evidence:**
  ```csharp
  // src/ActualLab.Rpc.Server/RpcWebSocketServerDefaultDelegates.cs:28-32
  var clientId = query[server.Options.ClientIdParameterName].SingleOrDefault() ?? "";
  var serializationFormat = query[server.Options.SerializationFormatParameterName].SingleOrDefault() ?? "";
  return RpcRef.NewServer(clientId, serializationFormat, isBackend);
  ```
  ```csharp
  // src/ActualLab.Rpc.Server/RpcWebSocketServer.cs:72-76
  if (peer.ConnectionState.Value.IsConnectingOrConnected()) {
      Log.LogWarning("'{PeerRef}': {Peer} is already connected, disconnecting the old connection first...", ...);
      await peer.Disconnect(cancellationToken).ConfigureAwait(false);
  }
  ```
  ```csharp
  // src/ActualLab.Rpc/RpcClientPeer.cs:20 — clientId IS the RemotePeerId, reversibly
  ClientId = Id.ToBase64Url();
  ```
  `RpcRef` equality is by `Address` precisely so that "an RPC client can reconnect to exactly
  the same peer rather than a new one … they use the 'clientId' parameter to construct a new
  server RpcRef on each WebSocket connection" (`src/ActualLab.Rpc/RpcRef.cs:121-131`) — the
  reconnect-resumption is intended; the missing authentication of the resumption is the defect.

- **Fix:** Require the reconnecting party to prove possession of something the server issued.
  Minimum viable change inside this partition: make the peer key a function of both the
  `clientId` *and* the connection's authenticated identity (bound session / auth ticket), so a
  connection carrying a different session can never land on another client's peer — the
  `RpcWebSocketServerRefFactory` is already the designed extension point for that
  (`RpcRef.NewServer(hostInfo, …)`), but the default implementation must be the safe one, not
  an opt-in. Stronger: have the server mint a random reconnect token at first handshake, return
  it in the handshake, and require it (in a header, not the query string) on reconnect; then
  stop deriving `ClientId` from the peer `Id` so the handshake id is not recoverable from the
  URL.

---

### F5. Session bound to an RPC connection bypasses `ISessionValidator`

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** auth-bypass
- **Location:** `src/ActualLab.Fusion.Server/Rpc/RpcPeerOptionsExt.cs:28-38`
  (contrast `src/ActualLab.Fusion.Server/Middlewares/SessionMiddleware.cs:89-92`)

- **What:** `SessionMiddleware` runs the registered `ISessionValidator` and refuses to adopt a
  session it rejects. The RPC connection factory does neither: the `?session=` query value is
  accepted after only a syntactic `IsValid()` check, and the cookie fallback calls
  `sessionMiddleware.GetSession(httpContext)` — the raw cookie reader — rather than the
  validating `GetOrCreateSession`. So the validator gates HTTP requests but not RPC
  connections, which is where essentially all Fusion traffic flows.

- **Why it matters / attack path:** `ISessionValidator` is the framework's documented hook for
  "is this session still acceptable" and is wired to `IAuth`
  (`src/ActualLab.Fusion.Ext.Services/Authentication/FusionBuilderExt.cs:70`), whose
  implementation is `session.IsValid() && !IsSignOutForced(session)`
  (`src/ActualLab.Fusion.Ext.Services/Authentication/Services/DbAuthService.Base.cs:40`). An
  application that plugs in its own validator to enforce revocation, expiry, IP pinning or
  tenant checks will see it honoured on HTTP and silently skipped on the WebSocket, and a
  client can hold a rejected session on an RPC connection indefinitely by supplying it as
  `?session=`. With the built-in `IAuth` the practical impact is limited because a forced
  sign-out also clears `UserId` in the store, so the session de-authenticates anyway — the
  severity here is "a security extension point that does not cover the main entry point",
  not a live authentication bypass in the default stack.

- **Evidence:**
  ```csharp
  // src/ActualLab.Fusion.Server/Rpc/RpcPeerOptionsExt.cs:29-36
  var sessionValues = query[SessionParameterName];
  var sessionId = sessionValues.Count == 1 ? sessionValues[0] ?? "" : "";
  if (!sessionId.IsNullOrEmpty() && new Session(sessionId) is var session1 && session1.IsValid())
      return CreateSessionBoundRpcConnectionAsync(transport, properties, session1);   // no validator
  var sessionMiddleware = httpContext.RequestServices.GetService<SessionMiddleware>();
  if (sessionMiddleware?.GetSession(httpContext) is { } session2 && session2.IsValid())
      return CreateSessionBoundRpcConnectionAsync(transport, properties, session2);   // no validator
  ```
  vs.
  ```csharp
  // src/ActualLab.Fusion.Server/Middlewares/SessionMiddleware.cs:89-92
  if (session is not null && SessionValidator is not null) {
      var isValid = await SessionValidator.IsValidSession(session, cancellationToken)...;
      isInvalid = !isValid;
  }
  ```

- **Fix:** Make `ServerConnectionFactory` async-await the `ISessionValidator` (it already
  returns a `Task<RpcConnection>` and receives a `CancellationToken`) for both the query and
  cookie branches; on rejection, fall through to an unbound `RpcConnection` rather than binding
  the session. Re-validate on each reconnect, which happens naturally since the factory runs
  per connection.

---

### F6. `SessionMiddleware` adopts any client-supplied cookie value as the session identity, and it is never rotated at sign-in (session fixation)

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** auth / logic
- **Location:** `src/ActualLab.Fusion.Server/Middlewares/SessionMiddleware.cs:72-78` and `:106-112`
  (sign-in side, out of partition: `src/ActualLab.Fusion.Ext.Services/Authentication/ServerAuthHelper.cs:172-181`)

- **What:** `GetSession` turns whatever is in the `FusionAuth.SessionId` cookie into a
  `Session` with no server-side check that the id was ever issued by this server — the only
  constraint is `Session`'s ctor requiring length ≥ 8
  (`src/ActualLab.Fusion/Session/Session.cs:36-42`). The middleware then re-issues that same id
  as the response cookie. Authentication later attaches the user to *that* id
  (`AuthBackend_SignIn(session, …)` with the pre-existing session), and nothing anywhere mints a
  fresh session id at the authentication boundary.

- **Why it matters / attack path:** An attacker who can plant a cookie in the victim's browser —
  a sibling subdomain setting `Domain=example.com`, any cookie-injection primitive, or an
  active network position on a non-HSTS host — fixes the victim's session id to a value the
  attacker knows. The victim signs in; `ServerAuthHelper.SignIn` binds the real user to the
  attacker-chosen id; the attacker replays it (as a cookie, or as `?session=` on an RPC
  connection — see F5) and is now the victim. This is textbook session fixation, and the usual
  mitigation (regenerate the session identifier on privilege change) is absent by design.

- **Evidence:**
  ```csharp
  // src/ActualLab.Fusion.Server/Middlewares/SessionMiddleware.cs:72-78
  public virtual Session? GetSession(HttpContext httpContext) {
      var cookies = httpContext.Request.Cookies;
      cookies.TryGetValue(cookieName, out var sessionId);
      return sessionId.IsNullOrEmpty() ? null : new Session(sessionId);   // adopted verbatim
  }
  // :108-111 — and echoed straight back
  responseCookies.Append(cookieName, session.Id, Settings.Cookie.Build(httpContext));
  ```
  ```csharp
  // src/ActualLab.Fusion.Ext.Services/Authentication/ServerAuthHelper.cs:179 — same id, now authenticated
  var signInCommand = new AuthBackend_SignIn(session, newUser, authenticatedIdentity);
  ```

- **Fix:** Rotate at the privilege boundary: after a successful `SignIn`, mint
  `Session.New()`, migrate the auth state to it, and overwrite the cookie (the RPC connection's
  bound session will pick the new id up on the next reconnect). Optionally, have
  `SessionMiddleware` refuse cookie values that are not recognised by the session store,
  falling back to `Session.New()` instead of adopting the caller's id.

---

### F7. .NET Framework Fusion server never binds a session to RPC connections

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** logic (broken feature)
- **Location:** `src/ActualLab.Fusion.Server.NetFx/FusionWebServerBuilder.cs:17-33`
  (contrast `src/ActualLab.Fusion.Server/FusionWebServerBuilder.cs:38-47` and
  `src/ActualLab.Fusion.Server/Internal/FusionServerModuleInitializer.cs:13-20`)

- **What:** On ASP.NET Core, `FusionWebServerBuilder` registers `RpcDefaultSessionReplacer` and
  a module initializer applies `ApplyFusionServerOverrides()`, which installs the
  session-binding `ServerConnectionFactory`. The .NET Framework builder does neither, and the
  `ActualLab.Fusion.Server.NetFx` assembly contains no equivalent of either type (verified by
  file listing and by its `.csproj`, which references only `Ext.Contracts` and
  `Rpc.Server.NetFx`).

- **Why it matters / attack path:** Fusion clients rely on sending `Session.Default` and having
  the server substitute the connection's session. Against a .NET Framework host the
  substitution never happens, so every session-taking compute method / command receives
  `Session.Default`, which `RequireValid()` rejects
  (`src/ActualLab.Fusion/Session/Session.cs:24` — `Validator = session => !session.IsDefault()`).
  The feature fails closed, so this is a functionality gap rather than a hole — but it is a
  silent one: nothing in the builder or the README says session-bound RPC is unsupported on
  .NET Framework, and an application that "works" only because clients happen to pass explicit
  sessions is one client change away from breaking.

- **Evidence:**
  ```csharp
  // src/ActualLab.Fusion.Server/FusionWebServerBuilder.cs:40-41  (ASP.NET Core)
  rpc.AddWebSocketServer();
  rpc.AddMiddleware(_ => new RpcDefaultSessionReplacer());
  ```
  ```csharp
  // src/ActualLab.Fusion.Server.NetFx/FusionWebServerBuilder.cs:29-30  (.NET Framework)
  services.Insert(0, AddedTagDescriptor);
  fusion.Rpc.AddWebSocketServer();          // no middleware, no ServerConnectionFactory override
  ```

- **Fix:** Port `RpcPeerOptionsExt`/`SessionBoundRpcConnection`/`RpcDefaultSessionReplacer` to
  the OWIN project (reading the session from `IOwinContext.Request.Query["session"]` and the
  request cookie) and register them from the NetFx `FusionWebServerBuilder`; or document the
  limitation explicitly and fail fast at startup.

---

### F8. `JsonifyErrorsAttribute` returns unfiltered exception type and message to the caller

- **Severity:** MEDIUM
- **Confidence:** CONFIRMED
- **Category:** info-leak
- **Location:** `src/ActualLab.Fusion.Server/JsonifyErrorsAttribute.cs:21-27`,
  `src/ActualLab.Fusion.Server.NetFx/JsonifyErrorsAttribute.cs:25-32`

- **What:** The filter serializes `exception.ToExceptionInfo()` — the assembly-qualified
  exception type name plus `Exception.Message` — into the 500 response body for *any*
  exception, with no allow-list, no rewriting hook, and no environment gate.

- **Why it matters / attack path:** Any anonymous caller who can reach a controller decorated
  with `[JsonifyErrors]` and trigger an unexpected exception receives the internal exception
  type (which discloses assembly names, ORM/provider, and internal namespaces) and the raw
  message, which for `SqlException`, `IOException`, `SocketException`,
  `InvalidOperationException` from EF, etc. routinely contains table/column names, file paths,
  hostnames or connection details. The tests around this filter
  (`tests/ActualLab.Fusion.Tests/Server/JsonifyErrorsAttributeTests.cs:19`) still refer to a
  "RewriteErrorsIfSet" behaviour that the current implementation no longer has.

- **Evidence:**
  ```csharp
  // src/ActualLab.Fusion.Server/JsonifyErrorsAttribute.cs:21-27
  var serializer = TypeDecoratingTextSerializer.Default;
  var content = serializer.Write(exception.ToExceptionInfo());
  var result = new ContentResult() { Content = content, ContentType = "application/json",
      StatusCode = (int)HttpStatusCode.InternalServerError };
  ```

- **Fix:** Reinstate a `Func<Exception, Exception>` rewrite/allow-list hook on the attribute,
  defaulting to "pass through only exception types the app opts into, otherwise emit a generic
  `RemoteException`". `ActualLab.RestEase`'s client already degrades gracefully to
  `Errors.UnknownServerSideError()` when it cannot reconstruct the exception
  (`src/ActualLab.RestEase/Internal/RestEaseHttpMessageHandler.cs:40-53`), so a redacted default
  does not break the RestEase round-trip.

---

### F9. An over-long `f` query value makes the rejection path throw instead of closing the socket

- **Severity:** LOW
- **Confidence:** CONFIRMED
- **Category:** dos (minor) / logic
- **Location:** `src/ActualLab.Rpc.Server/RpcWebSocketServer.cs:53-57`

- **What:** The unsupported-format rejection echoes the attacker-supplied format key into the
  WebSocket close description. `WebSocket.CloseAsync` rejects a `statusDescription` longer than
  123 UTF-8 bytes with `ArgumentException`; the fixed prefix is 38 characters, so any `f` value
  over ~85 characters throws. The exception is swallowed by the outer catch
  (`:115` logs a Warning, `:117` returns), so the close frame is never sent and the socket is
  aborted instead.

- **Why it matters:** An unauthenticated request produces an accepted WebSocket, a thrown
  exception, and a `Warning`-level log record with a stack trace — a cheap log-amplification
  primitive, and a misleading error for legitimate clients that mistype the format.

- **Evidence:**
  ```csharp
  // src/ActualLab.Rpc.Server/RpcWebSocketServer.cs:53-57
  await webSocket.CloseAsync(
      (WebSocketCloseStatus)RpcWebSocketCloseCode.UnsupportedFormat,
      $"Unsupported RPC serialization format: '{rpcRef.SerializationFormat}'",  // unbounded
      cancellationToken).ConfigureAwait(false);
  ```

- **Fix:** Truncate the format key (e.g. to 32 chars) before interpolating, or send a constant
  description. Better still, reject unsupported formats with a 400 before the upgrade, as the
  OWIN server already does (`src/ActualLab.Rpc.Server.NetFx/RpcWebSocketServer.cs:39-43`).

---

### F10. `MapFusionRenderModeEndpoints` emits a malformed `Location: ~/` redirect

- **Severity:** LOW
- **Confidence:** CONFIRMED
- **Category:** logic
- **Location:** `src/ActualLab.Fusion.Server/Endpoints/RenderModeEndpoint.cs:42-45` and `:61-65`

- **What:** `redirectTo` defaults to the MVC-style app-relative `"~/"`, and the failed-check
  fallback uses it too. The MVC path is fine — `RedirectResultExecutor` runs the value through
  `IUrlHelper.Content`, which expands `~/`. The minimal-API path does not:
  `Results.Redirect("~/")` writes the literal string into the `Location` header, so the browser
  resolves it relative to `/fusion/renderMode/...` and lands on a non-existent path.

- **Why it matters:** Every render-mode switch made through `MapFusionRenderModeEndpoints`
  (the .NET 7+ route, `src/ActualLab.Fusion.Server/EndpointRouteBuilderExt.cs:19-23`) redirects
  to the wrong URL when no explicit `redirectTo` is supplied.

- **Fix:** Use `"/"` rather than `"~/"` in `RenderModeEndpoint.Invoke`, or resolve `~/` against
  `httpContext.Request.PathBase` inside `RedirectResult.ExecuteAsync`.

---

### F11. Duplicate `clientId`/`f` query parameters throw, and the OWIN server echoes an unvalidated sub-protocol

- **Severity:** LOW
- **Confidence:** CONFIRMED
- **Category:** logic
- **Location:** `src/ActualLab.Rpc.Server/RpcWebSocketServerDefaultDelegates.cs:30-31`,
  `src/ActualLab.Rpc.Server/RpcHttpServerDefaultDelegates.cs:20-21`,
  `src/ActualLab.Rpc.Server.NetFx/RpcWebSocketServerDefaultDelegates.cs:36-38`

- **What:** Two issues in the request-parsing delegates.
  (a) `query[...].SingleOrDefault()` on a `StringValues` holding two entries throws
  `InvalidOperationException`, so `GET /rpc/ws?clientId=a&clientId=b` yields a 500 plus a
  `Warning` log with a stack trace rather than a 400. The `session` parameter in
  `RpcPeerOptionsExt.cs:30` already handles the duplicate case explicitly
  (`sessionValues.Count == 1 ? … : ""`), so the inconsistency is unintended.
  (b) The OWIN accept-context factory selects the client's *first offered* sub-protocol without
  checking it against anything the server supports, reflecting arbitrary client input into the
  `Sec-WebSocket-Protocol` response header. RFC 6455 requires the server to choose a protocol it
  actually implements; the RPC layer implements none.

- **Fix:** (a) Replace `SingleOrDefault()` with `Count == 1 ? values[0] : ""` and return a 400
  for ambiguous values. (b) Drop the sub-protocol echo, or match it against an explicit
  supported-protocol list.

---

## Out-of-partition findings

- The peer-resumption trust decision that makes F4 exploitable lives in P1:
  `RpcHandshake.GetPeerChangeKind` (`src/ActualLab.Rpc/Infrastructure/RpcHandshake.cs:30-32`)
  decides "same client, keep all state" from a single client-supplied GUID, and
  `RpcClientPeer.ClientId = Id.ToBase64Url()` (`src/ActualLab.Rpc/RpcClientPeer.cs:20`) publishes
  that GUID in the connection URL. Even with an authenticated peer key (F4's fix), deriving the
  handshake id from the URL-visible client id is worth removing.
- The reconnect grace period that turns F2 into a 3-minute pin is
  `RpcPeerOptions.DefaultServerPeerShutdownTimeoutProvider`
  (`src/ActualLab.Rpc/Configuration/Options/RpcPeerOptions.cs:54-58`) — P1.
- `RestEaseHttpMessageHandler.DeserializeError` (`src/ActualLab.RestEase/Internal/RestEaseHttpMessageHandler.cs:39-41`)
  hands a hostile-server-controlled body to `TypeDecoratingTextSerializer`, which resolves an
  arbitrary assembly-qualified type name (`src/ActualLab.Core/Serialization/TypeDecoratingTextSerializer.cs:73`)
  *before* the `IsAssignableFrom` check at line 74. Because the target is the sealed struct
  `ExceptionInfo`, the only reachable effect is arbitrary type/assembly *load* (probing-path
  assembly load, static-ctor execution), not arbitrary object construction; the subsequent
  `ExceptionInfo.ToException()` is constrained to `Exception` subtypes with a `(string message)`
  ctor (`src/ActualLab.Core/Serialization/ExceptionInfo.cs:101-120`). Reported here only as a
  pointer — the resolution policy itself is P3's.

---

## Areas examined

Read in full:

- `src/ActualLab.Rpc.Server/`: `RpcWebSocketServer.cs`, `RpcWebSocketServerOptions.cs`,
  `RpcWebSocketServerBuilder.cs`, `RpcWebSocketServerDefaultDelegates.cs`, `RpcHttpServer.cs`,
  `RpcHttpServerOptions.cs`, `RpcHttpServerBuilder.cs`, `RpcHttpServerDefaultDelegates.cs`,
  `EndpointRouteBuilderExt.cs`, `RpcBuilderExt.cs`.
- `src/ActualLab.Rpc.Server.NetFx/`: `RpcWebSocketServer.cs`, `RpcWebSocketServerOptions.cs`,
  `RpcWebSocketServerBuilder.cs`, `RpcWebSocketServerDefaultDelegates.cs`,
  `EndpointRouteBuilderExt.cs`, `RpcBuilderExt.cs`, `HttpConfigurationExt.cs`, `AssemblyExt.cs`,
  `ServiceCollectionExt.cs`, `ServiceProviderExt.cs`, `Internal/DependencyResolver.cs`,
  `Internal/HttpActionContextExt.cs`.
- `src/ActualLab.Fusion.Server/`: `FusionWebServerBuilder.cs`, `FusionMvcWebServerBuilder.cs`,
  `FusionBuilderExt.cs`, `ServiceCollectionExt.cs`, `ApplicationBuilderExt.cs`,
  `EndpointRouteBuilderExt.cs`, `JsonifyErrorsAttribute.cs`,
  `Middlewares/SessionMiddleware.cs`, `Middlewares/HttpContextExtractors.cs`,
  `Rpc/RpcPeerOptionsExt.cs`, `Rpc/RpcOptionsExt.cs`, `Rpc/RpcDefaultSessionReplacer.cs`,
  `Rpc/SessionBoundRpcConnection.cs`, `Endpoints/RenderModeEndpoint.cs`,
  `Endpoints/RedirectUrlChecker.cs`, `Controllers/RenderModeController.cs`,
  `Internal/` (all: `SessionModelBinder`, `SymbolModelBinder`, `MomentModelBinder`,
  `TypeRefModelBinder`, `SimpleModelBinderProvider`, `HttpRequestExt`, `ControllerFilter`,
  `BlazorCircuitActivitySuppressor`, `FusionServerModuleInitializer`).
- `src/ActualLab.Fusion.Server.NetFx/`: all `.cs` files plus the `.csproj`.
- `src/ActualLab.RestEase/`: all `.cs` files (`RestEaseBuilder`, `ServiceCollectionExt`,
  `Internal/RestEaseHttpMessageHandler`, `…BuilderFilter`, `…RequestBodySerializer`,
  `…RequestQueryParamSerializer`, `…ResponseDeserializer`, `Internal/Errors`).

Read as supporting context (not reviewed as partition scope): `RpcHub`, `RpcPeer`,
`RpcServerPeer`, `RpcClientPeer`, `RpcRef`/`RpcRef.Static`/`RpcRefAddress`, `RpcHandshake`,
`RpcInboundContext`, `RpcInboundCall`, `RpcCallTrackers`, `RpcSystemCalls`/`RpcSystemCallSender`,
`RpcPeerOptions`, `RpcLimits`, `RpcSerializationFormatResolver`, `WebSocketOwner`,
`RpcWebSocketClientOptions`, `RpcHttpClientOptions`, `Session`, `SessionResolver`,
`ISessionValidator`, `ServerAuthHelper`, `AuthEndpoints`, `DbAuthService.*`, `ExceptionInfo`,
`TypeDecoratingTextSerializer`, `GuidExt`, `RenderModeDef`, `ts/packages/rpc/src/rpc-peer.ts`,
the four server-related regression test files under `tests/ActualLab.Fusion.Tests/Server/`,
`docs/PartAA-Server.md`, `docs/PartR-CO.md`, `docs/CHANGELOG.md`, `docs/plans/fusion-library-audit.md`,
and the `UseWebSockets` call sites in `samples/`.

## Areas NOT examined

- **No code was built or executed.** All findings are static; nothing was reproduced against a
  running server. F1's browser cookie-attachment behaviour and F4's takeover consequence would
  benefit from an empirical repro (a small ASP.NET Core host in a scratch worktree plus a raw
  WebSocket client), which I did not build within this pass.
- The generated/compiled artefacts under `src/ActualLab.RestEase/{bin,obj}` and
  `src/ActualLab.Rpc.Server/{bin,obj}` (build output, not source).
- `AssemblyAttributes.cs` and `README.md` files in the partition (no logic).
- Everything reached only as context above: the RPC transport/peer internals (P1), the call
  pipeline, method resolution and backend-service gating (P2), serializer internals (P3), the
  Fusion computed/state core (P5), the auth/EF/session-store implementations behind
  `ISessionValidator` and `IAuth` (P6). In particular I did **not** attempt to verify P2's
  guarantee that `MethodDef.IsBackend && !Peer.Ref.IsBackend` fully prevents backend-service
  dispatch on the public path — F1 and F4 assume it holds.
- ASP.NET Core / OWIN framework behaviour was reasoned about from documented semantics
  (`WebSocketOptions.AllowedOrigins` gating, `RedirectResultExecutor` vs `Results.Redirect`
  handling of `~/`, `WebSocket.CloseAsync`'s 123-byte description limit, `SingleOrDefault`
  throwing on two elements) rather than confirmed by running it.
