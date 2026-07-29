# WASM crypto coverage for `Session.Sha256Hash`

Scope: does `System.Security.Cryptography.SHA256` (and, as a bound on the question,
`RandomNumberGenerator`) work under a WebAssembly host on **every** TFM that
`src/ActualLab.Fusion/ActualLab.Fusion.csproj:4` ships?

`ActualLab.Fusion` TFMs: `net10.0;net9.0;net8.0;net7.0;net6.0;net5.0;netcoreapp3.1;netstandard2.1;netstandard2.0`

Method: download the real browser-wasm runtime pack for each era, decompile the
crypto assembly with `ilspycmd`, and read the actual `SHA256` /
`RandomNumberGenerator` implementation that a browser app would load. Packs used
are listed in "Evidence" below; they live under `tmp/review-r2/packs/`.

## Per-TFM table

| TFM | WASM a reachable host? | `SHA256` available | `RandomNumberGenerator` available | Verdict |
|---|---|---|---|---|
| `net10.0` | Yes — Blazor WASM 10 | **Verified (binary)** — `SHAManagedHashProvider.SHA256ManagedImplementation` in `System.Security.Cryptography.dll` | **Verified (binary)** — `RandomNumberGeneratorImplementation` | OK |
| `net9.0` | Yes — Blazor WASM 9 | **Verified (binary)** — same | **Verified (binary)** — same | OK |
| `net8.0` | Yes — Blazor WASM 8 | **Verified (binary)** — same | **Verified (binary)** — same | OK |
| `net7.0` | Yes — Blazor WASM 7 | **Verified (binary)** — same | **Verified (binary)** — same | OK |
| `net6.0` | Yes — Blazor WASM 6 | **Verified (binary)** — `Internal.Cryptography.SHAHashProvider.SHA256ManagedImplementation`; `HashProviderDispenser.CreateHashProvider("SHA256")` returns it; **zero** `PlatformNotSupportedException` in `SHAHashProvider`; no `[UnsupportedOSPlatform("browser")]` on `SHA256` | **Verified (binary)** — `RandomNumberGenerator.Create()` → `RandomNumberGeneratorImplementation.s_singleton`; the `[UnsupportedOSPlatform("browser")]` sits only on the `Create(string)` overload, which Fusion does not call | OK |
| `net5.0` | Yes — Blazor WASM 5 | **Verified (binary)** — identical findings to net6.0 | **Verified (binary)** — `Create()` → `new RandomNumberGeneratorImplementation()`; same annotation split | OK |
| `netcoreapp3.1` | **No** — not a Blazor WASM app TFM (see note 1). Consumers are .NET Core 3.1 server / console / Blazor **Server** | n/a for WASM; full platform SHA-256 on desktop .NET Core 3.1 | n/a for WASM; full platform RNG | Moot — no WASM path |
| `netstandard2.1` | **Yes** — Blazor WASM **3.2** apps target `netstandard2.1` (verified), and `ActualLab.Fusion.Blazor`'s netstandard2.1 build explicitly references `Microsoft.AspNetCore.Components.WebAssembly` 3.2.0 | **Verified (binary)** — Mono 3.2 `mscorlib.dll`: `SHA256.Create()` returns `new SHA256Managed()` **directly** (no `CryptoConfig`, no reflection → no trimming hazard); `SHA256Managed` is a pure-managed C# SHA-256 | **Verified (binary + runtime symbols)** — `RandomNumberGenerator.Create()` → `RNGCryptoServiceProvider`, which is **not** managed: `[MethodImpl(InternalCall)]` `RngOpen`/`RngInitialize`/`RngGetBytes`. Verified `dotnet.wasm` contains `mono_rand_open`, `mono_rand_try_get_bytes` and all three `ves_icall_…RNGCryptoServiceProvider_Rng*` entries | OK — and SHA-256 is the *safer* of the two here |
| `netstandard2.0` | **Not via Blazor** (a Blazor 3.2 app prefers the netstandard2.1 asset). Primary consumers are `net472`/`net48` — **verified**. Only plausible WASM path is **Unity WebGL** at API level ".NET Standard 2.0" — **inferred, not verified** | Desktop .NET Framework: fine (verified by framework rules). Unity WebGL: **inferred** — Unity's mono/corefx-derived BCL ships a managed `SHA256`; **not verified against a Unity binary** | Same story: **inferred** for Unity WebGL, and already exercised today by `RandomStringGenerator` | OK for the verified consumers; Unity WebGL is unverified for **both** APIs equally |

