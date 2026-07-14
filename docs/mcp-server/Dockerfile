# Fusion documentation site + MCP server, deployed at fusion.actuallab.net on the
# shared VM. Built in-image on the VM (git-poll + `docker compose up -d --build`),
# the same model the TownHall / BoardGames / Fusion.Samples deployments use.
#
# Stages are ordered so the common case — a commit that touches src/ only — is a
# cache hit for the two expensive stages (snippets, site) and rebuilds just the
# final source copy. Docs changes rebuild the site stage; snippet-source changes
# (which live under docs/) rebuild the snippets stage.

# 1) Refresh MarkdownSnippets in place (docs/**/*.md). Depends on docs/ only.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS snippets
WORKDIR /repo
COPY global.json ./
COPY .config/ ./.config/
COPY docs/ ./docs/
# Pass the target dir explicitly so mdsnippets doesn't require a .git repo
# (the build context excludes .git to stay small).
RUN dotnet tool restore \
    && dotnet mdsnippets docs

# 2) Build the VitePress site + docs MCP index (mcp-index.json). Node only.
FROM node:22-bookworm AS site
WORKDIR /repo/docs
COPY docs/package.json docs/package-lock.json ./
RUN npm ci
COPY --from=snippets /repo/docs/ /repo/docs/
RUN npm run docs:build

# 3) Runtime: static docs host + MCP server + ripgrep over the Fusion source.
FROM node:22-bookworm-slim AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends ripgrep util-linux \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app

COPY docs/mcp-server/package.json docs/mcp-server/package-lock.json ./docs/mcp-server/
RUN cd docs/mcp-server && npm ci --omit=dev

COPY docs/mcp-server/ ./docs/mcp-server/
COPY docs/mcp/ ./docs/mcp/
COPY --from=site /repo/docs/.vitepress/dist/ ./docs/.vitepress/dist/
COPY --from=site /repo/docs/.generated/mcp-index.json ./docs/.generated/mcp-index.json
# Fusion source served by the source_* / symbol_search tools (rg + indexes).
COPY src/ ./src/
COPY samples/ ./samples/
COPY tests/ ./tests/

ENV NODE_ENV=production \
    PORT=8080 \
    RG_NICE=1 \
    SOURCE_ROOTS=src,samples,tests
EXPOSE 8080
ENTRYPOINT ["node", "docs/mcp-server/server.mjs"]
