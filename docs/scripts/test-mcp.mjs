import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";
import path from "node:path";
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StreamableHTTPClientTransport } from "@modelcontextprotocol/sdk/client/streamableHttp.js";

const port = 8790;
const endpoint = new URL(`http://127.0.0.1:${port}/mcp`);
const wranglerPath = new URL("../node_modules/wrangler/bin/wrangler.js", import.meta.url);
const docsDirectory = fileURLToPath(new URL("..", import.meta.url));
const tempDirectory = path.resolve(docsDirectory, "../tmp");
const server = spawn(process.execPath, [
  fileURLToPath(wranglerPath),
  "pages", "dev", ".vitepress/dist",
  "--port", String(port),
], {
  cwd: docsDirectory,
  env: {
    ...process.env,
    WRANGLER_LOG_PATH: path.join(tempDirectory, "wrangler-mcp-test.log"),
    XDG_CONFIG_HOME: path.join(tempDirectory, "wrangler-config"),
  },
  stdio: ["ignore", "pipe", "pipe"],
});
let output = "";
server.stdout.on("data", chunk => output += chunk);
server.stderr.on("data", chunk => output += chunk);

async function waitUntilReady() {
  for (let attempt = 0; attempt < 60; attempt++) {
    try {
      const response = await fetch(endpoint, { headers: { Accept: "text/event-stream" } });
      if (response.status < 500) {
        await response.body?.cancel();
        return;
      }
    }
    catch {}
    await new Promise(resolve => setTimeout(resolve, 250));
  }
  throw new Error(`Wrangler did not start.\n${output}`);
}

function textOf(result) {
  return result.content.filter(item => item.type === "text").map(item => item.text).join("\n");
}

try {
  await waitUntilReady();
  const client = new Client({ name: "fusion-docs-mcp-test", version: "1.0.0" });
  await client.connect(new StreamableHTTPClientTransport(endpoint));

  const tools = await client.listTools();
  const names = tools.tools.map(tool => tool.name).sort();
  const expected = ["get", "intro", "search", "search_expanded"];
  if (JSON.stringify(names) !== JSON.stringify(expected))
    throw new Error(`Unexpected tools: ${names.join(", ")}`);

  const intro = textOf(await client.callTool({ name: "intro", arguments: {} }));
  if (!intro.includes("ActualLab.Fusion") || !intro.includes("Computed<T>"))
    throw new Error("The intro tool returned incomplete content.");

  const search = textOf(await client.callTool({ name: "search", arguments: { query: "ShardMapBuilder", limit: 3 } }));
  if (!search.includes("api-index-full#shardmapbuilder-abstract-record"))
    throw new Error("The search tool did not return the expected API anchor.");

  const section = textOf(await client.callTool({
    name: "get",
    arguments: { anchor: "api-index-full#shardmapbuilder-abstract-record" },
  }));
  if (!section.includes("shard-to-node index maps"))
    throw new Error("The get tool did not return the expected API description.");

  const genericSection = textOf(await client.callTool({
    name: "get",
    arguments: { anchor: "https://fusion.actuallab.net/PartF-C#computed-lt-t-gt-the-core-abstraction" },
  }));
  if (!genericSection.includes("This document covers `Computed<T>`"))
    throw new Error("The get tool did not resolve a full URL with a generic-type heading.");

  if (!section.includes("Base URL: https://fusion.actuallab.net/"))
    throw new Error("The get tool did not include the Base URL line.");

  const truncated = textOf(await client.callTool({
    name: "get",
    arguments: { anchor: "ActualLab.Fusion-vs/AkkaNET#actuallab-fusion-vs-akka-net" },
  }));
  if (!truncated.includes("truncated to sub-headers only")
    || !truncated.includes("](https://fusion.actuallab.net/ActualLab.Fusion-vs/AkkaNET#"))
    throw new Error("The get tool did not truncate a large section to sub-heading links.");

  const expanded = textOf(await client.callTool({
    name: "search_expanded",
    arguments: { query: "Cascading invalidation", limit: 2 },
  }));
  if (!expanded.includes("glossary#cascading-invalidation") || !expanded.includes("Automatic propagation"))
    throw new Error("The expanded search tool did not return the expected glossary section.");
  if (!expanded.includes("Base URL: https://fusion.actuallab.net/"))
    throw new Error("The expanded search tool did not include the Base URL line.");

  await client.close();
  console.log(`MCP integration test passed: ${names.join(", ")}.`);
}
finally {
  server.kill();
}