### Note 1 — why `netcoreapp3.1` is not a WASM host
`Microsoft.AspNetCore.Components.WebAssembly` 3.2.0 ships **only** a `lib/netstandard2.1`
asset (verified by unpacking the nupkg; its single nuspec dependency group is
`.NETStandard2.1`). Blazor WASM 3.2 apps therefore target `netstandard2.1`. This is
verified from the package layout; I did not run the 3.2 SDK to prove no
`netcoreapp3.1` Blazor WASM app can be produced, so treat "impossible" as
strongly-supported inference rather than proof. It does not change the verdict:
`netcoreapp3.1`'s crypto is fine on every host it *can* run on.

### Note 2 — how the `netstandard2.0` ↔ `net472`/`net48` claim was verified
`ActualLab.Fusion.Server.NetFx` and `ActualLab.Rpc.Server.NetFx` target
`net48;net472` and project-reference `ActualLab.Fusion`; `tests/Directory.Build.props:7`
also targets `net48;net472`. .NET Framework cannot consume `netstandard2.1` by
design, so `netstandard2.0` is the only asset those consumers can bind to. The
earlier investigation's claim holds.

### Note 3 — `RandomNumberGenerator` vs `SHA256` are genuinely different stories
The task asked whether these two have the same availability story. **They do not**,
and the difference cuts in SHA-256's favour:

- On **net5.0–net10.0** browser-wasm, both are fully managed and neither is
  `[UnsupportedOSPlatform("browser")]`.
- On **Blazor 3.2 / Mono classic**, `SHA256` is *pure managed IL* (`SHA256Managed`,
  reached without `CryptoConfig`), whereas `RandomNumberGenerator` bottoms out in
  runtime **icalls** (`mono_rand_*`). SHA-256 is the more portable of the two;
  the RNG is the one that depends on the runtime implementing something.
- `SHA256Managed`'s constructor has exactly one throw path, gated on
  `CryptoConfig.AllowOnlyFipsAlgorithms` — which Mono hard-codes to `false`
  (`[MonoLimitation]`). Unreachable.

### Note 4 — `ActualLab.Core` already bounds the whole question
`src/ActualLab.Core/Generators/RandomStringGenerator.cs:18` declares
`public static readonly RandomStringGenerator Default = new();`, and the constructor
(line 34) calls `RandomNumberGenerator.Create()`. This runs in the type's **static
constructor**, on **every** TFM, unconditionally. Any host where crypto is
unavailable already fails at `ActualLab.Core` load — long before
`Session.Sha256Hash` is ever touched. `Session.Sha256Hash` therefore adds **no new
platform surface**; it is strictly less demanding than what Core already requires.

## Evidence (packs inspected)

| Era | Package | Version | Assembly read |
|---|---|---|---|
| net7–net10 | `microsoft.netcore.app.runtime.mono.browser-wasm` (local NuGet cache) | 7.0.20, 8.0.21, 9.0.13, 10.0.5 | `runtimes/browser-wasm/lib/<tfm>/System.Security.Cryptography.dll` |
| net6 | `microsoft.netcore.app.runtime.mono.browser-wasm` (downloaded) | 6.0.0, 6.0.36 | `runtimes/browser-wasm/lib/net6.0/System.Security.Cryptography.Algorithms.dll` |
| net5 | `microsoft.netcore.app.runtime.**browser-wasm**` (downloaded) | 5.0.0, 5.0.17 | `runtimes/browser-wasm/lib/net5.0/System.Security.Cryptography.Algorithms.dll` |
| Blazor 3.2 | `microsoft.aspnetcore.components.webassembly.runtime` (downloaded) | 3.2.0 | `tools/dotnetwasm/bcl/mscorlib.dll`, `tools/dotnetwasm/wasm/dotnet.wasm` |
| Blazor 3.2 | `microsoft.aspnetcore.components.webassembly` (downloaded) | 3.2.0 | nuspec / `lib/` layout |

