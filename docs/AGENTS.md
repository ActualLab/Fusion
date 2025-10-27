# Agent instructions for ActualLab.Fusion Documentation

## Scope

This file applies to all documentation files in the `docs/` directory and its subdirectories.

`../AGENTS.md` (located in the root folder) describes agent instructions for the entire project. 

Before you proceed further, you ABSOLUTELY need to read the following files:
- `README.md` (located in `docs/` folder)
- `../README.md` (located in the root folder)
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

- Starting point: `docs/README.md`
- Main documentation files: `Part*.md` files that contain the core documentation
- Source code snippets: `Part*.cs` files that are used to generate code examples in the documentation.
- `docs/.vitepress/` contains the VitePress configuration for the documentation site
- `docs/diagrams/` contains Mermaid diagrams used in the documentation
- `docs/img/` contains images used in the documentation
- `docs/tutorial/` contains the outdated documentation files - we are writing the new ones (`Part*.md) based on these ones.
- `docs/outdated/` contains other outdated documentation that may still be useful.

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
// This snippet is referenced from AGENTS.md
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
