# Agent instructions for ActualLab.Fusion Documentation

## Scope

This file applies to all documentation files in the `docs/` directory and its subdirectories.

`../AGENTS.md` (located in the root folder) describes agent instructions for the entire project. 

Before you proceed further, you ABSOLUTELY need to read the following files:
- `index.md` (located in `docs/` folder)
- `../index.md` (located in the root folder)
- `../AGENTS.md` (located in the root folder)

## General Instructions  

We are migrating the old Fusion documentation located
in `docs/tutorial/` folder into the new `Part*.md` and `Part*.cs`
files in `docs/` folder. Our goal is to make it better: 
easier to read, cleaner, and factually correct - 
based on the most current source code.

For your reference, `docs/Part01.*` files were generated
as a request to merge and update `docs/tutorial/Part01.md`-`Part03.md` 
from the old Fusion documentation.
YOU MUST ABSOLUTELY SEE THESE FILES before starting to work 
on a similar task.

## Documentation Structure

- Starting point: `docs/index.md`
- Main documentation files: `Part*.md` files that contain the core documentation
- Source code snippets: `Part*.cs` files that are used to generate code examples in the documentation.
- `docs/.vitepress/` contains the VitePress configuration for the documentation site
- `docs/diagrams/` contains Mermaid diagrams used in the documentation
- `docs/img/` contains images used in the documentation
- `docs/outdated/` contains other outdated documentation that may still be useful. **DO NOT EDIT files in this folder.**

## Documentation Tools

- **mdsnippets**: Used to extract code snippets from `*.cs` files and replace their references in `*.md` files
- **VitePress**: Used to build the documentation site

Code snippet format:

The source block in `.cs` file:
```cs
#region PartXX_SnippetId
// This snippet is referenced from AGENTS.md
#endregion
```

The reference / embedding block in `.md` file:
```md
<!-- snippet: PartXX_SnippetId -->
```cs
// This snippet is referenced from .instructions.md
```
<!-- endSnippet -->
```

## Building Documentation

All the tools & scripts listed below must be started from `docs/` folder. So if you're in the root folder, run `cd Docs` first.

- To update code snippets in the documentation (careful, it will overwrite the `*.md` files!):
  ```powershell
  dotnet mdsnippets
  ```

- To build and run the documentation site:
  ```powershell
  npm run docs:dev
  ```

- To build the documentation for production:
  ```powershell
  npm run docs:build
  ```

## Running Code Snippets

The `Docs.csproj` project can execute the code samples from `PartXX.cs` files. Each `PartXX.cs` file contains a class with a static `Run` method that demonstrates the concepts described in the corresponding `PartXX.md` file.

To run specific parts:
```powershell
# Run a specific part (e.g., Part01)
dotnet run --project Docs.csproj -- Part01

# Run multiple parts
dotnet run --project Docs.csproj -- Part01 Part02

# Run all parts
dotnet run --project Docs.csproj -- all

# Interactive mode: run without arguments to see available parts
dotnet run --project Docs.csproj
```

When run interactively (without arguments), the program will:
1. Display all available parts (classes starting with "Part")
2. Prompt you to enter part names (space-separated) or press Enter to run all parts

## Documentation Style Guide

- Use clear, concise language
- Provide practical examples
- Use relative links for internal documentation references
- Use consistent formatting for code blocks and commands
- Include tables where it's helpful
- For longer code samples (more than 2-3 lines), consider extracting them into code snippets
- If you edit an existing code snippet, always update both `.md` and `.cs` files
- Ensure code examples are accurate and up-to-date
- When adding new documentation, follow the existing structure
- Update the documentation index if adding new pages.