Two facts worth recording, because they are why the earlier pass could not close
these gaps:

1. **There is no `microsoft.netcore.app.runtime.mono.browser-wasm` 5.x.** That
   package ID starts at `6.0.0-preview.4`. The .NET 5 browser runtime ships as
   `microsoft.netcore.app.runtime.browser-wasm` (no `.Mono.` segment), versions
   5.0.0–5.0.17.
2. **On net5/net6 the types are not in `System.Security.Cryptography.dll`.** That
   merged assembly only exists from net7 onward. On net5/net6 look in
   `System.Security.Cryptography.Algorithms.dll`. Searching for the merged name in
   a 5.x/6.x pack finds nothing and reads as a false negative.

Also note the type name changed: net5/net6 use
`Internal.Cryptography.SHAHashProvider.SHA256ManagedImplementation`; net7+ use
`System.Security.Cryptography.SHAManagedHashProvider.SHA256ManagedImplementation`.

## Recommendation

**Ship as is — no code change, no capability check, no fallback.** SHA-256 is
verified available, by direct inspection of the real runtime binary, on every TFM
where Fusion can actually run in WebAssembly: `net5.0` through `net10.0` (managed
`SHAHashProvider`, no `PlatformNotSupportedException`, no browser-unsupported
annotation on the API Fusion calls) and `netstandard2.1` (Mono 3.2's
`SHA256.Create()` returns a pure-managed `SHA256Managed` with no reflection, so it
survives trimming too). `netcoreapp3.1` has no WASM path at all, and
`netstandard2.0`'s verified consumers are `net472`/`net48`, where crypto is
trivially fine. The one genuinely unverified cell is Unity WebGL binding the
`netstandard2.0` asset — but that cell is unverified for `RandomNumberGenerator`
in exactly the same way, and `ActualLab.Core` has already taken that bet
unconditionally in a static initializer since long before `Sha256Hash` existed
(Note 4). So SHA-256 does not widen Fusion's platform requirements by one inch,
and a runtime capability check with a non-SHA fallback would be strictly worse:
it would let two peers compute different values for the same session, replacing a
loud, immediate, identical-on-every-platform failure with a silent divergence.
The existing comment at `src/ActualLab.Fusion/Session/Session.cs:177-178` is
accurate; if anything is worth adding, it is a one-line note that the claim is
verified down to net5.0 and Blazor 3.2 — not new code.

## HMACSHA256 coverage (follow-up)

> **Headline: `HMACSHA256` is NOT a drop-in equivalent of `SHA256` on WASM.**
> On **net5.0 and net6.0 browser-wasm it is a pure `PlatformNotSupportedException`
> stub** — the type is `[UnsupportedOSPlatform("browser")]` and *every* member,
> constructor included, throws. Managed HMAC on browser-wasm arrives in **.NET 7**.
> `SHA256` works on all of net5.0–net10.0; `HMACSHA256` works only on net7.0+.

Scope: does `System.Security.Cryptography.HMACSHA256` work under a WebAssembly host
on every TFM `ActualLab.Fusion` ships? Same method as above: unpack the real
browser-wasm runtime pack, decompile the crypto assembly, and read the code a
browser app actually loads. Two independent tools were used: `ilspycmd` 10.1.1
for the bodies, plus a purpose-built `System.Reflection.Metadata` reader
(`tmp/review-r2/mdtool/Program.cs`) for type/method custom attributes, so the
`[UnsupportedOSPlatform]` findings do not depend on the decompiler's rendering.

### Per-TFM table

