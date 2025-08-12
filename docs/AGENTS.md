# AGENTS.md for ActualLab.Fusion Documentation

## Scope

This file applies to all documentation files in the `docs/` directory and its subdirectories.

IMPORTANT: Read `../README.md` to learn what Fusion is.

## Documentation Structure

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

All the tools & scripts listed below must be started from `Docs/` folder. So if you're in the root folder, run `cd Docs` first.

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

## Additional Notes

AGENTS.md in other folders may extend and override instructions provided here.
