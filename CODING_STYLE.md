# Coding Style Guide

This document describes the coding conventions used in ActualLab.Fusion project that differ from standard .NET conventions.

## General Principles

- The coding style documented here takes precedence over standard .NET conventions, so...
- Follow .NET and C# best practices for code style and structure, BUT if you see a different convention is used here or in the existing source code, stick to it.
- All modern C# language features are preferred over the legacy ones. In particular:
  - Use file-scoped namespaces
  - Use pattern matching
  - Use record types and default constructors
  - Use expression-bodied members
  - Use field-backed auto-properties and field keyword
  - Use nullable reference types
  - Use var instead of explicit types
  - etc.
- When in Doubt, examine existing code in the same area and match its style.

## Regular comments, docstrings, XML documentation comments

This section applies to **C# and TypeScript** equally. Claude has a strong
tendency to over-comment; read this section before writing a single comment,
docstring, or XML doc. The rules here are deliberately strict.

### Philosophy — when to write a comment at all

**Default to no comments.** Code is the single source of truth. Names, types,
and structure should carry the meaning; a comment that merely restates what
the code already says doesn't add information — it doubles the reading load
and goes stale the moment the code changes underneath it. Stale comments are
worse than missing ones, because both Claude and human readers may trust them
over the code.

**Write a comment only when something is not straightforward** to a reasonably
experienced developer reading at normal pace (assume "senior, but not
extremely senior" — competent but skimming, not studying). The mental test:
imagine that reader going through the file fast. If the comment wastes their
time because what follows is obvious, drop it. If it saves them time
understanding a non-obvious invariant, constraint, workaround, or subtlety
they'd likely miss on a quick read, keep it. A comment roughly doubles the
text the reader processes for that spot — it has to earn that cost.

**Don't document members by default.** Typically document the *class* (or
module/file in TypeScript) when its purpose isn't obvious from the name. For
an individual member, add a note only when its behavior is unusual: a hidden
side effect, a non-obvious precondition, surprising return semantics, a
workaround for a specific bug. If you find yourself writing a page of docs on
a single method, the method is wrong — rename it, split it, or rework its
parameters until the signature carries the meaning.

**For methods specifically:** the method name plus parameter names should
explain what it does. Reach for a comment only when they can't, and only for
the part the signature doesn't already carry.

### Types (class, struct, record, interface, enum, delegate, including nested)

- DO write a `/// <summary>` XML doc when the type's purpose isn't obvious
  from its name.
- Keep it short: **5 lines maximum, 3 lines ideal.** If a type doc keeps
  growing, split the type — don't keep writing.
- Use `<see cref="..."/>` for cross-references.

### Members (methods, properties, fields, events)

- **Do NOT write `/// <summary>` XML docs on members.** Ever. This is stricter
  than the default .NET guidance. `///` on members bloats IntelliSense with
  prose that ages faster than the signature.
- If a member genuinely needs explanation (per the philosophy above), use a
  regular `//` comment.
  - **C#**: put the comment at the **top of the method body** (inside the
    braces).
  - **TypeScript**: put the comment **above the method declaration**.
- If the name already explains what the method does, **omit the comment** —
  don't restate the signature in English.
- Keep comments short: a single line is almost always enough. Prefer a useful
  one-liner over a paragraph.

### Placement order for a type (top to bottom)

1. Regular `//` comment (optional, extra context not suitable for API docs)
2. Empty line (if regular comment is present)
3. `/// <summary>` XML documentation
4. `#pragma` directives (if any)
5. Attributes
6. Type declaration

Example — type doc:
```csharp
// This type is used as an extra parameter of constructors to indicate newly generated Id required

/// <summary>
/// A unit-type constructor parameter indicating that a new identifier should be generated.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Generate : IEquatable<Generate>
```

Example — type doc with `<see cref="..."/>`:
```csharp
/// <summary>
/// A thread-safe object pool backed by a <see cref="ConcurrentQueue{T}"/>
/// and a <see cref="StochasticCounter"/> for approximate size tracking.
/// </summary>
public class ConcurrentPool<T> : IPool<T>
```

Example — C# method comment (inside the body):
```csharp
public Task<bool> SwitchFacing(CancellationToken cancellationToken)
{
    // Clears _deviceId so the next state-sync SwitchCamera (which may carry
    // a stale deviceId from LocalAppSettings) doesn't no-op.
    _deviceId = "";
    return _jsRef.InvokeAsync<bool>("switchFacing", cancellationToken).AsTask();
}
```