| TFM | `HMACSHA256` exists | Managed? — MAC provider chain | `[UnsupportedOSPlatform("browser")]` | PNSE sites reachable from `new HMACSHA256(byte[])` / `ComputeHash` / `HashData` | Verdict |
|---|---|---|---|---|---|
| `net10.0` | **Verified (binary)** — `public class HMACSHA256 : HMAC` | **Verified (binary)** — ctor → `HMACCommon("SHA256", key, 64)` → `HashProviderDispenser.CreateMacProvider` → `HMACManagedHashProvider` → 2 × `HashProviderDispenser.CreateHashProvider` → `SHAManagedHashProvider` | **None** — verified at metadata level on the type and on all 21 methods | **0** | **OK** |
| `net9.0` | **Verified (binary)** — same | **Verified (binary)** — same chain | **None** | **0** | **OK** |
| `net8.0` | **Verified (binary)** — same | **Verified (binary)** — same chain | **None** | **0** | **OK** |
| `net7.0` | **Verified (binary)** — same | **Verified (binary)** — same chain (`HMACManagedHashProvider` lacks `Clone()`; otherwise identical) | **None** | **0** | **OK** |
| `net6.0` | Type exists, but as a **stub** | **NO** — `Internal.Cryptography.HashProviderDispenser.CreateMacProvider` is a one-line `throw new PlatformNotSupportedException(...)`. `HMACCommon.ChangeKeyImpl` calls it unconditionally | **YES — on the type** (`[UnsupportedOSPlatform("browser")]`) | **13** — `.ctor()`, `.ctor(byte[])`, `Key` get **and** set, `HashCore`×2, `HashFinal`, `TryHashFinal`, `Initialize`, `HashData`×3, `TryHashData`. Only `Dispose(bool)` is a no-op | **BROKEN** |
| `net5.0` | Type exists, but as a **stub** | **NO** — identical `CreateMacProvider` PNSE throw | **YES — on the type** | **9** (net5 has no `HashData`/`TryHashData` statics yet) | **BROKEN** |
| `netstandard2.1` (Blazor WASM 3.2 / Mono classic) | **Verified (binary)** — in `mscorlib.dll` (**not** `System.Core.dll`), `[ComVisible(true)] public class HMACSHA256 : HMAC` | **Verified (binary)** — `HMACSHA256(byte[] key)` sets `m_hashName = "SHA256"` and assigns `m_hash1 = new SHA256Managed(); m_hash2 = new SHA256Managed();` **directly**. **No `CryptoConfig`, no `HashAlgorithm.Create`, no reflection** → no trimming hazard. `HMAC.HashCore`/`HashFinal` are the textbook managed ipad/opad + `TransformBlock` implementation | n/a (attribute did not exist in that era) | **0** | **OK** |
| `netcoreapp3.1` | n/a — not a Blazor WASM app TFM (Note 1 above) | n/a | n/a | n/a | Moot — no WASM path |
| `netstandard2.0` | Not via Blazor (a 3.2 app binds the netstandard2.1 asset); verified consumers are `net472`/`net48` (Note 2 above) | Desktop .NET Framework: fine. Unity WebGL: **inferred, not verified** | n/a | n/a | OK for verified consumers |

### Note A — exactly what breaks on net5.0 / net6.0

Decompiled `System.Security.Cryptography.Algorithms.dll` from
`runtimes/browser-wasm/lib/net{5,6}.0/`:

```csharp
[UnsupportedOSPlatform("browser")]
public class HMACSHA256 : HMAC
{
    public HMACSHA256(byte[] key)
    {
        throw new PlatformNotSupportedException(SR.SystemSecurityCryptographyAlgorithms_PlatformNotSupported);
    }
    // ... every other member likewise
}
```

and the dispenser it would have used:

```csharp
internal static class HashProviderDispenser
{
    public static HashProvider CreateHashProvider(string hashAlgorithmId) => hashAlgorithmId switch {
        "SHA1" or "SHA256" or "SHA384" or "SHA512" => new SHAHashProvider(hashAlgorithmId),  // hashing works
        _ => throw new CryptographicException(...),
    };

    public static HashProvider CreateMacProvider(string hashAlgorithmId, ReadOnlySpan<byte> key)
        => throw new PlatformNotSupportedException(...);                                     // MAC does not
}
```

