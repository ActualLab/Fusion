# Fusion robustness & correctness audit — brief (round 2)

You are one of the maintainers of **ActualLab.Fusion**, an open-source .NET +
TypeScript framework. We are doing a scheduled hardening pass over our own code
before a release. The goal is to find **real defects we should fix**: input
validation gaps, correctness bugs, concurrency bugs, resource leaks, and places
where the library trusts data it should validate.

This is ordinary defensive code review of our own library. Read the code, find
bugs, write them down with a suggested fix.

## Repository

- Root: `D:\Projects\ActualLab.Fusion` (Windows; `pwsh` available; `rg` available).
- C# sources: `src/*` (22 projects). TypeScript sources: `ts/packages/{core,rpc,fusion,fusion-rpc,fusion-react}`.
- Tests: `tests/*`. Docs: `docs/`, `CODING_STYLE.md`.

## What the library does

- **ActualLab.Rpc** — bidirectional RPC over WebSockets with its own framing,
  call multiplexing, reconnection, and a client-side result cache.
- **ActualLab.Fusion** — reactive caching / dependency tracking (`Computed`,
  `State`), invalidation propagation, compute services streamed over RPC.
- **Session / auth extensions** (`ActualLab.Fusion.Ext.*`, `Fusion.Blazor.*`) —
  session ids, authentication state, user/session stores backed by EF Core.
- **ActualLab.Core** — serialization (System.Text.Json / MemoryPack /
  MessagePack), collections, async primitives, text/`ByteString` handling.

## Which inputs are untrusted

The library is a network library, so a lot of its input comes from the other
side of a connection and must be validated defensively. Treat these as inputs
that may be malformed, out of range, out of order, or simply wrong:

1. Everything a remote peer sends over an RPC connection: handshake payloads,
   service/method names, call ids, argument buffers, headers, stream ids,
   keep-alive frames, and truncated or oversized versions of all of these.
2. HTTP requests reaching our server endpoints, including the WebSocket upgrade
   request: query string, headers, cookies, client id / session id values.
3. Session ids and auth tokens presented by clients.
4. Rows read back from the database or Redis that originated from client input.
5. On the client side (both .NET and TypeScript): everything the server sends —
   a buggy or misbehaving server should not be able to corrupt or unboundedly
   grow client state.

## What we want reported

**Robustness / input handling**
- Missing validation that lets a malformed message crash the process or wedge a
  connection permanently.
- Deserialization that resolves types from names supplied on the wire, or
  otherwise instantiates types the contract does not allow.
- Unbounded allocation driven by a length or count read from the wire.
- Collections keyed by remote-supplied values that grow without a bound or an
  eviction policy (memory exhaustion over time).
- Missing size/rate limits on messages, arguments, or in-flight calls.
- Error responses that include internal details (stack traces, internal type
  names, connection state) that should stay server-side.

**Access control correctness**
- Service/method resolution that could reach a service intended to be
  internal/backend-only, or a method that is not part of the client-facing
  contract. Verify the check exists and is correct — don't assume it.
- Session handling: id generation quality (RNG choice, entropy), validation,
  lifetime, and whether a client-supplied id is accepted without verification.
- Tenant/shard isolation in the EF layer.
- Raw SQL built by string concatenation or interpolation.
- Anywhere one client's cached results, invalidations, or session state could
  become visible to another client.

**Severe general bugs**
- Data races, torn state, missing memory barriers in lock-free code.
- Deadlocks, lock-ordering inversions, sync-over-async on hot paths.
- Resource leaks: undisposed `CancellationTokenSource` / registrations, leaked
  subscriptions, timers, channel readers, unbounded caches.
- Incorrect invalidation / cache coherence bugs that serve stale data.
- Exception handling that swallows cancellation or permanently breaks a peer.
- `async void`, unobserved task exceptions, fire-and-forget without a failure path.
- Integer overflow / off-by-one in buffer or index math; `Span`/`Memory`/`unsafe` misuse.
- Reconnection/handshake state-machine bugs that can wedge a peer permanently.
- Non-cryptographic RNG used where a security property is claimed.

**Do not report**
- Style, naming, formatting, missing XML docs or comments.
- Micro-optimizations with no correctness consequence.
- Issues with no reachable code path.
- "Add a null check" where the value provably cannot be null.
- Test-only code smells.

## Rules of engagement

1. **Verify before reporting.** Read the actual code and follow the call path
   from a public entry point to the defect. If you cannot show the path, either
   keep digging or mark the finding `PLAUSIBLE` and say what is unverified.
   Prefer 10 verified findings over 40 speculative ones.
2. **Do not modify the repository working tree**, except files under `tmp/`.
   Do not stage or commit anything. Do not run formatters.
3. **If you want to run an experiment or test**, either create a git worktree
   (`git worktree add ../ActualLab.Fusion-<name> -b review-r2-<name>` from the
   repo root) and work there, or create a small repro project under `tmp/` that
   references the latest published Fusion NuGet packages. Never build or edit
   the main working tree for an experiment.
4. Cite every finding as `path/to/File.cs:LINE` (repo-relative, forward slashes).
5. Be honest about confidence.

## Severity scale

- **CRITICAL** — remote code execution, authorization bypass, cross-user data
  exposure, remote crash of the whole server process, or silent data corruption.
- **HIGH** — unauthenticated resource exhaustion, data reaching the wrong peer,
  a race that corrupts shared state, a deadlock reachable in normal operation.
- **MEDIUM** — bug that breaks a feature or leaks resources over time, or a
  weakness that needs unusual preconditions.
- **LOW** — real but minor.

Report CRITICAL/HIGH/MEDIUM; include LOW only when clearly actionable.

## Output format

Write your report to the file named in your task prompt, one block per finding,
most severe first:

```
### F<n>. <short title>

- **Severity:** CRITICAL | HIGH | MEDIUM | LOW
- **Confidence:** CONFIRMED | PLAUSIBLE
- **Category:** access-control | deserialization | resource-exhaustion | race | leak | injection | info-leak | logic | crypto | ...
- **Location:** `src/Foo/Bar.cs:123`
- **What:** 1-3 sentences stating the defect precisely.
- **Why it matters / failure scenario:** concrete steps from a public entry
  point to the bad outcome.
- **Evidence:** the specific code that proves it (short quotes + line refs).
- **Fix:** the concrete change you would make.
```

End the file with `## Areas examined` (files/subsystems you actually read) and
`## Areas NOT examined` (what you skipped and why).

Finish by printing a short summary: counts by severity + one line per finding.