Example — TypeScript method comment (above the declaration):
```ts
// Flips front/back by facingMode so the browser picks the primary lens per facing.
public async switchFacing(): Promise<boolean> {
    ...
}
```

## Key Differences from Default .NET Conventions


### File Organization

#### File placement:
- `src/` for the source code of ActualLab.Fusion projects
- `samples/` for sample apps
- `tests/` for test projects
- `docs/` for documentation
  
#### Line Lengths and Indentation:
- **Maximum line length**: **120 characters**
- **Line endings**: use **LF** (`\n`) for all files (not CRLF)
- **Indent sizes**:
    - **4 spaces** for C#, TypeScript, and CSS code
    - **2 spaces** for XML, JSON, YAML, and project files (instead of 4).

#### Method Parameters and Arguments Formatting:
- Maximum **4 formal parameters** on a single line (more restrictive than default)
- Maximum **6 invocation arguments** on a single line (more restrictive than default).

#### Attribute Formatting:
- Maximum attribute length for the same line: **70 characters** (more restrictive than default)
- Place field attributes on separate lines
- Place accessor holder attributes on separate lines (unless the owner is single-line).

#### Multi-targeting
- Follow the project's multi-targeting patterns with conditional compilation.

### Global Usings

`Directory.Build.props` files may define some global usings, such as:

```xml
<Using Include="ActualLab" />
<Using Include="ActualLab.Api" />
<Using Include="ActualLab.Async" />
```

Search for `<Using>` to get the full list. Avoid adding explicit usings for global usings.

### Naming Conventions

- **Private static readonly fields and constants**: use PascalCase (`ReadonlyField`)
- **All other private fields, including static ones**: use underscore prefix with camelCase (`_fieldName`)
- **Async method suffix**: Do NOT use `Async` suffix for async methods.
  The only exception is slow-path async methods inside other async methods
  (e.g., `CompleteAsync` inside `Write` method that handles the case
  when the operation cannot complete synchronously).

### Braces and Formatting

**Mixed brace style** that differs from consistent Allman or K&R:
- **Classes, methods, constructors**: opening brace on **next line** (Allman style)
- **Everything else**: opening brace on **same line** (K&R style)
- **Any razor code**: opening brace on **same line** (K&R style).

So in particular, the opening brace must be on **same line** (K&R style) for the following:
- Properties, accessors, local methods, anonymous methods
- If blocks, case blocks, and all other blocks that could be used inside method bodies

Example:
```csharp
// Method - brace on next line
public void MethodName()
{
    // method body
}

// Property - brace on same line
public string PropertyName {
    get => _field;
    set => _field = value;
}

// Anonymous method - brace on same line
var action = () => {
    // body
};
```

### Blank Lines

More restrictive than default:
- **0 blank lines** inside namespaces (default allows 1)
- **0 blank lines** inside types (default allows 1)
- **0 blank lines** around single-line properties, fields, and methods
- Keep maximum **1 blank line** in code (default allows more)
- A blank line typically follows any `return`, `break`, `continue`, `yield return`,
  or `yield break` statement — i.e. any block-escaping statement — unless it's on
  the very last line of the enclosing statement block.
- Methods whose body ends with one or more **local functions** typically have an
  explicit `return;` right before the first local function, followed by a blank
  line. This marks where the method's actual execution ends and makes the
  local-function section unambiguous to the reader.

Example:
```csharp
protected override async Task OnRun(CancellationToken cancellationToken)
{
    // ... main body ...
    return;

    void Helper() {
        // ...
    }
}
```

### Code Style Preferences

- **Expression-bodied members**: preferred for **all member types**
  including methods and constructors (default only suggests for properties/accessors).
  The `=>` arrow for one-line methods should be on the same line as return expression,
  and it's preferred to move it to the dedicated line for class method bodies,
  but not for property accessors.
- **Braces for single statements** are not required,
  typically they're used only if the statement is prefixed with a comment,
  or when it significantly improves the readability.

### Using Directives

