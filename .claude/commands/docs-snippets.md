---
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
description: Documentation snippets guide - authoring, running, and embedding code snippets
argument-hint: [question-or-task]
---

# Documentation Snippets Guide

This guide explains how to author, run, and embed code snippets in the Fusion documentation.

## Overview

The documentation system uses three components:

1. **Code snippets** in `Part*.cs` files - actual runnable C# code
2. **Markdown documentation** in `Part*.md` files - where snippets are embedded
3. **mdsnippets tool** - extracts code from `.cs` and embeds into `.md`

## File Naming Conventions

### Mapping Between .cs and .md Files

| Code File | Documentation File(s) | Namespace |
|-----------|----------------------|-----------|
| `PartF.cs` | `PartF.md`, `PartF-*.md` | `Docs.PartF` |
| `PartC.cs` | `PartC.md`, `PartC-*.md` | `Docs.PartC` |
| `PartR.cs` | `PartR.md`, `PartR-*.md` | `Docs.PartR` |
| `PartB.cs` | `PartB.md`, `PartB-*.md` | `Docs.PartB` |
| `PartO.cs` | `PartO.md`, `PartO-*.md` | `Docs.PartO` |
| `PartAA.cs` | `PartAA.md`, `PartAA-*.md` | `Docs.PartAA` |
| `PartAP.cs` | `PartAP.md`, `PartAP-*.md` | `Docs.PartAP` |
| `PartEF.cs` | `PartEF.md`, `PartEF-*.md` | `Docs.PartEF` |
| `PartF-MM.cs` | `PartF-MM.md` | `Docs.PartFMM` |

### Namespace Convention

All Part files use `Docs.{PartName}` namespace (without hyphens):
- `PartF.cs` → `namespace Docs.PartF;`
- `PartF-MM.cs` → `namespace Docs.PartFMM;`
- `PartAA.cs` → `namespace Docs.PartAA;`

## Authoring Snippets in .cs Files

### 1. The DocPart Base Class

Every Part file must define a class that inherits from `DocPart`:

```cs
namespace Docs.PartF;

public class PartF : DocPart
{
    public override async Task Run()
    {
        // Run snippets here
    }
}
```

The `DocPart` base class (defined in `Program.cs`) provides:
- Abstract `Run()` method - must be implemented to run all snippets
- `StartSnippetOutput(string snippetName)` - outputs `---- {snippetName} ----` marker

### 2. Defining Code Snippets

Use `#region` / `#endregion` to define snippets:

```cs
#region PartF_Declare_Service
public class CounterService : IComputeService
{
    [ComputeMethod]
    public virtual async Task<int> Get(string key)
    {
        return _counters.GetValueOrDefault(key, 0);
    }
}
#endregion
```

**Naming convention:** `{PartName}_{SnippetId}`
- `PartF_Declare_Service`
- `PartF_Register_Services`
- `PartC_PrintCommandSession`

### 3. Running Snippets in the Run() Method

Use `StartSnippetOutput()` to mark snippet execution:

```cs
public override async Task Run()
{
    StartSnippetOutput("Automatic Caching");
    #region PartF_Automatic_Caching
    await counters.Get("a"); // Prints: Get(a) = 0
    await counters.Get("a"); // Prints nothing -- it's a cache hit
    #endregion

    StartSnippetOutput("Invalidation");
    #region PartF_Invalidation
    counters.Increment("a");
    await counters.Get("a"); // Prints: Get(a) = 1
    #endregion
}
```

### 4. Snippets Outside the Run() Method

Snippets defining types, interfaces, or helper methods go outside `Run()`:

```cs
namespace Docs.PartF;

#region PartF_Declare_Service
public class CounterService : IComputeService
{
    // ... service implementation
}
#endregion

public class PartF : DocPart
{
    public override async Task Run()
    {
        // ... run code
    }
}
```

## Embedding Snippets in .md Files

### Basic Embedding

In your `.md` file, use this format:

```md
<!-- snippet: PartF_Declare_Service -->
<!-- endSnippet -->
```

After running `dotnet mdsnippets`, it becomes:

```md
<!-- snippet: PartF_Declare_Service -->
```cs
public class CounterService : IComputeService
{
    [ComputeMethod]
    public virtual async Task<int> Get(string key)
    {
        return _counters.GetValueOrDefault(key, 0);
    }
}
```
<!-- endSnippet -->
```

### Running mdsnippets

From the `docs/` directory:

```powershell
dotnet mdsnippets
```

This scans all `.cs` files for `#region` blocks and updates all `.md` files with matching `<!-- snippet: -->` references.

**Warning:** This overwrites `.md` files! The content between `<!-- snippet: X -->` and `<!-- endSnippet -->` is replaced.

