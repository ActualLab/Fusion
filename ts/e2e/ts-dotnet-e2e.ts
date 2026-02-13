/**
 * Cross-language E2E test script — TypeScript RPC client ↔ .NET RPC server.
 *
 * Invoked by TypeScriptRpcE2ETest.cs via: npx tsx ts/e2e/ts-dotnet-e2e.ts <scenario>
 * Environment: RPC_SERVER_URL=ws://localhost:<port>/rpc/ws
 *
 * Wire method naming: "ServiceName.MethodName:wireArgCount"
 * wireArgCount = user args + ctOffset (1 for IComputeService methods with CancellationToken)
 */

import WebSocket from "ws";
import {
  RpcClientPeer,
  defineRpcService,
  type WebSocketLike,
} from "@actuallab/rpc";
import { Computed } from "@actuallab/fusion";
import {
  FusionHub,
  defineComputeService,
} from "@actuallab/fusion-rpc";

const serverUrl = process.env["RPC_SERVER_URL"];
if (!serverUrl) {
  console.error("RPC_SERVER_URL not set");
  process.exit(1);
}

const scenario = process.argv[2] ?? "all";

// ---------------------------------------------------------------------------
// Service definitions — using defineRpcService with overload support
// ---------------------------------------------------------------------------

// ITypeScriptTestService (IRpcService — no CancellationToken, ctOffset=0)
// Add has two overloads: Add(a,b) → wire Add:2, Add(a,b,c) → wire Add:3
interface ITypeScriptTestService {
  Add(a: number, b: number): Promise<number>;
  Add(a: number, b: number, c: number): Promise<number>;
  Greet(name: string): Promise<string>;
  Negate(value: boolean): Promise<boolean>;
  Divide(a: number, b: number): Promise<number>;
  Echo(message: string | null): Promise<string | null>;
}

const TestServiceDef = defineRpcService("ITypeScriptTestService", {
  Add: { args: [0, 0] },            // Add:2
  "Add:3": { args: [0, 0, 0] },     // Add:3 (overload)
  Greet: { args: [""] },
  Negate: { args: [false] },
  Divide: { args: [0.0, 0.0] },
  Echo: { args: [""] },
});

// ITypeScriptTestComputeService (IComputeService — has CancellationToken, ctOffset=1)
interface ITypeScriptTestComputeService {
  GetCounter(key: string): Promise<number>;
  Set(key: string, value: number): Promise<void>;
  Increment(key: string): Promise<void>;
}

// Only GetCounter has [ComputeMethod] — Set and Increment are regular methods
const TestComputeServiceDef = defineComputeService("ITypeScriptTestComputeService", {
  GetCounter: { args: [""] },                 // wire: GetCounter:2 (1 arg + ctOffset)
  Set: { args: ["", 0], callTypeId: 0 },      // wire: Set:3 (2 args + ctOffset)
  Increment: { args: [""], callTypeId: 0 },    // wire: Increment:2 (1 arg + ctOffset)
});

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function assert(condition: boolean, label: string): void {
  if (!condition) throw new Error(`Assertion failed: ${label}`);
}

function timeout(ms: number, label: string): Promise<never> {
  return new Promise((_, reject) =>
    setTimeout(() => reject(new Error(label)), ms),
  );
}

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

async function run(): Promise<void> {
  const hub = new FusionHub();
  const clientId = crypto.randomUUID();
  const clientUrl = `${serverUrl}?clientId=${encodeURIComponent(clientId)}&f=json5`;
  const peer = new RpcClientPeer(crypto.randomUUID(), hub, clientUrl);

  // wsFactory: create ws WebSocket (Node.js)
  const wsFactory = (url: string) => new WebSocket(url) as unknown as WebSocketLike;

  // Start connection loop with handshake exchange
  const runPromise = peer.run(wsFactory);

  // Wait for connection + handshake
  await Promise.race([
    new Promise<void>((resolve) => peer.connected.add(() => resolve())),
    timeout(10_000, "Connection timeout"),
  ]);

  // Small delay for handshake exchange to complete
  await delay(100);

  try {
    if (scenario === "basic-types" || scenario === "all")
      await testBasicTypes(hub, peer);
    if (scenario === "overload-resolution" || scenario === "all")
      await testOverloadResolution(hub, peer);
    if (scenario === "compute-invalidation" || scenario === "all")
      await testComputeInvalidation(hub, peer);

    console.log("ALL TESTS PASSED");
  } finally {
    peer.close();
    await runPromise.catch(() => {});
    // Give a moment for cleanup
    await delay(50);
  }
}