This is the same file that makes plain `SHA256` work — the split is deliberate:
.NET 5/6 browser-wasm shipped a managed **hash** provider but no managed **MAC**
provider. Verified on **four** packs so a servicing update cannot be blamed:
`5.0.0`, `5.0.17`, `6.0.0`, `6.0.36` — all four identical.

### Note B — what changed in .NET 7

.NET 7 added `System.Security.Cryptography.HMACManagedHashProvider`, and
`CreateMacProvider` stopped throwing:

```csharp
public static HashProvider CreateMacProvider(string hashAlgorithmId, ReadOnlySpan<byte> key) => hashAlgorithmId switch {
    "SHA1" or "SHA256" or "SHA384" or "SHA512" => new HMACManagedHashProvider(hashAlgorithmId, key),
    _ => throw new CryptographicException(...),
};
```

`HMACManagedHashProvider` is textbook managed HMAC — it holds two
`HashProvider`s obtained from `CreateHashProvider(...)` (i.e. two
`SHAManagedHashProvider` instances, **the very same managed SHA-256 core the
existing part of this document verified**) and XORs the key with the `0x36`/`0x5C`
pads itself. Verified identical at both ends of every band: `7.0.10`/`7.0.20`,
`8.0.0`/`8.0.21`, `9.0.0`/`9.0.13`, `10.0.0`/`10.0.5`.

### Note C — the base-class `HMAC` PNSE sites are not reachable

`net7.0+` `HMAC` (the abstract base) does contain 7 `PlatformNotSupportedException`
throws, but none is on the path Fusion would take:

- `HashCore`×2, `HashFinal`, `TryHashFinal`, `Initialize` — all five are `virtual`
  and **overridden** by `HMACSHA256` with real implementations.
- `HMAC.Create()` (obsolete, `SYSLIB0007`) and the `HashName` setter's
  *change-after-set* guard. `HMACSHA256`'s ctor sets `base.HashName = "SHA256"`
  exactly once, from `null`, so the guard (`_hashName != null && value != _hashName`)
  is false.
- `HMAC.Create(string)` routes through `CryptoConfig.CreateFromName<HMAC>` and is
  `[RequiresUnreferencedCode]`. **Do not use it** — it is the one trimming hazard
  in the area. `new HMACSHA256(key)` is not affected.

`LiteHashProvider` (used only by the `Stream` overloads of `HashData`) has 2 PNSE
sites — `CreateXof` and `CreateKmac`. `CreateHmac` returns a real `LiteHmac` that
wraps `CreateMacProvider`. Not a risk.

### Note D — Mono classic (Blazor 3.2) is fine, and better than its RNG

`mscorlib.dll` from `tools/dotnetwasm/bcl/`:

```csharp
public HMACSHA256(byte[] key)
{
    m_hashName = "SHA256";
    m_hash1 = new SHA256Managed();
    m_hash2 = new SHA256Managed();
    HashSizeValue = 256;
    InitializeKey(key);
}
```

`BlockSizeValue` is left at the base class's default of `64`, which is correct for
SHA-256. `CryptoConfig` appears in this era only in `HMAC.HashName`'s **setter**
(`m_hash1 = HashAlgorithm.Create(m_hashName)`) and in `HMAC.Create(string)` — the
`HMACSHA256` ctor writes the `m_hashName` **field** directly and never touches
either, so there is no reflection on the constructor path. `SHA256Managed`'s only
throw is the `CryptoConfig.AllowOnlyFipsAlgorithms` gate, which Mono hard-codes to
`false` (already established in Note 3 above). The parameterless `HMACSHA256()`
ctor does pull randomness (`Utils.GenerateRandom(64)` →
`StaticRandomNumberGenerator.GetBytes`, i.e. the `mono_rand_*` icalls) — use the
`HMACSHA256(byte[] key)` overload and that dependency disappears.

### Evidence (packs / assemblies inspected)

