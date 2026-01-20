---
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
description: Documentation writer for Fusion docs - use for any docs/ folder tasks
argument-hint: [task-description]
---

# Fusion Documentation Writer

You are working on documentation for ActualLab.Fusion, published at https://fusion.actuallab.net/

## FIRST STEP - Always Read

Before doing anything else, read `/docs/AGENTS.md` - it contains the authoritative instructions for documentation work including style guide, tools, and structure details.

## Documentation Structure

```
docs/
├── index.md              # Starting point, navigation structure
├── Part*.md              # Main documentation chapters
├── Part*.cs              # Code snippets for corresponding .md files
├── .vitepress/           # VitePress site configuration
│   └── config.mts        # Navigation, sidebar, theme config
├── img/                  # Images (light/ and dark/ subfolders for themed SVGs)
├── img-src/              # Mermaid diagram sources (.mmd files)
├── outdated/             # Old docs - DO NOT EDIT
└── Docs.csproj           # Project for building/running code samples
```

## Marketing vs Technical Content

**Marketing materials** (punchy, interesting, sell the benefits):
- `/README.md` (root) - First impression for GitHub visitors
- `/docs/index.md` - Landing page for documentation site

These files should be engaging, highlight value propositions, and hook the reader. Focus on "why" and "what's possible" rather than technical details. Use compelling examples, emphasize pain points solved, and keep it scannable.

**Technical documentation** (precise, thorough, instructional):
- All `Part*.md` files and everything else in `/docs`

These should be accurate, detailed, and help developers actually use Fusion.

## Key Documentation Files

| File | Purpose |
|------|---------|
| `Part01*.md/cs` | Core concepts - Computed Values, Compute Services |
| `Part02*.md/cs` | Replicas, Compute Service Clients |
| `Part03*.md/cs` | Commands, Authentication |
| `Part04*.md/cs` | Blazor integration |
| `Part05*.md/cs` | Advanced topics |
| `PartAA*.md/cs` | Sample app walkthrough |
| `PartAP*.md/cs` | API reference |
| `Performance.md` | Benchmarks and performance data |

## Tools

### mdsnippets - Code Embedding

Embeds code from `.cs` files into `.md` files.

**In `.cs` file:**
```cs
#region PartXX_SnippetName
// Your code here
#endregion
```

**In `.md` file:**
```md
<!-- snippet: PartXX_SnippetName -->
<!-- endSnippet -->
```

Run from `/docs`: `dotnet mdsnippets`

### VitePress - Site Generator

- Dev server: `npm run docs:dev`
- Production build: `npm run docs:build`

### Running Code Samples

```powershell
cd docs
dotnet run --project Docs.csproj -- Part01    # Run specific part
dotnet run --project Docs.csproj -- all       # Run all parts
dotnet run --project Docs.csproj              # Interactive mode
```

## Style Guide (Critical Rules)

1. **NO horizontal rules** - Don't use `---` except for YAML frontmatter. VitePress CSS handles section separation.

2. **NO triangle symbols in diagrams** - Don't use `▶` or `◀` in ASCII art/code blocks (inconsistent width). Use `>` and `<` instead.

3. **Paired edits** - When editing code snippets, always update BOTH the `.md` and `.cs` files.

4. **Relative links** - Use relative paths for internal doc links.

5. **Code extraction** - For code samples longer than 2-3 lines, extract to `.cs` file as a snippet.

## Common Tasks

### Adding/Editing Content
1. Read the target `Part*.md` file
2. If it has code snippets, also read `Part*.cs`
3. Make changes to both files if needed
4. Verify with `dotnet build Docs.csproj`

### Updating Navigation
Edit `.vitepress/config.mts` - look for `sidebar` and `nav` sections.

### Adding Images
- Place in `docs/img/`
- For themed diagrams: `docs/img/light/` and `docs/img/dark/`

### Mermaid Diagrams

See the "Mermaid Diagrams" section in `/docs/AGENTS.md` for usage and rules.

## Your Task

$ARGUMENTS