- Place using directives **outside namespace** (C# 10+ default is inside).

### Member Ordering

Members within a class should be ordered as follows:

1. **Settings-style nested type**, if any.
   The instance of this type is passed to every constructor.
   Other nested types are placed at the very end of the class.
2. **Static fields** (public readonly, then public, then private)
3. **Instance fields** (private, then internal)
4. **Instance properties and public fields** ()
    - Private, then protected properties - typically they are DI injected
    - Public properties and fields are located closer to the constructor
5. Lazy style is often preferred for DI-injected properties,
   especially in the UI-related code.
   Use `=> field ??= Services.GetRequiredService<T>()`.
6. **Constructor-like static NewXxx-style methods**
7. **Constructors** (public, then private),
   though primary constructors are preferred.
8. **Public methods**, ordered by importance/usage frequency.
9. **Protected/internal methods**.
   Use `// Protected/internal methods` comment to separate this section
10. **Private methods**, such as helper methods and utilities.
    Use `// Private methods` comment to separate this section.
11. All other nested types.
    Use `// Nested types` comment to separate this section.

For typical RPC API (interface):
1. Read methods go first.
   Typically, these are `[ComputeMethod]` methods.
2. Write methods go next,
   Typically, these are `[CommandHandler]` methods.
3. Command handler methods should have `On` prefix
   (e.g., `OnChange`, `OnUpdate`).
4. Command handler commands should be declared right after API interface
   in the same file. Their names should start with `{InterfaceNameWithoutI}_`
   prefix, e.g., `Chat_Edit` for `IChat` interface.

Special cases:
- **API implementation classes** should have the same member order
  as in the API interface.
- **DI injected services** typically follow more specific to more general
  order, so services like `ILogger` are placed at the very end of
  DI injected member set.
- If it's hard to determine the order, use alphabetical order.

Examples:
```csharp
public class Chats(IServiceProvider services) : IChats
{
    // 1. Static fields
    public static readonly TileStack<long> ServerIdTileStack = Constants.Chat.ServerIdTileStack;
    
    // 2. Dependency-injected services
    private IAccounts Accounts { get; } = services.GetRequiredService<IAccounts>();
    private IPlaces Places => field ??= services.GetRequiredService<IPlaces>();
    private ICommander Commander { get; } = services.Commander();
    private ILogger Log { get; } = services.LogFor<Chats>();
    
    // 3. Public read methods (e.g., compute methods)
    public virtual async Task<Chat?> Get(Session session, ChatId chatId, CancellationToken cancellationToken)
    { /* ... */ }
    
    // 4. Public write methods (e.g., command handlers)
    // [CommandHandler]
    public virtual async Task<Chat> OnChange(Chats_Change command, CancellationToken cancellationToken)
    { /* ... */ }
    
    // Protected methods
    
    // 5. Protected/internal methods
    [ComputeMethod]
    protected virtual async Task<ReadPositionsStat> GetReadPositionsStatInternal(ChatId chatId, CancellationToken cancellationToken)
    { /* ... */ }
    
    // Private methods
    
    private async Task<PrincipalId> GetOwnPrincipalId(Session session, ChatId chatId, CancellationToken cancellationToken)
    { /* ... */ }
}

public interface IMediaBackend : IComputeService, IBackendService
{
    [ComputeMethod]
    Task<Media?> Get(MediaId? mediaId, CancellationToken cancellationToken);
    [ComputeMethod]
    Task<Media?> GetByMediaIdScope(string mediaIdScope, CancellationToken cancellationToken);
    [ComputeMethod]
    Task<Media?> GetByContentId(string contentId, CancellationToken cancellationToken);

    [CommandHandler]
    Task<Media?> OnChange(MediaBackend_Change command, CancellationToken cancellationToken);
    [CommandHandler]
    Task OnCopyChat(MediaBackend_CopyChat command, CancellationToken cancellationToken);
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
// ReSharper disable once InconsistentNaming
public sealed partial record MediaBackend_Change(
    [property: DataMember, MemoryPackOrder(0)] MediaId Id,
    [property: DataMember, MemoryPackOrder(1)] Change<Media> Change
) : ICommand<Media?>, IBackendCommand, IHasShardKey<MediaId>
{
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public MediaId ShardKey => Id;
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
// ReSharper disable once InconsistentNaming
public sealed partial record MediaBackend_CopyChat(
    [property: DataMember, MemoryPackOrder(0)] ChatId ChatId,
    [property: DataMember, MemoryPackOrder(1)] string CorrelationId,
    [property: DataMember, MemoryPackOrder(2)] MediaId[] MediaIds
) : ICommand<Unit>, IBackendCommand, IHasShardKey<ChatId>
{
    [IgnoreDataMember, MemoryPackIgnore]
    public ChatId ShardKey => ChatId;
}
```

### Project-Specific Patterns

1. **Primary constructors, dependency injection, lazy DI style**:
```csharp
public class Chats(IServiceProvider services) : IChats
{
    private IServiceProvider Services { get; } = services;
    private IAccounts Accounts { get; } = services.GetRequiredService<IAccounts>();
    private IPlaces Places => field ??= Services.GetRequiredService<IPlaces>();
    private ICommander Commander => field ??= Services.Commander();  // Rarely needed
    private ILogger Log => field ??= Services.LogFor<Chats>(); // Rarely needed
}
```

2. **API records** should be fully serializable,
   which typically implies the presence of the following attributes:
```csharp
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[method: MemoryPackConstructor, SerializationConstructor, JsonConstructor]
public sealed partial record TextEntry(
    [property: DataMember(Order = 0), MemoryPackOrder(0), Key(0)] long LocalId,
    [property: DataMember(Order = 1), MemoryPackOrder(1), Key(1)] string Content)
{ }
```

3. **.ConfigureAwait(false)** must be used in all async calls
   in service layer code, and **.ConfigureAwait(true)** is typically needed
   in the UI code, if the code after `await` uses instance properties
   or fields. Otherwise, it could be `ConfigureAwait(false)`.

Here is an example of how `.ConfigureAwait(false)` can be used in the UI code:
```csharp
public override async Task Require(CancellationToken cancellationToken)
{
    var mustBeActive = MustBeActive;
    var mustBeAdmin = MustBeAdmin;
    // Instance properties are cached, so .ConfigureAwait(false) is fine from here

    var account = await Accounts.GetOwn(Session, cancellationToken).ConfigureAwait(false);
    if (mustBeAdmin) {
        account.Require(AccountFull.MustBeAdmin);
        return; // No extra checks are needed in this case
    }
    if (mustBeActive)
        account.Require(AccountFull.MustBeActive);
}
```

4. **Do not use `new TaskCompletionSource()`** directly.
   Use `TaskCompletionSourceExt.New()` or `TaskCompletionSourceExt.New<T>()` instead.

5. Two overloads similar to `.ConfigureAwait(...)` are used:
- `.SilentAwait(true/false)` awaits a task w/o throwing any exceptions
- `.ResultAwait(true/false)` awaits a task and returns `Result<T>` w/o throwing any exceptions.

6. **Prefer `FilePath` over `string` for file paths and file names.**
   Use `FilePath` from `ActualLab.IO` instead of raw strings when working
   with file paths or file names. `FilePath` provides path combination
   via `&` and `|` operators, `RelativeTo`, `DirectoryPath`,
   `FileNameWithoutExtension`, `Extension`, and implicit conversion
   to/from `string`.

| Instead of | Use |
|---|---|
| `string filePath = "/some/path"` | `FilePath filePath = "/some/path"` |
| `Path.Combine(dir, fileName)` | `dir & fileName` or `dir \| fileName` |
| `Path.GetFileName(path)` | `path.FileName` |
| `Path.GetExtension(path)` | `path.Extension` |

   See `ActualLab.IO.FilePath` for the full API.

7. **Prefer `sealed` classes and records** unless inheritance is intended.

8. **Prefer `LogFor(GetType())` over `LogFor<T>()`** for the current type in non-static context.

9. **Prefer primary constructors for services** when acceptable.


### Disabled/Silenced Warnings

Search for `<NoWarn>` to see the list of disabled warnings.

See [`.editorconfig`](../.editorconfig) for the complete list of silenced analyzer warnings.

## TypeScript

TypeScript follows the same flow-control spacing rules as C#:
- Never place a flow-control statement on the same line as its `if`, `for`,
  `while`, or similar condition.
- A `return`, `break`, `continue`, `throw`, or `yield` statement is typically
  followed by a blank line unless it is the last statement in its enclosing block.
- If the flow-control statement is the last statement in a nested block, put the
  blank line after that block instead, unless the block is the whole method or
  function body.

TypeScript uses the same member-section comments as .NET:
- Order class members similarly to .NET classes: static fields first, then
  instance fields/properties, constructor-like setup, public methods,
  protected/internal-style helpers, private methods, and nested/local types
  or constants last when applicable.
- Put private helper methods under a `// Private methods` section.
- If protected/internal-style helpers are needed, use `// Protected/internal methods`
  before them and keep `// Private methods` below that section.
- Do not create ad hoc alternatives such as `// Helpers`, `// Utilities`, or
  `// Internals` when the .NET section names apply.

Example:
```ts
// Wrong
if (Api._isDotNetRpcConnected === value) return;

// Correct
if (Api._isDotNetRpcConnected === value)
    return;

Api._isDotNetRpcConnected = value;
```
