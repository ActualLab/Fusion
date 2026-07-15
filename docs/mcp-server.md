---
title: Fusion Documentation & Source MCP Server
description: Connect Claude Code or ChatGPT to the public ActualLab.Fusion documentation and source-code MCP server.
---

# Fusion Documentation & Source MCP Server

The Fusion documentation site exposes a public, read-only Model Context Protocol server. It gives AI coding assistants
focused access to the Fusion **documentation** (the same Markdown and anchors used by this website) and to the Fusion
**source code** (the `.cs` / `.razor` files under `src`, `samples`, and `tests`).

**Endpoint:** `https://fusion.actuallab.net/mcp`

No authentication or API key is required. All tools are read-only and return Markdown or plain text.

## Documentation tools

| Tool | Inputs | Result |
| --- | --- | --- |
| `intro` | None | A compact explanation of Fusion's mental model and documentation coverage. |
| `search` | `query`, optional `limit` (default 10, maximum 20) | Ranked documentation titles, URLs, and exact anchors. |
| `get` | `anchor` | The Markdown for one anchor. Small sections are returned in full (including sub-headings); large sections return the immediate text plus links to their sub-headings. |
| `search_expanded` | `query`, optional `limit` (default 5, maximum 10) | Ranked matches expanded through the next heading at the same or a higher level. |

## Source-code tools

Two entry points, mirroring how you would navigate a checkout: locate a file (or a declaration) first, then read it.

| Tool | Inputs | Result |
| --- | --- | --- |
| `source_index` | `pattern`, optional `limit` | Files whose path or top-level type names match a regex — use it to find the file(s) you need. |
| `symbol_search` | `pattern`, optional `limit` | Declarations (types, methods, properties, fields — any accessibility) matching a regex, each with its file and line range to fetch. |
| `source_search` | `query`, optional `context`, `fixedStrings`, `ignoreCase` | `ripgrep` over the source, bounded to 64 KB of output and ~1 s. |
| `source_read` | `file`, optional `startLine`, `endLine` | A whole source file (capped at 64 KB) or a specific line range of it. |

For heavy source navigation, cloning [github.com/ActualLab/Fusion](https://github.com/ActualLab/Fusion) and using your
own tools is usually faster; this MCP is best for conceptual documentation and quick source lookups.

The introduction is also available as a normal page at [ActualLab.Fusion in Brief](mcp-intro.md), but it is intentionally
not included in the documentation sidebar.

## Claude Code

Add the remote server for the current project using Claude Code's default local scope:

```powershell
claude mcp add --transport http fusion-docs https://fusion.actuallab.net/mcp
```

Or add it once at user scope to make it available in every project:

```powershell
claude mcp add --transport http --scope user fusion-docs https://fusion.actuallab.net/mcp
```

Local configuration takes precedence if a server with the same name also exists at user scope.

Verify the configuration:

```powershell
claude mcp get fusion-docs
```

Start Claude Code and run `/mcp` to inspect the connection. You can then ask, for example:

> Use the Fusion documentation MCP server to explain computed-state update delays and show the relevant API types.

To share the server through a repository instead, add this entry to `.mcp.json`:

```json
{
  "mcpServers": {
    "fusion-docs": {
      "type": "http",
      "url": "https://fusion.actuallab.net/mcp"
    }
  }
}
```

## Codex

Codex CLI stores MCP server definitions in `~/.codex/config.toml`. On recent versions you can add the remote server
with a single command:

```powershell
codex mcp add fusion-docs --transport http https://fusion.actuallab.net/mcp
```

Or add the entry to `~/.codex/config.toml` yourself:

```toml
[mcp_servers.fusion-docs]
url = "https://fusion.actuallab.net/mcp"
```

If your Codex build predates native streamable-HTTP support, bridge the remote endpoint through `mcp-remote` as a
stdio server instead:

```toml
[mcp_servers.fusion-docs]
command = "npx"
args = ["-y", "mcp-remote", "https://fusion.actuallab.net/mcp"]
```

List the configured servers to verify:

```powershell
codex mcp list
```

Start Codex and ask, for example:

> Use the Fusion documentation MCP server to explain computed-state update delays and show the relevant API types.

## ChatGPT

ChatGPT currently connects to custom remote MCP servers through developer mode on supported Business, Enterprise, and
Edu workspaces:

1. Have a workspace admin enable developer mode under **Workspace Settings → Permissions & Roles → Connected Data**.
2. Open **Settings → Apps → Advanced Settings** and enable developer mode for your account.
3. Select **Apps → Create** and enter `https://fusion.actuallab.net/mcp` as the MCP endpoint.
4. Select no authentication, then choose **Scan Tools**.
5. Create the app and enable the resulting draft app in a new chat.

The exact labels can change while custom MCP support is in beta. ChatGPT must connect to the deployed HTTPS endpoint; it
cannot connect directly to a server running only on your development machine.

## Suggested Workflow

For an unfamiliar Fusion task, call `intro` once, use `search` to locate likely sections, and then `get` or
`search_expanded` for the most relevant anchors. To work with the implementation, use `source_index` or `symbol_search`
to locate files or declarations, `source_read` to read the exact region, and `source_search` to grep across the source.
This keeps the model context focused while retaining direct links to the complete website documentation and source.