## Running Code Snippets

### Build First

```powershell
cd docs
dotnet build Docs.csproj
```

### Run Specific Parts

```powershell
# Run a single part
dotnet run --project Docs.csproj -- PartF

# Or from the output directory
cd artifacts/docs/bin/Docs/debug
dotnet Docs.dll PartF
```

### Run Multiple Parts

```powershell
dotnet Docs.dll PartF PartC PartR
```

### Run All Parts

```powershell
dotnet Docs.dll all
```

### Interactive Mode

```powershell
dotnet Docs.dll
# Shows: Available parts: PartAA, PartAP, PartB, PartC, PartEF, PartF, PartFMM, PartO, PartR
# Prompts for selection
```

## Understanding the Output

When you run a part, output is structured with markers:

```
---- Part PartF started ----
---- Automatic Caching ----
Get(a) = 0
---- Invalidation ----
Increment(a)
Get(a) = 1
---- Part PartF completed ----
```

- `---- Part {PartName} started ----` - Part execution begins
- `---- {SnippetName} ----` - Individual snippet starts (from `StartSnippetOutput`)
- `---- Part {PartName} completed ----` - Part execution ends

### Finding Output for a Specific Snippet

To find output for `PartF_Automatic_Caching`:

1. Run the part: `dotnet Docs.dll PartF`
2. Look for `---- Automatic Caching ----` in the output
3. Everything until the next `----` marker is that snippet's output

## Updating Snippet Output in .md Files

When documentation shows expected output, you need to manually update it:

1. **Run the snippet:**
   ```powershell
   dotnet Docs.dll PartF 2>&1 | tee output.txt
   ```

2. **Find the relevant section** in the output (between `---- SnippetName ----` markers)

3. **Update the .md file** - locate the output section (usually in a code block after the snippet) and update it

**Note:** mdsnippets only embeds code, not output. Output must be manually maintained.

## Complete Example: Adding a New Snippet

### Step 1: Add Code to PartF.cs

```cs
namespace Docs.PartF;

// Type definition snippet (outside Run)
#region PartF_MyNewService
public class MyNewService : IComputeService
{
    [ComputeMethod]
    public virtual Task<string> GetValue() => Task.FromResult("Hello");
}
#endregion

public class PartF : DocPart
{
    public override async Task Run()
    {
        // ... existing snippets ...

        StartSnippetOutput("MyNewService Demo");
        #region PartF_MyNewService_Demo
        var service = sp.GetRequiredService<MyNewService>();
        var result = await service.GetValue();
        WriteLine($"Result: {result}");
        #endregion
    }
}
```

### Step 2: Reference in PartF.md

```md
## My New Service

Here's a simple service:

<!-- snippet: PartF_MyNewService -->
<!-- endSnippet -->

Using the service:

<!-- snippet: PartF_MyNewService_Demo -->
<!-- endSnippet -->

Expected output:
```
Result: Hello
```
```

### Step 3: Run mdsnippets

```powershell
cd docs
dotnet mdsnippets
```

### Step 4: Verify

```powershell
dotnet build Docs.csproj
dotnet run --project Docs.csproj -- PartF
```

## Configuration

The `mdsnippets.json` file in `docs/` configures the tool:

```json
{
    "$schema": "https://raw.githubusercontent.com/SimonCropp/MarkdownSnippets/master/schema.json",
    "Convention": "InPlaceOverwrite",
    "OmitSnippetLinks": true,
    "ExcludeMarkdownDirectories": [ "outdated" ]
}
```

- `InPlaceOverwrite` - Updates .md files in place
- `OmitSnippetLinks` - Doesn't add source file links
- `ExcludeMarkdownDirectories` - Skips `outdated/` folder

## Best Practices

1. **Snippets must match original code** - When converting inline code from `.md` files to snippets, keep the code as close to the original as possible. Don't comment out lines or dramatically change the code structure.

   **If code references undefined methods/types:**
   - **DO:** Add fake/stub methods or types to make the snippet compile
   - **DON'T:** Comment out the code that references them

   ```cs
   // BAD - commented out code changes the example
   #region PartF_Invalidation
   using (Invalidation.Begin()) {
       // _ = GetOrders(cartId, default);  // <-- Don't do this!
   }
   #endregion

   // GOOD - add a stub method to make it compile
   // Stub for snippet
   Task<Order[]> GetOrders(long cartId, CancellationToken ct) => Task.FromResult(Array.Empty<Order>());

   #region PartF_Invalidation
   using (Invalidation.Begin()) {
       _ = GetOrders(cartId, default);  // <-- Original code preserved
   }
   #endregion
   ```

