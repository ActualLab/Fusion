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

#### Comments and XML Documentation:
- Prefer regular comments over XML documentation
- **DON'T write XML documentation comments for public APIs UNLESS they are already there**
- When XML documentation exists, maintain its style and completeness.

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
- Blank line must follow any (yield) return, (yield) break, or continue statement -
  in other words, any block-escaping statement - unless it's the last statement in the block.

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
   Use `[field: AllowNull, MaybeNull]` with null-coalescing assignment of
   Services.GetRequiredService<T>()`
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
    [field: AllowNull, MaybeNull]
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
    [field: AllowNull, MaybeNull]
    private IPlaces Places => field ??= Services.GetRequiredService<IPlaces>();
    [field: AllowNull, MaybeNull]
    private ICommander Commander => field ??= Services.Commander();  // Rarely needed
    [field: AllowNull, MaybeNull]
    private ILogger Log => field ??= Services.LogFor<Chats>(); // Rarely needed
}
```

2. **API records** should be fully serializable,
   which typically implies presence of the following attributes:
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

4. Two overloads similar to `.ConfigureAwait(...)` are used:
- `.SilentAwait(true/false)` awaits a task w/o throwing any exceptions
- `.ResultAwait(true/false)` awaits a task and returns `Result<T>` w/o throwing any exceptions.


### Disabled/Silenced Warnings

Search for `<NoWarn>` to see the list of disabled warnings.

See [`.editorconfig`](../.editorconfig) for the complete list of silenced analyzer warnings.
