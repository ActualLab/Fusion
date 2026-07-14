import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";
import path from "node:path";
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StreamableHTTPClientTransport } from "@modelcontextprotocol/sdk/client/streamableHttp.js";

const serverDir = fileURLToPath(new URL("..", import.meta.url));
const port = Number(process.env.SMOKE_PORT ?? 8099);
const endpoint = new URL(`http://127.0.0.1:${port}/mcp`);

const child = spawn(process.execPath, [path.join(serverDir, "server.mjs")], {
  env: { ...process.env, PORT: String(port), SOURCE_ROOTS: process.env.SOURCE_ROOTS ?? "src" },
  stdio: ["ignore", "pipe", "pipe"],
});
let log = "";
child.stdout.on("data", d => (log += d));
child.stderr.on("data", d => (log += d));

const textOf = result => result.content.filter(c => c.type === "text").map(c => c.text).join("\n");

async function waitReady() {
  for (let attempt = 0; attempt < 80; attempt++) {
    try {
      const response = await fetch(`http://127.0.0.1:${port}/healthz`);
      if (response.ok) return;
    }
    catch {}
    await new Promise(r => setTimeout(r, 250));
  }
  throw new Error(`Server did not start.\n${log}`);
}

function show(title, text) {
  console.log(`\n===== ${title} =====`);
  console.log(text.split("\n").slice(0, 14).join("\n"));
}

try {
  await waitReady();
  const client = new Client({ name: "smoke", version: "1.0.0" });
  await client.connect(new StreamableHTTPClientTransport(endpoint));

  const tools = (await client.listTools()).tools.map(t => t.name).sort();
  console.log("Tools:", tools.join(", "));

  show("source_index /ComputedState/", textOf(await client.callTool({ name: "source_index", arguments: { pattern: "ComputedState", limit: 8 } })));
  show("symbol_search /^Invalidate/", textOf(await client.callTool({ name: "symbol_search", arguments: { pattern: "^Invalidate", limit: 8 } })));
  show("source_search 'IComputed'", textOf(await client.callTool({ name: "source_search", arguments: { query: "interface IComputed", context: 1 } })));
  show("source_read Result.cs 11-24", textOf(await client.callTool({ name: "source_read", arguments: { file: "src/ActualLab.Core/Result.cs", startLine: 11, endLine: 24 } })));
  show("get glossary#cascading-invalidation", textOf(await client.callTool({ name: "get", arguments: { anchor: "glossary#cascading-invalidation" } })));

  await client.close();
  console.log("\nSMOKE OK:", tools.length, "tools");
}
finally {
  child.kill("SIGKILL");
}