2. **Handle duplicate variables with numbered names** - When the original code has multiple lines declaring the same variable name (which won't compile), use numbered suffixes consistently:

   ```cs
   // Original (won't compile - duplicate 'value'):
   // var value = await computed.Use(ct);
   // var value = await computed.Use(allowInconsistent: true, ct);

   // GOOD - use numbered suffixes:
   var value1 = await computed.Use(ct);
   var value2 = await computed.Use(allowInconsistent: true, ct);
   ```

   For blocks of similar function calls or property accesses, use short numbered variables:

   ```cs
   // GOOD - consistent numbering for API showcase:
   var d1 = FixedDelayer.Get(1);    // 1 second delay
   var d2 = FixedDelayer.Get(0.5);  // 500ms delay
   var d3 = FixedDelayer.NextTick;  // ~16ms delay
   var d4 = FixedDelayer.MinDelay;  // Minimum safe delay (32ms)
   ```

3. **Short code (1-2 lines) doesn't need snippets** - For very short code examples (up to 2 lines), keep them as regular code blocks in the `.md` file. Snippets add overhead and are not worth it for trivial examples.

   ```md
   <!-- Keep as regular code block -->
   ```cs
   computed.Invalidate();
   ```

   <!-- Use snippet for longer code -->
   <!-- snippet: PartF_ComplexExample -->
   <!-- endSnippet -->
   ```

4. **ALL C# code (3+ lines) must be in snippets** - Never write C# code directly in `.md` files. Every C# code block must come from a snippet in a `.cs` file. This ensures:
   - Code is compiled and validated
   - Code stays in sync with actual implementations
   - No stale or broken examples in documentation

   **Exceptions** (code that cannot be in `.cs` snippets):
   - `.razor` component code (Blazor markup)
   - `.csproj` / MSBuild XML
   - `appsettings.json` / configuration files
   - Shell commands / PowerShell
   - Pseudo-code or conceptual examples marked as such

5. **Keep snippets COMPACT** - Snippets should contain only the essential code the reader needs to see. **Do NOT wrap code in unnecessary classes or methods just to satisfy C# syntax requirements.**

   **BAD - unnecessary wrapper class:**
   ```cs
   #region PartAA_QuickStart_Register
   public static class QuickStartExample  // <-- Reader doesn't need this
   {
       public static void RegisterAuthServices(IServiceCollection services)  // <-- Or this
       {
           var fusion = services.AddFusion();
           var fusionServer = fusion.AddWebServer();
           fusion.AddDbAuthService<AppDbContext, long>();
       }
   }
   #endregion
   ```

   **GOOD - just the essential code:**
   ```cs
   #region PartAA_QuickStart_Register
   var fusion = services.AddFusion();
   var fusionServer = fusion.AddWebServer();
   fusion.AddDbAuthService<AppDbContext, long>();
   #endregion
   ```

   **When to include classes/methods in snippets:**
   - The class itself is meaningful (e.g., showing a compute service with `[ComputeMethod]` attributes)
   - Demonstrating interface implementation patterns
   - Showing class structure, inheritance, or decorators
   - The original documentation showed the full class

   **When NOT to include wrapper classes:**
   - The original inline code was just statements or method bodies
   - The class exists solely because C# requires code to be in a class
   - The wrapper adds no educational value for the reader

6. **Process ALL Part files including subparts** - Documentation is split into:
   - Main parts: `PartF.md`, `PartC.md`, `PartR.md`, etc.
   - Subparts: `PartF-MM.md`, `PartC-CI.md`, `PartO-EV.md`, `PartAA-DB.md`, etc.

   When extracting inline code to snippets, scan **both** main parts and subparts (all `Part*.md` and `Part*-*.md` files).

7. **Keep snippets focused** - Each snippet should demonstrate one concept
8. **Use descriptive names** - `PartF_Automatic_Caching` not `PartF_Snippet1`
9. **Match documentation flow** - Snippets should appear in logical order in Run()
10. **Test before committing** - Always run `dotnet build Docs.csproj` and test the part
11. **Update both files** - When editing snippets, update `.cs` AND verify `.md` after mdsnippets
12. **Use StartSnippetOutput** - Makes output parseable and easier to match with docs

## Troubleshooting

### Snippet not appearing in .md

- Check the region name matches exactly (case-sensitive)
- Run `dotnet mdsnippets` from the `docs/` directory
- Verify the .cs file is in the docs folder (mdsnippets scans recursively)

### Build errors after mdsnippets

- mdsnippets only copies code; it doesn't validate C# syntax
- Check the original .cs file compiles: `dotnet build Docs.csproj`

### Output doesn't match documentation

- Run the part and capture output
- Compare with documented output
- Update documentation manually (output is not auto-synced)

## Your Task

$ARGUMENTS