// ---------------------------------------------------------------------------
// Test: basic-types — using typed client proxy with clean method names
// ---------------------------------------------------------------------------

async function testBasicTypes(hub: FusionHub, peer: RpcClientPeer): Promise<void> {
  const svc = hub.addClient<ITypeScriptTestService>(peer, TestServiceDef);

  // Add(3, 5) → 8 (2-arg overload)
  const r1 = await svc.Add(3, 5);
  assert(r1 === 8, `Add(3,5) expected 8, got ${r1}`);

  // Add(-1, 1) → 0
  const r2 = await svc.Add(-1, 1);
  assert(r2 === 0, `Add(-1,1) expected 0, got ${r2}`);

  // Greet("World") → "Hello, World!"
  const r3 = await svc.Greet("World");
  assert(r3 === "Hello, World!", `Greet expected "Hello, World!", got "${r3}"`);

  // Negate(true) → false
  const r4 = await svc.Negate(true);
  assert(r4 === false, `Negate(true) expected false, got ${r4}`);

  // Divide(10, 3) → ≈3.333
  const r5 = (await svc.Divide(10.0, 3.0)) as number;
  assert(Math.abs(r5 - 10 / 3) < 0.001, `Divide(10,3) expected ≈3.333, got ${r5}`);

  // Echo(null) → null
  const r6 = await svc.Echo(null as any);
  assert(r6 === null, `Echo(null) expected null, got ${r6}`);

  // Echo("test") → "test"
  const r7 = await svc.Echo("test");
  assert(r7 === "test", `Echo("test") expected "test", got "${r7}"`);

  console.log("  basic-types: PASSED");
}

// ---------------------------------------------------------------------------
// Test: overload-resolution — Add(a,b) vs Add(a,b,c)
// ---------------------------------------------------------------------------

async function testOverloadResolution(hub: FusionHub, peer: RpcClientPeer): Promise<void> {
  const svc = hub.addClient<ITypeScriptTestService>(peer, TestServiceDef);

  // 2-arg overload: Add(3, 5) → wire "ITypeScriptTestService.Add:2" → 8
  const r1 = await svc.Add(3, 5);
  assert(r1 === 8, `Add(3,5) expected 8, got ${r1}`);

  // 3-arg overload: Add(1, 2, 3) → wire "ITypeScriptTestService.Add:3" → 6
  const r2 = await svc.Add(1, 2, 3);
  assert(r2 === 6, `Add(1,2,3) expected 6, got ${r2}`);

  // Verify overloads produce different results for same first two args
  const r3 = await svc.Add(10, 20);
  assert(r3 === 30, `Add(10,20) expected 30, got ${r3}`);

  const r4 = await svc.Add(10, 20, 30);
  assert(r4 === 60, `Add(10,20,30) expected 60, got ${r4}`);

  console.log("  overload-resolution: PASSED");
}

// ---------------------------------------------------------------------------
// Test: compute-invalidation — uses Computed.capture() for clean invalidation tracking
// ---------------------------------------------------------------------------

async function testComputeInvalidation(hub: FusionHub, peer: RpcClientPeer): Promise<void> {
  const svc = hub.addClient<ITypeScriptTestComputeService>(peer, TestComputeServiceDef);

  // 1. Set counter — regular call (non-compute, non-noWait)
  await svc.Set("myKey", 42);

  // 2. Capture the Computed backing GetCounter("myKey")
  const captured = await Computed.capture(() => svc.GetCounter("myKey"));
  assert(captured.value === 42, `GetCounter after Set expected 42, got ${captured.value}`);

  // 3. Increment — triggers server-side invalidation
  await svc.Increment("myKey");

  // 4. Wait for invalidation
  await Promise.race([
    captured.whenInvalidated(),
    timeout(5_000, "Invalidation timeout — $sys-c.Invalidate not received"),
  ]);

  // 5. Re-fetch after invalidation
  const captured2 = await Computed.capture(() => svc.GetCounter("myKey"));
  assert(captured2.value === 43, `GetCounter after Increment expected 43, got ${captured2.value}`);

  console.log("  compute-invalidation: PASSED");
}

// ---------------------------------------------------------------------------
// Entry point
// ---------------------------------------------------------------------------

run().then(
  () => process.exit(0),
  (err) => {
    console.error("FAILED:", err);
    process.exit(1);
  },
);
