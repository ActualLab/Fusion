# Deployment

Hosts the Fusion documentation site **and** the documentation/source MCP server
as a single website on the shared VM, behind Cloudflare:

- `fusion.actuallab.net` → static VitePress docs (served by `fusion-app`)
- `fusion.actuallab.net/mcp` → MCP server (StreamableHTTP): docs tools
  (`intro`, `search`, `get`, `search_expanded`) + source tools (`source_index`,
  `symbol_search`, `source_search`, `source_read`)

This replaces the old Cloudflare Pages deployment (static site + `functions/mcp.ts`
Pages Function). One Node process serves both the static site and `/mcp`; the
source tools run `ripgrep` over the Fusion source baked into the image.

## Topology

```
Browser ─HTTPS─> Cloudflare (proxied) ─HTTPS─> edge Caddy :443 ─HTTP─> fusion-app :8080
```

The **edge Caddy** is the one from the BoardGames deployment on the same VM: it
owns 80/443, terminates TLS with the Cloudflare wildcard Origin cert
(`*.actuallab.net`), and reverse-proxies each subdomain to the matching
container. `fusion-app` publishes no ports and joins that Caddy's network
(`boardgames_default`, referenced here as the external `edge` network). The
`fusion.actuallab.net` route lives in the BoardGames repo's `deploy/Caddyfile`.

## Build model

Built in-image on the VM (`docker compose up -d --build`), the same poll-based
model as TownHall / BoardGames / Fusion.Samples. The multi-stage
`docs/mcp-server/Dockerfile`:

1. **snippets** (`dotnet/sdk`) — `dotnet mdsnippets` refreshes MarkdownSnippets
   in `docs/**/*.md`. Depends on `docs/` only, so `src/`-only commits are a cache
   hit here.
2. **site** (`node`) — `npm ci` + `npm run docs:build` (mcp:index + slides +
   VitePress + redirects) produces `docs/.vitepress/dist` and the docs MCP index.
3. **runtime** (`node-slim` + `ripgrep`) — the MCP server + static host, with
   `src/`, `samples/`, `tests/` baked in for the source tools.

CPU/memory are capped (`cpus: 1.5`, `mem_limit: 640m`) so an MCP search can never
starve the other apps on the host.

## First-time host setup

```bash
git clone https://github.com/ActualLab/Fusion /opt/apps/fusion
cd /opt/apps/fusion/deploy
docker compose -f docker-compose.prod.yml up -d --build
```

Add the subdomain to the edge Caddyfile (in the BoardGames repo's
`deploy/Caddyfile`) and point Cloudflare DNS `fusion.actuallab.net` at the VM
(proxied A record). The wildcard Origin cert already covers the host.

## Auto-deploy on push

A systemd timer polls `origin/master` every 2 minutes and rebuilds + restarts
when it moves.

```bash
sudo cp systemd/fusion-deploy.* /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now fusion-deploy.timer
```

Force a deploy immediately: `deploy/deploy.sh --force`.
