# Fusion security & severe-bug review — shared brief (round 2)

## Repository

- Root: `D:\Projects\ActualLab.Fusion` (Windows; `pwsh` available; `rg` may be available).
- C# sources: `src/*` (22 projects). TypeScript sources: `ts/packages/{core,rpc,fusion,fusion-rpc,fusion-react}`.
- Tests: `tests/*`. Samples: sibling repo `D:\Projects\ActualLab.Fusion.Samples`.
- Docs: `docs/` (`docs/api-index.md`, `docs/api-index-full.md`), `CODING_STYLE.md`.

## What Fusion is (context for the threat model)

ActualLab.Fusion is a .NET + TypeScript framework providing:
- **ActualLab.Rpc** — a bidirectional RPC layer over WebSockets (also HTTP for
  some paths) with its own binary/text framing, call multiplexing, reconnection,
  and a client-side result cache.
- **ActualLab.Fusion** — reactive caching / dependency tracking (`Computed`,
  `State`), invalidation propagation, and "compute services" whose results are
  cached and streamed to clients over RPC.
- **Session / auth extensions** (`ActualLab.Fusion.Ext.*`, `Fusion.Blazor.*`) —
  session ids, authentication state, user/session stores backed by EF Core.
- **ActualLab.Core** — serialization (System.Text.Json / MemoryPack /
  MessagePack), collections, async primitives, text/`ByteString` handling.

## Threat model — what counts as attacker-controlled

Treat all of the following as untrusted, attacker-controlled input:

1. **Anything a remote peer sends over an RPC connection**: handshake payloads,
   service/method names, call ids, argument buffers, headers, stream ids,
   cancellation/keep-alive frames, WebSocket frames themselves (size, order,
   fragmentation, close codes), and malformed/truncated versions of all of these.
2. **HTTP requests** to `ActualLab.Rpc.Server` / `ActualLab.Fusion.Server`
   endpoints, including the WebSocket upgrade request: query string, headers,
   cookies, `Origin`, client id / session id values.
3. **Session ids and auth tokens** presented by clients.
4. **Data read back from a database or Redis** that a client could have
   influenced earlier (stored-payload attacks, e.g. operation log entries).
5. **On the client side** (TS and .NET clients): everything the server sends,
   because a compromised/hostile server or a MITM'd connection is in scope for
   client-side memory-safety / unbounded-growth / prototype-pollution issues.

A **backend/internal** service marked as such is still reachable if an
authorization check is missing or bypassable — verify the check, don't assume it.

## What to look for (in rough priority order)

**Security**
- Missing or bypassable authorization on RPC service/method resolution
  (e.g. a client peer being able to invoke a service intended to be
  backend-only, or to call a method not in the client-visible contract).
- Unsafe deserialization: type resolution from wire-supplied type names,
  polymorphic deserialization, arbitrary type instantiation, gadget surface.
- Session handling: predictable/low-entropy session ids, missing validation,
  session fixation, session ids logged or leaked in errors, comparisons that are
  not constant-time where it matters.
- Injection: SQL (raw SQL / string-concatenated queries in the EF layer),
  command injection, log injection that corrupts structured logs.
- Information disclosure: exception details, stack traces, internal type names,
  or connection secrets sent to remote peers.
- DoS / resource exhaustion reachable pre-authentication: unbounded buffers,
  unbounded collection growth keyed by attacker-supplied values, missing size
  limits on messages/arguments, algorithmic complexity attacks, unbounded
  concurrency, unbounded retry/reconnect loops.
- Missing origin/CSRF protection on the WebSocket upgrade or HTTP endpoints.
- Cryptography misuse: non-cryptographic RNG used for security-relevant values,
  weak hashing where a security property is claimed, fixed IVs/keys.
- Anything that lets one client observe another client's cached results,
  invalidations, or session state.

**Severe general bugs**
- Data races, torn state, lost updates, missing memory barriers on lock-free code.
- Deadlocks, lock-ordering inversions, sync-over-async on critical paths.
- Resource leaks: undisposed `CancellationTokenSource`/registrations, leaked
  subscriptions, leaked timers, leaked channel readers, unbounded caches.
- Incorrect invalidation / cache coherence bugs that can serve stale or
  cross-tenant data.
- Wrong exception handling that swallows cancellation or turns a transient error
  into a permanently broken peer/state.
- `async void`, unobserved task exceptions, fire-and-forget without a failure path.
- Integer overflow / off-by-one in buffer or index math; `Span`/`Memory`/`unsafe`
  misuse.
- Reconnection/handshake state machine bugs that can wedge a peer permanently.

**Explicitly out of scope (do not report)**
- Style, naming, formatting, missing XML docs, missing comments.
- Micro-optimizations with no correctness or DoS consequence.
- Purely hypothetical issues with no reachable path from the threat model above.
- "Add a null check" where the value provably cannot be null.
- Test-only code smells (unless the bug is in shipped code exercised by tests).

## Rules of engagement

1. **Verify before reporting.** Read the actual code. Follow the call path from
   an attacker-reachable entry point to the defect. If you cannot show the path,
   either dig until you can or mark the finding `PLAUSIBLE` and say what is
   unverified. Prefer 10 verified findings over 40 speculative ones.
2. **Do not modify the repository working tree**, except for files under
   `tmp/` . Do not stage or commit anything. Do not run formatters.
3. **If you want to run an experiment or test to verify something**, you must
   either:
   - create a **git worktree** to play in
     (`git worktree add ../ActualLab.Fusion-<yourname> -b review-r2-<yourname>`
     run from the repo root), and do all builds/edits there; or
   - create a **mini repro project under `tmp/`** that references the **latest
     published Fusion NuGet packages** (`ActualLab.Core`, `ActualLab.Rpc`,
     `ActualLab.Fusion`, …) rather than project references.
   Never build/modify the main working tree for an experiment.
4. Cite every finding as `path/to/File.cs:LINE` (repo-relative, forward slashes).
5. Be honest about confidence. A wrong "critical" finding costs more than a
   missed medium one.

## Severity scale

- **CRITICAL** — remote code execution, authentication/authorization bypass,
  cross-tenant/cross-user data exposure, remote crash of the whole server
  process, or silent data corruption.
- **HIGH** — pre-auth DoS of a server, leak of sensitive data to the wrong peer,
  a race that corrupts shared state, a deadlock reachable in normal operation.
- **MEDIUM** — bug that breaks a feature or leaks resources over time, or a
  security weakness that needs unusual preconditions.
- **LOW** — real but minor; include only if genuinely worth fixing.

Report CRITICAL/HIGH/MEDIUM. Include LOW only when it is clearly actionable.

## Output format

Write your report to the file assigned in your task prompt, using exactly this
structure, one block per finding, most severe first:

```
### F<n>. <short title>

- **Severity:** CRITICAL | HIGH | MEDIUM | LOW
- **Confidence:** CONFIRMED | PLAUSIBLE
- **Category:** auth-bypass | deserialization | dos | race | leak | injection | info-leak | logic | crypto | ...
- **Location:** `src/Foo/Bar.cs:123` (add more locations if relevant)
- **What:** 1-3 sentences stating the defect precisely.
- **Why it matters / attack path:** concrete steps from an attacker-reachable
  entry point (or a concrete failure scenario for non-security bugs).
- **Evidence:** the specific code that proves it (short quotes + line refs).
- **Fix:** the concrete change you would make.
```

End the file with a `## Areas examined` section listing the files/subsystems you
actually read, and a `## Areas NOT examined` section listing what you skipped and
why — this is used to decide whether another review pass is needed.

Also print a short summary (finding count by severity + one line each) as your
final message.
