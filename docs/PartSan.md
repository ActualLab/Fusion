# Sanitization: Secrets in Logs

A session id in a log line, an API key in an exception message, a bearer token in a query string —
each of these is a real leak, and each of them survives in a log aggregator far longer than the
secret itself stays valid. The types in `ActualLab.Compliance` exist to fix that one problem:
**keep a secret out of logs and error messages without keeping it out of the wire.**

That last part is what makes the approach cheap to adopt. `SanitizedString<TSanitizer>` is
wire-compatible with `string` in every serialization format ActualLab supports, so changing a
member's type from `string` to `SanitizedString<T>` is not a wire change and needs no migration.

## Required Package

| Package | Purpose |
|---------|---------|
| [ActualLab.Core](https://www.nuget.org/packages/ActualLab.Core/) | The whole sanitization framework (`ActualLab.Compliance`) |

## SanitizedString&lt;TSanitizer&gt;

Pick a sanitizer, use it as the type argument, and the value masks itself whenever anything
renders it:

<!-- snippet: PartSan_Declare -->
```cs
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
public sealed partial record ApiCredentials(
    [property: DataMember(Order = 0), MemoryPackOrder(0), Key(0)] string UserId,
    [property: DataMember(Order = 1), MemoryPackOrder(1), Key(1)]
    [property: MemoryPackAllowSerialize] // MemoryPack-only: it can't see the registered formatter
    SanitizedString<Sanitizers.PrefixAndLengthHint> ApiKey
);
```
<!-- endSnippet -->

<!-- snippet: PartSan_Render -->
```cs
WriteLine($"Authenticating {credentials.UserId} with {credentials.ApiKey}");
WriteLine(credentials.ApiKey.Value); // .Value is the raw one, and it's the only way to get it
```
<!-- endSnippet -->

```
Authenticating alice with <<sk* [16-31]>>
sk-7f3a91b8c2e4d6f0
```

The masking lives entirely in `ToString()`. Everything else about the type is deliberately
transparent:

| Member | Behavior |
|--------|----------|
| `ToString()` | The masked form — unless [sanitization is suspended](#suspending-sanitization) |
| `Value` | The raw string, always |
| `implicit operator SanitizedString<T>(string?)` | Assign a `string` and it just works |
| `explicit operator string(...)` | **Explicit** on purpose, so the raw value can't slip into an interpolation by accident |
| `Equals` / `GetHashCode` / `==` | Compare the **raw** values, so two equal secrets stay equal regardless of masking |
| `IsEmpty`, `Empty` | `null` and `""` are the same thing, and no sanitizer masks an empty value |

Equality on the raw value matters more than it looks: two different secrets can mask to identical
text, and a lookup keyed on the masked form would collide.

<!-- snippet: PartSan_Equality -->
```cs
var a = new SanitizedString<Sanitizers.LengthHint>("hunter2-hunter2");
var b = new SanitizedString<Sanitizers.LengthHint>("abcdefghijklmno"); // Same length, same mask
WriteLine($"{a} == {b}: {a == b}");
```
<!-- endSnippet -->

```
<<* [8-15]>> == <<* [8-15]>>: False
```

## Wire Compatibility

**Serialization always carries the raw value.** Masking is a rendering concern, and a masked value
on the wire would be silent data loss — the secret would be gone, not hidden.

Every format ships a converter that reads and writes the raw string, byte for byte identical to
what a plain `string` member produces:

| Format | Converter |
|--------|-----------|
| System.Text.Json | `SanitizedStringJsonConverter` |
| Newtonsoft.Json | `SanitizedStringNewtonsoftJsonConverter` |
| MemoryPack | `SanitizedStringMemoryPackFormatter` |
| MessagePack | `SanitizedStringMessagePackFormatter<>` |
| Nerdbank.MessagePack | `SanitizedStringNerdbankConverter<>` |
| `TypeConverter` (config binding, ASP.NET model binding, …) | `SanitizedStringTypeConverter` |

So the JSON for the record above still contains the key:

<!-- snippet: PartSan_Wire -->
```cs
WriteLine(SystemJsonSerializer.Default.Write(credentials));
```
<!-- endSnippet -->

```json
{"userId":"alice","apiKey":"sk-7f3a91b8c2e4d6f0"}
```

…and the binary payload is bit-identical to the one produced by the same record declared with a
plain `string`:

<!-- snippet: PartSan_DeclareBefore -->
```cs
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
public sealed partial record PlainCredentials(
    [property: DataMember(Order = 0), MemoryPackOrder(0), Key(0)] string UserId,
    [property: DataMember(Order = 1), MemoryPackOrder(1), Key(1)] string ApiKey
);
```
<!-- endSnippet -->

<!-- snippet: PartSan_WireCompatible -->
```cs
var plain = new PlainCredentials("alice", ApiKey);
foreach (var serializer in (IByteSerializer[])[MemoryPackByteSerializer.Default, MessagePackByteSerializer.Default]) {
    using var plainBytes = serializer.Write(plain);
    using var sanitizedBytes = serializer.Write(credentials);
    var isSame = plainBytes.WrittenSpan.SequenceEqual(sanitizedBytes.WrittenSpan);
    WriteLine($"{serializer.GetType().Name}: {isSame}");
}
```
<!-- endSnippet -->

```
MemoryPackByteSerializer: True
MessagePackByteSerializer: True
```

::: warning This protects logs, not sinks that serialize
`SanitizedString<T>` stops a secret from reaching a log line, an exception message, or a
`ToString()`-based diagnostic. It does **nothing** about a sink that serializes — an RPC call, a
database column, a JSON audit trail, an outbound webhook all receive the raw value. That's by
design; if a value must not reach a particular sink, don't send it there.
:::

The one place the substitution isn't free is MemoryPack: its source generator only accepts member
types it can see a `[MemoryPackable]` annotation on, and `SanitizedString<T>`'s formatter is
registered at runtime instead. Add `[MemoryPackAllowSerialize]` to the member (as in the snippet
above) and MemoryPack defers to the registered formatter. Nothing about the payload changes.

## The Sanitizers

Each sanitizer is named after **what it leaves visible** — that's the only thing distinguishing
them. All of them pass an empty value through unchanged.

| Sanitizer | Leaves visible | `sk-7f3a91b8c2e4d6f0` renders as |
|-----------|----------------|----------------------------------|
| `Sanitizers.Hidden` | Nothing | `<<hidden>>` |
| `Sanitizers.LengthHint` | A length bucket | `<<* [16-31]>>` |
| `Sanitizers.PrefixAndLengthHint` | First 2 chars + a length bucket | `<<sk* [16-31]>>` |
| `Sanitizers.Fingerprint` | Identity, not content | `<<2e513fef>>` |
| `Sanitizers.SessionString` | A 4-char prefix + a hash, in `Session.ToString()`'s format | `Ab3f:297ccba4` |
| `Sanitizers.UriQuery` | Everything except the parameters its policy names | see below |
| `Sanitizers.RpcRequestQuery` | Same, with ActualLab's RPC endpoint policy baked in | see below |

<!-- snippet: PartSan_Sanitizers -->
```cs
WriteLine(Sanitizer.Sanitize<Sanitizers.Hidden>(ApiKey));
WriteLine(Sanitizer.Sanitize<Sanitizers.LengthHint>(ApiKey));
WriteLine(Sanitizer.Sanitize<Sanitizers.PrefixAndLengthHint>(ApiKey));
WriteLine(Sanitizer.Sanitize<Sanitizers.Fingerprint>(ApiKey));
WriteLine(Sanitizer.Sanitize<Sanitizers.SessionString>("Ab3fSomeLongSessionId"));
WriteLine(Sanitizer.Sanitize<Sanitizers.RpcRequestQuery>("?s=Ab3fSomeLongSessionId&p=Zm9vYmFy&f=mempack6"));
```
<!-- endSnippet -->

```
<<hidden>>
<<* [16-31]>>
<<sk* [16-31]>>
<<2e513fef>>
Ab3f:297ccba4
?s=Ab3f:297ccba4&p=<<hidden>>&f=mempack6
```

`PrefixAndLengthHint` falls back to `LengthHint` for values of 3 characters or fewer — a 2-character
prefix of a 3-character secret isn't a hint, it's the secret.

### Length hints are buckets, not lengths

`[16-31]` is a power-of-two bucket, not a range someone chose. An exact length is itself a
distinguisher — it narrows a candidate set, and for short structured values it can identify the
value outright — so the exact length never leaks.

### Fingerprint is the odd one out

Every other sanitizer throws information away. `Fingerprint` throws away the content but keeps the
identity: equal values produce equal output.

<!-- snippet: PartSan_Fingerprint -->
```cs
WriteLine(Sanitizer.Sanitize<Sanitizers.Fingerprint>("alice@example.com"));
WriteLine(Sanitizer.Sanitize<Sanitizers.Fingerprint>("alice@example.com")); // Same input, same output
WriteLine(Sanitizer.Sanitize<Sanitizers.Fingerprint>("bob@example.com"));
```
<!-- endSnippet -->

```
<<05125fe6>>
<<05125fe6>>
<<925de7fc>>
```

That's exactly what you want when you need to follow one value across a log — "the same client id
appears in all three of these failures" is a question no other sanitizer can answer.

::: warning Fingerprint is not a redaction
It's a short, non-cryptographic hash of the value. Anyone holding a list of candidate values can
hash them and match. Use it for high-entropy identifiers — session ids, client ids, tokens — and
never for anything a reader could plausibly enumerate: email addresses, phone numbers, account
numbers, postcodes.
:::

### Query strings

`UriQuery` sanitizes a URL query parameter by parameter: its selector maps a parameter name to the
`Sanitizer` for that parameter's value, or to `null` to leave it alone. Parameter names are matched
by the policy, valueless parameters are passed through, and the leading `?` is optional.

`RpcRequestQuery` is `UriQuery` with the policy for ActualLab's own RPC endpoints — `s`/`session`
via `SessionString`, `clientId` via `Fingerprint`, `p` (the reconnect proof) via `Hidden`, and
everything else readable. Each of those name lists is a settable static property, so an app can
extend the policy.

## Turning Sanitization On

Sanitization is **suspended unless a scope turns it on**, and `Sanitization` is the ambient switch:

| Member | Meaning |
|--------|---------|
| `Sanitization.IsSuspended` | Whether masking is currently off — `true` by default |
| `Sanitization.Begin()` | A scope in which values render masked |
| `Sanitization.Suspend()` | A scope that turns masking back off — nestable inside `Begin()` |
| `Sanitization.IsGloballySuspended` | The process-wide default, `true`; a thread scope overrides it either way |

<!-- snippet: PartSan_Suspend -->
```cs
WriteLine(credentials.ApiKey.ToString());
using (Sanitization.Begin())
    WriteLine(credentials.ApiKey.ToString());
WriteLine(credentials.ApiKey.ToString());
```
<!-- endSnippet -->

```
sk-7f3a91b8c2e4d6f0
<<sk* [16-31]>>
sk-7f3a91b8c2e4d6f0
```

::: danger Why off by default
A masking member is usually written as `get => Sanitizer.MaybeSanitize<T>(field)` — and a
**serializer reads that same getter**. Were masking on by default, the masked form would be what
lands on the wire and in the database: silent data loss no compiler can catch. So masking is off
until something asks for it, and the thing that asks is [`SanitizingLogger`](#sanitizinglogger),
which opens a `Begin()` scope around each log call — on the logging thread only.
:::

::: warning A scope does not flow across `await`
The flag is `[ThreadStatic]`, not `AsyncLocal`, because it's read on every `ToString()` of every
sanitized value and an `AsyncLocal` read is far too expensive for that path. The consequence:
don't wrap awaited work in a `Begin()` scope — the continuation may resume on another thread,
where the scope was never set. Keep the scope around the synchronous rendering itself.
:::

## SanitizingLogger

`SanitizingLogger` wraps an `ILogger` and opens a `Sanitization.Begin()` scope around every
`Log(...)` call, so `ISanitized` values mask themselves in the log and nowhere else.
`SanitizingLoggerFactory` applies it to every logger a factory creates, and
`AddSanitizingLoggerFactory()` registers that in DI:

```cs
services.AddLogging(logging => logging.AddSanitizingLoggerFactory(
    c => c.GetRequiredService<HostInfo>().IsProductionInstance));
```

::: warning Sanitize lazily, or not at all
The scope only covers what is rendered **inside** the log call. `Log.LogInformation("{Query}",
Sanitizer.MaybeSanitize<T>(query))` evaluates its argument first, outside the scope, so it logs
the raw value. Pass something whose `ToString()` is deferred — a `SanitizedString<T>` or an
`ISanitized` — or mask unconditionally with `Sanitizer.Sanitize<T>` when the value only ever
reaches a log.
:::

## Sanitizer: Masking Without Wrapping

Not every value wants to be a `SanitizedString<T>`. When a type already holds a plain `string` and
only one computed member needs masking, use `Sanitizer` directly:

| API | Applies the sanitizer… |
|-----|------------------------|
| `sanitizer.Apply(value)` | Always |
| `sanitizer.MaybeApply(value)` | Only when sanitization isn't suspended |
| `Sanitizer.Sanitize<T>(value)` | Always |
| `Sanitizer.MaybeSanitize<T>(value)` | Only when sanitization isn't suspended |
| `Sanitizer.Get<T>()` / `Sanitizer.Get(Type)` | — returns the single shared instance |

`Get<T>()` and `Get(Type)` hand out the same instance, so two call sites can't end up with
different policy. Sanitizers must be stateless and thread-safe.

`MaybeSanitize<T>` is the one to reach for in a getter or a `ToString()` — it honors `Suspend()`
without any wrapper type:

<!-- snippet: PartSan_MaybeSanitize -->
```cs
public sealed class ConnectRequest(string path, string query)
{
    public string Path { get; } = path;
    public string Query { get; } = query;
    // Computed, never serialized - so masking right in the getter is safe
    public string LogTitle => Path + Sanitizer.MaybeSanitize<Sanitizers.RpcRequestQuery>(Query);
}
```
<!-- endSnippet -->

<!-- snippet: PartSan_MaybeSanitize_Use -->
```cs
var request = new ConnectRequest("/rpc/ws", "?s=Ab3fSomeLongSessionId&f=mempack6");
WriteLine(request.LogTitle);
using (Sanitization.Suspend())
    WriteLine(request.LogTitle); // MaybeSanitize honors the scope, Sanitize wouldn't
```
<!-- endSnippet -->

```
/rpc/ws?s=Ab3f:297ccba4&f=mempack6
/rpc/ws?s=Ab3fSomeLongSessionId&f=mempack6
```

### Don't do this in a serialized member

The reason `SanitizedString<T>` exists at all is that the obvious shortcut is a data-loss bug:

<!-- snippet: PartSan_DontMaskInGetter -->
```cs
[DataContract]
public sealed record BadCredentials
{
    private readonly string _apiKey = "";

    // The masked value is what gets serialized, stored, and sent - the secret is simply lost
    [DataMember(Order = 0)]
    public string ApiKey {
        get => Sanitizer.Sanitize<Sanitizers.Hidden>(_apiKey);
        init => _apiKey = value;
    }
}
```
<!-- endSnippet -->

<!-- snippet: PartSan_DontMaskInGetter_Use -->
```cs
var bad = new BadCredentials { ApiKey = ApiKey };
WriteLine(SystemJsonSerializer.Default.Write(bad));
```
<!-- endSnippet -->

```json
{"apiKey":"<<hidden>>"}
```

The mask went on the wire and into the database; the key is gone. `SanitizedString<T>` makes this
impossible — its converters read `Value` and never call `ToString()`. **If a member is serialized,
never apply a sanitizer in its getter.**

## Writing Your Own

Derive from `Sanitizer`, implement `Apply`, and name the type after what it leaves visible. Keep
it stateless — a single instance is shared process-wide. A sanitizer with a parameterless
constructor can be a `SanitizedString<>` type argument:

<!-- snippet: PartSan_CustomSanitizer -->
```cs
public sealed class EmailDomain : Sanitizer
{
    public override string Apply(string value)
    {
        var atIndex = value.IndexOf('@', StringComparison.Ordinal);
        return atIndex < 0
            ? Sanitize<Sanitizers.Hidden>(value)
            : Sanitizers.HiddenValue + value[atIndex..];
    }
}
```
<!-- endSnippet -->

`UriQuery` carries its policy delegate as a constructor argument, so it has no parameterless
constructor and **can't** be a `SanitizedString<>` type argument. Derive from it with a fixed
policy — exactly what `RpcRequestQuery` does — and you get one that can:

<!-- snippet: PartSan_UriQueryPolicy -->
```cs
public sealed class DownloadQuery() : Sanitizers.UriQuery(SelectSanitizer)
{
    private static Sanitizer? SelectSanitizer(string name)
        => string.Equals(name, "token", StringComparison.OrdinalIgnoreCase)
            ? Get<Sanitizers.Hidden>()
            : null;
}
```
<!-- endSnippet -->

<!-- snippet: PartSan_CustomSanitizer_Use -->
```cs
WriteLine(Sanitizer.Sanitize<EmailDomain>("alice@example.com"));
WriteLine(new SanitizedString<EmailDomain>("alice@example.com").ToString());
WriteLine(Sanitizer.Sanitize<DownloadQuery>("?id=17&token=Zm9vYmFy"));
```
<!-- endSnippet -->

```
<<hidden>>@example.com
<<hidden>>@example.com
?id=17&token=<<hidden>>
```

## Tagging Interfaces

Two interfaces let infrastructure recognize sanitized values without knowing their sanitizer:

| Interface | Meaning |
|-----------|---------|
| `ISanitized` | This type's `ToString()` takes `Sanitization.IsSuspended` into account |
| `ISanitizedString` | `ISanitized` + `Value` and `Sanitizer` — the non-generic face of `SanitizedString<T>`, for serializers and log formatters |

## Choosing a Sanitizer

| If the value is… | Use |
|------------------|-----|
| A secret you never need to recognize again | `Hidden` |
| A secret where "was it set? was it truncated?" matters | `LengthHint` |
| A prefixed credential (`sk-…`, `ghp_…`) whose kind is worth seeing | `PrefixAndLengthHint` |
| A high-entropy identifier you need to correlate across log records | `Fingerprint` |
| A Fusion session id | `SessionString` |
| A URL query string | `UriQuery` / `RpcRequestQuery` |
| Low-entropy PII (email, phone, account number) | `Hidden` — never `Fingerprint` |