| Era | Package | Versions | Assembly read | Types decompiled |
|---|---|---|---|---|
| net7–net10 | `microsoft.netcore.app.runtime.mono.browser-wasm` (local NuGet cache) | 7.0.10, 7.0.20, 8.0.0, 8.0.21, 9.0.0, 9.0.13, 10.0.0, 10.0.5 | `runtimes/browser-wasm/lib/<tfm>/System.Security.Cryptography.dll` | `HMACSHA256`, `HMAC`, `HMACCommon`, `HashProviderDispenser`, `HMACManagedHashProvider`, `SHAManagedHashProvider`, `LiteHashProvider`, `LiteHmac` |
| net6 | `microsoft.netcore.app.runtime.mono.browser-wasm` | 6.0.0 (`packs/x600`), 6.0.36 (`packs/x636`) | `runtimes/browser-wasm/lib/net6.0/System.Security.Cryptography.Algorithms.dll` | `HMACSHA256`, `HMACCommon`, `Internal.Cryptography.HashProviderDispenser` |
| net5 | `microsoft.netcore.app.runtime.browser-wasm` (no `.Mono.` segment — see gotcha 1 above) | 5.0.0 (`packs/x500a`), 5.0.17 (`packs/x500`) | `runtimes/browser-wasm/lib/net5.0/System.Security.Cryptography.Algorithms.dll` | same |
| Blazor 3.2 | `microsoft.aspnetcore.components.webassembly.runtime` | 3.2.0 (`packs/x320`) | `tools/dotnetwasm/bcl/mscorlib.dll` | `HMACSHA256`, `HMAC`, `SHA256Managed`, `Utils` |

Decompiled sources and metadata dumps are kept under `tmp/review-r2/hmac/`.
The attribute reader is `tmp/review-r2/mdtool/` (control-tested: it correctly
reports the `[Obsolete]`/`[RequiresUnreferencedCode]` pair on
`RandomNumberGenerator.Create(string)`, and it *does* find
`UnsupportedOSPlatformAttribute` where present — which is exactly how the
net5/net6 `HMACSHA256` annotation was caught).

Gotcha worth adding to the two already recorded above:

3. **`HMACSHA256` lives in `mscorlib.dll` on Mono classic, not `System.Core.dll`.**
   On .NET Framework it is a `System.Core` type; searching `System.Core.dll` in the
   Blazor 3.2 BCL finds nothing and reads as a false negative. The other four BCL
   assemblies checked (`System.dll`, `System.Security.dll`, `Mono.Security.dll`,
   `System.Core.dll`) do not define it.

### Recommendation

**Do not use `System.Security.Cryptography.HMACSHA256` in code compiled for the
`net5.0` or `net6.0` TFMs of `ActualLab.Fusion` if that code can run in a browser.**
It is not a "might not be optimal" case — it is an unconditional
`PlatformNotSupportedException` from the constructor, so the failure is immediate
and total on Blazor WASM 5 and 6. `SHA256` does not have this problem, which is why
the earlier half of this document reached a clean verdict; do not generalize that
verdict to HMAC.

Practical options, in preference order:

1. **Compute HMAC-SHA256 from `SHA256` directly.** HMAC is ~20 lines over a hash
   primitive (`K' ⊕ 0x5C ‖ H(K' ⊕ 0x36 ‖ m)`), and `SHA256` is verified managed
   and browser-safe on **every** TFM in the list, net5.0 included — this is exactly
   what .NET 7's own `HMACManagedHashProvider` does. A `#if NET7_0_OR_GREATER`
   using the BCL `HMACSHA256` with this fallback below it costs little and keeps
   net5.0/net6.0 working.
2. **Restrict the API to `net7.0+`**, if the feature is genuinely optional, and
   let net5.0/net6.0 consumers not have it.
3. Drop the `net5.0`/`net6.0` TFMs (both are long past end of support). Out of
   scope for this note, but it is the option that makes the problem vanish.

Whatever is chosen, the `net7.0`–`net10.0` and Blazor-3.2/`netstandard2.1` paths
need **no** guard, capability check, or fallback: verified by direct inspection of
the real runtime binaries, `HMACSHA256` there is fully managed, unannotated, and
free of any reachable `PlatformNotSupportedException`. And never call
`HMAC.Create(string)` — it is the only genuinely trimming-hostile entry point in
this area on every modern TFM.
