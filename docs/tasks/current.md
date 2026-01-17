# Current Task

## Task 1: Move Parts 8, 12, 13 to Outdated

Move the following files to `/docs/outdated/` folder:
- Part08.md, Part08.cs
- Part12.md, Part12.cs
- Part13.md, Part13.cs

Then remove all references to these parts from:
- Table of contents (index.md, README.md, or wherever TOC is located)
- All other Part*.md files that reference them

## Task 2: Update Part 5 (Commander)

### 2.1 Replace Image Table with Markdown Table
- Find the image reference (the first/only image in Part05.md)
- Get the source data from `/docs/tables/Mediator vs Commander.md`
- Replace the image with the actual markdown table

### 2.2 Validate Code and Statements
- Read Part05.md and Part05.cs thoroughly
- Check if all statements correspond to the current Fusion codebase
- Ensure the C# code in Part05.cs:
  - Contains all code described in the document
  - Compiles and runs correctly
  - Produces output that matches what's shown in the documentation
- Use code snippets format (like other parts) if not already done
- Fix any incorrect or outdated statements

## Task 3: Update Part 10 (Multi-host Invalidation)

### 3.1 Background Research
- Read Part08.md (before it's moved) to understand the context Part 10 references
- Understand multi-host invalidation = outbox pattern for invalidation
- Look at the multi-host invalidation test in samples (test with ping-pong pattern showing two hosts using the same database)
- Check QuickStart.md as it covers Operation Framework briefly

### 3.2 Create Part10.cs - Identifier Verification Approach
**Use this approach (same for Part 11):**
1. Find every identifier mentioned in Part10.md
2. Add fake uses of these identifiers in a non-snippet section of the C# file
3. Each reference should have a comment with its **original name** from the doc
   - Example: `_ = typeof(SomeActualClass); // "SomeOldName" from docs`
   - Or use a fake method: `Dump(nameof(SessionMiddleware)); // session middleware`
4. Try to compile - resolve every build error
5. Once all errors resolved, you have the actual names for every referenced identifier
6. Then refactor into proper code snippets

### 3.3 Update Code Examples and Validate
- Update outdated API calls, for example:
  - `CreateOperationDbContext` should now use `DbHub`
  - Check if `ServerSideCommandBase` still exists
  - Verify current naming for: `NestedCommandLogger`, `TransientOperationScopeProvider`, etc.
- Reference TodoApp or Blazor app source code for correct patterns
- Validate all statements in the document are correct against current codebase
- Use code snippets format for code examples

## Task 4: Update Part 11 (Authentication)

**Very outdated code - needs significant updates.**

### 4.1 Create Part11.cs - Identifier Verification Approach
Same approach as Part 10:
1. Find every identifier mentioned in Part11.md
2. Add fake uses in non-snippet code with comments showing original names
3. Compile and resolve all build errors
4. Once compiling, refactor into proper snippets

### 4.2 Specific Items to Fix
- **Discord channel reference**: Remove, replace with "Voced Fusion" (or similar community)
- **`_Host.html`**: Now `HostPage.razor` (as stated in Part 4)
- **`fusion.js`** and similar: Check if references are still valid
- **`OrderController`**: Legacy pattern - Fusion services used to be exposed via controllers, now not necessary. Remove or explain it's deprecated.
- **Razor source**: Fix to match current patterns - look at `App.razor` in TodoApp
- **Claims search code**: Likely now part of `FusionAuth` / `ServerRouter` helper

### 4.3 Code Snippet Sources
- For snippets mentioning Session class: copy from actual Session class
- Leave reference to original source code (like Part 4 does)
- Use TodoApp code as primary reference for how things should be done

### 4.4 Validation
- Every reference to Fusion code must be verified against current codebase
- Ensure all snippets compile
- Update Part11.md with corrected code and explanations

---

## General Approach for Parts 10 & 11

**Do Part 10 first, then Part 11.**

1. Start with the C# file
2. Find every identifier mentioned in the .md file
3. Add fake use of each identifier with comment showing original name
4. Compile and fix all errors (this reveals actual current names)
5. Refactor into proper code snippets
6. Update the .md file accordingly

## Handling Unresolved Issues

If unable to resolve some issues:
- Document them in `/docs/tasks/unresolved.md`
- Include: what the issue is, what was tried, what the original reference was
- Will be reviewed manually later

---

## Reference Locations

- Fusion source code: `/project/src/`
- Fusion samples: `/samples-project/` (Docker) or `D:\Projects\ActualLab.Fusion.Samples` (Windows)
- Table source: `/docs/tables/Mediator vs Commander.md`
- Multi-host invalidation test: search in samples for "MultiHost*" test
- TodoApp: primary reference for current patterns (especially for Auth and Blazor)
- Part 4: reference for how to cite original source code in snippets
