# Coding Style Guide

This document describes the coding style conventions used in the ActualLab.Fusion project that differ from standard .NET conventions.

For complete installation, build, and test instructions, refer to `AGENTS.md`.

## Coding Style Differences from Default .NET Conventions

This project uses several coding style conventions that differ from standard .NET guidelines:

### 1. **File-Scoped Namespaces with Semicolon Separator**
The project uses file-scoped namespaces but places them on separate lines with a semicolon, followed by a blank line:
```csharp
namespace ActualLab.Channels;

public class UnbufferedPushSequence<T> : IAsyncEnumerable<T>
```
**Standard .NET:** Typically uses block-scoped namespaces or file-scoped without the extra blank line.

### 2. **Compact Brace Style for Control Flow**
Single-line statements in control flow often omit braces, and opening braces for multi-line blocks (inside method bodies) appear on the same line, except for classes and methods:
```csharp
try {
    while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
    while (reader.TryRead(out var item)) {
        var newItem = transformer(item);
        await writer.WriteAsync(newItem, cancellationToken).ConfigureAwait(false);
    }
}
catch (OperationCanceledException oce) {
    if ((copyMode & ChannelCopyMode.CopyCancellation) != 0)
        writer.TryComplete(oce);
}
```
So if you write a method or property accessor body, use compact bracing style. 

**Standard .NET:** Typically uses braces on new lines (Allman style) and always includes braces for control flow statements.

### 3. **Expression-Bodied Members with Arrow Syntax**
Extensive use of expression-bodied members for simple properties and methods:
```csharp
public static FusionBlazorBuilder AddBlazor(this FusionBuilder fusion)
    => new(fusion, null);
```
**Standard .NET:** This is acceptable but used more extensively in this project.

### 4. **Primary Constructors with Inline Field Initialization**
Heavy use of C# 12 primary constructors with direct parameter usage:
```csharp
public class RpcWebSocketServer(
    RpcWebSocketServer.Options settings,
    IServiceProvider services)
```
**Standard .NET:** More traditional constructor syntax is still common.

### 5. **Field-Backed Auto-Properties with `[field:]` Attribute**
Uses field-backed auto-properties with attributes applied to the backing field:
```csharp
[field: AllowNull, MaybeNull]
internal IEnumerable<RpcPeerTracker> PeerTrackers => field ??= Services.GetRequiredService<IEnumerable<RpcPeerTracker>>();
```
**Standard .NET:** Less common pattern; typically uses explicit backing fields.

### 6. **Aggressive Inlining Attributes**
Use `[MethodImpl(MethodImplOptions.AggressiveInlining)]` for performance-critical code, especially if the method is small:
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static CommandContext GetCurrent()
    => Current ?? throw Errors.NoCurrentCommandContext();
```
**Standard .NET:** Used more sparingly.

### 7. **Static Readonly Fields for Shared Resources**
Pattern of using static readonly fields for shared resources like `ArrayPool<T>.Shared`:
```csharp
private static readonly ArrayPool<T> Pool = ArrayPool<T>.Shared;
```
**Standard .NET:** Common pattern but used consistently throughout the project.

### 8. **Conditional Compilation for .NET Version Features**
Use of conditional compilation for different .NET versions, e.g., any lock has to be declared as follows:
```csharp
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
```
**Standard .NET:** Used when necessary, but this project has extensive multi-targeting.

### 10. **Record Types for Configuration/Options**
Extensive use of record types for configuration and options classes:
```csharp
public record RpcInterceptorOptions : Interceptor.Options
{
    // ...
}
```
**Standard .NET:** Records are newer; traditional classes are still common for these scenarios.

### 12. **Extensive Use of `ConfigureAwait(false)` and `SilentAwait(false)`**
Consistent use of `.ConfigureAwait(false)` on all awaited tasks in library code:
```csharp
await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false)
```
`.SilentAwait(false)` allows to await a task w/o throwing exceptions. 

**Standard .NET:** Recommended for libraries but not always consistently applied.

### 13. **Global Usings Configuration**
Certain global usings defined in `Directory.Build.props` including project-specific namespaces:
```xml
<Using Include="ActualLab" />
<Using Include="ActualLab.Api" />
<Using Include="ActualLab.Async" />
```
**Standard .NET:** Global usings are newer; this project uses them extensively.

### 14. **Nullable Reference Type Annotations**
Consistent use of nullable reference types with explicit `?` annotations and `[AllowNull, MaybeNull]` attributes:
```csharp
private volatile Exception? _completionError;
```
**Standard .NET:** Nullable reference types are standard, but this project is very consistent in their application.

## General Coding Conventions

### Project Organization
- Organize code into appropriate folders:
  - `src/` for main libraries
  - `samples/` for sample apps
  - `tests/` for test projects
  - `docs/` for documentation

### Naming and Clarity
- Use clear, descriptive names for classes, methods, and variables
- Follow standard .NET naming conventions (PascalCase for types and public members, camelCase for parameters and local variables)

### Documentation
- **DON'T write XML documentation comments for public APIs UNLESS they are already there**
- When documentation exists, maintain its style and completeness

### When in Doubt
- Examine existing code in the same area and match its style
- The coding style documented here takes precedence over standard .NET conventions

## Summary

When working with this codebase, AI agents should:
- Use file-scoped namespaces with semicolon and blank line separator
- Apply compact brace style for any construct except class and method declaration (opening brace on same line)
- Omit braces for single-line control flow statements
- Use primary constructors where appropriate
- Apply `[MethodImpl(MethodImplOptions.AggressiveInlining)]` for small hot-path methods
- Always use `.ConfigureAwait(false)` in library code
- Leverage expression-bodied members extensively
- Use record types for DTOs and configuration objects
- Apply field-backed auto-properties with `[field:]` attributes for lazy initialization
- Follow the project's multi-targeting patterns with conditional compilation
- Use clear, descriptive names following .NET conventions
- Don't add XML documentation unless it already exists
- Organize code into appropriate folders (src/, samples/, tests/, docs/)
