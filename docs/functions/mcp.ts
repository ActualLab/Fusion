import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { createMcpHandler } from "agents/mcp";
import { z } from "zod";
import index from "../.generated/mcp-index.json";
import {
  renderExpandedSearch,
  renderIntro,
  renderSearch,
  renderSection,
} from "../mcp/search.js";

const textResult = (text: string) => ({ content: [{ type: "text" as const, text }] });

function createServer() {
  const server = new McpServer({
    name: "ActualLab.Fusion Documentation",
    version: "1.0.0",
  });

  server.registerTool(
    "intro",
    {
      description: "Get a concise Markdown introduction to ActualLab.Fusion, its mental model, and the available documentation.",
      inputSchema: {},
    },
    async () => textResult(renderIntro(index)),
  );

  server.registerTool(
    "search",
    {
      description: "Search Fusion documentation and return ranked Markdown links and exact anchors without expanding their content.",
      inputSchema: {
        query: z.string().min(1).describe("Keywords or phrase to find."),
        limit: z.number().int().min(1).max(20).optional().default(10).describe("Number of matches, from 1 through 20."),
      },
    },
    async ({ query, limit }) => textResult(renderSearch(index, query, limit)),
  );

  server.registerTool(
    "get",
    {
      description: "Get the immediate Markdown text under one exact documentation anchor, stopping at the next heading of any level.",
      inputSchema: {
        anchor: z.string().min(1).describe("An anchor returned by search, such as PartF#invalidation or a full documentation URL."),
      },
    },
    async ({ anchor }) => textResult(renderSection(index, anchor)),
  );

  server.registerTool(
    "search_expanded",
    {
      description: "Search Fusion documentation and expand each matched Markdown section through the next heading at the same or a higher level.",
      inputSchema: {
        query: z.string().min(1).describe("Keywords or phrase to find."),
        limit: z.number().int().min(1).max(10).optional().default(5).describe("Number of expanded matches, from 1 through 10."),
      },
    },
    async ({ query, limit }) => textResult(renderExpandedSearch(index, query, limit)),
  );

  return server;
}

interface PagesContext {
  request: Request;
  env: unknown;
  waitUntil(promise: Promise<unknown>): void;
  passThroughOnException(): void;
}

export async function onRequest(context: PagesContext): Promise<Response> {
  const server = createServer();
  const executionContext = {
    waitUntil: context.waitUntil.bind(context),
    passThroughOnException: context.passThroughOnException.bind(context),
  } as ExecutionContext;
  return createMcpHandler(server, {
    route: "/mcp",
    enableJsonResponse: true,
  })(context.request, context.env, executionContext);
}
