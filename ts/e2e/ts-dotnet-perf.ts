/**
 * Cross-language performance test -TypeScript RPC client <-> .NET RPC server.
 *
 * Invoked by TypeScriptRpcPerfTest.cs via: npx tsx ts/e2e/ts-dotnet-perf.ts <scenario>
 * Environment:
 *   RPC_SERVER_URL  = ws://localhost:<port>/rpc/ws
 *   WORKER_COUNT    = number of concurrent workers (default: 50)
 *   ITER_COUNT      = iterations per worker per run (default: 2000)
 *
 * All methods live on ITypeScriptTestComputeService (IComputeService, all have CancellationToken).
 *
 * Scenarios:
 *   rpc                -plain RPC call (Add, non-compute method, callTypeId=0)
 *   compute-rpc-unique -remote compute call, unique args (cache miss → server round-trip)
 *   compute-rpc-same   -remote compute call, same arg (cache hit after first RPC round-trip)
 *   compute            -client-side cached compute call, same arg (no RPC after first call)
 *   stream             -streaming RPC call (StreamInt32, no compute)
 *   all                -all tests
 */

import WebSocket from "ws";
import {
  RpcClientPeer,
  RpcType,
  defineRpcService,
  type WebSocketLike,
} from "@actuallab/rpc";
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
const workerCount = parseInt(process.env["WORKER_COUNT"] ?? "50", 10);
const iterCount = parseInt(process.env["ITER_COUNT"] ?? "2000", 10);

// ---------------------------------------------------------------------------
// Service definition -single service for all perf tests
// ---------------------------------------------------------------------------

interface IPerfService {
  Add(a: number, b: number): Promise<number>;
  GetValue(value: number): Promise<number>;
}

interface IStreamPerfService {
  StreamInt32(count: number): Promise<AsyncIterable<number>>;
}

const PerfServiceDef = defineComputeService("ITypeScriptTestComputeService", {
  Add: { args: [0, 0], callTypeId: 0 },  // non-compute method
  GetValue: { args: [0] },               // [ComputeMethod] -default callTypeId
});

const StreamPerfServiceDef = defineRpcService("ITypeScriptTestComputeService", {
  StreamInt32: { args: [0], returns: RpcType.stream, wireArgCount: 1 },  // no CT
});

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function timeout(ms: number, label: string): Promise<never> {
  return new Promise((_, reject) =>
    setTimeout(() => reject(new Error(label)), ms),
  );
}

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

// ---------------------------------------------------------------------------
// Benchmark runner
// ---------------------------------------------------------------------------

async function runBenchmark(
  name: string,
  runFn: (workerCount: number, iterCount: number) => Promise<void>,
  wc: number,
  ic: number,
): Promise<void> {
  const warmupIc = Math.max(1, Math.floor(ic / 10));
  const totalWarmup = wc * warmupIc;
  const totalPerRun = wc * ic;

  // Warmup
  console.log(`  ${name}: warmup (${wc} x ${warmupIc} = ${totalWarmup} calls)...`);
  await runFn(wc, warmupIc);

  // 3 measured runs
  let bestOpsPerSec = 0;
  for (let run = 1; run <= 3; run++) {
    const start = performance.now();
    await runFn(wc, ic);
    const elapsedSec = (performance.now() - start) / 1000;
    const opsPerSec = totalPerRun / elapsedSec;
    console.log(`  ${name}: run ${run} - ${opsPerSec.toFixed(0)} ops/s (${totalPerRun} calls in ${elapsedSec.toFixed(3)}s)`);
    if (opsPerSec > bestOpsPerSec) bestOpsPerSec = opsPerSec;
  }

  console.log(`  ${name}: BEST = ${bestOpsPerSec.toFixed(0)} ops/s`);
}

// ---------------------------------------------------------------------------
// RPC performance test -Add(i, i), non-compute method
// ---------------------------------------------------------------------------

async function testRpcPerformance(svc: IPerfService): Promise<void> {
  const check = await svc.Add(3, 5);
  if (check !== 8) throw new Error(`Sanity check failed: Add(3,5) = ${check}`);

  await runBenchmark("rpc", async (wc, ic) => {
    const workers: Promise<void>[] = [];
    for (let w = 0; w < wc; w++) {
      workers.push((async () => {
        for (let i = 0; i < ic; i++) {
          await svc.Add(i, i);
        }
      })());
    }
    await Promise.all(workers);
  }, workerCount, iterCount);
}

// ---------------------------------------------------------------------------
// Compute-RPC (unique args) -GetValue(unique), cache miss → server round-trip each time
// ---------------------------------------------------------------------------

let computeRpcUniqueOffset = 0;

async function testComputeRpcUnique(svc: IPerfService): Promise<void> {
  const check = await svc.GetValue(42);
  if (check !== 42) throw new Error(`Sanity check failed: GetValue(42) = ${check}`);

  await runBenchmark("compute-rpc-unique", async (wc, ic) => {
    const base = computeRpcUniqueOffset;
    computeRpcUniqueOffset += wc * ic;

    const workers: Promise<void>[] = [];
    for (let w = 0; w < wc; w++) {
      const workerBase = base + w * ic;
      workers.push((async () => {
        for (let i = 0; i < ic; i++) {
          await svc.GetValue(workerBase + i);
        }
      })());
    }
    await Promise.all(workers);
  }, workerCount, iterCount);
}

// ---------------------------------------------------------------------------
// Compute-RPC (same arg) -GetValue(0), cached on server after first call
// ---------------------------------------------------------------------------

async function testComputeRpcSame(svc: IPerfService): Promise<void> {
  const check = await svc.GetValue(0);
  if (check !== 0) throw new Error(`Sanity check failed: GetValue(0) = ${check}`);

  await runBenchmark("compute-rpc-same", async (wc, ic) => {
    const workers: Promise<void>[] = [];
    for (let w = 0; w < wc; w++) {
      workers.push((async () => {
        for (let i = 0; i < ic; i++) {
          await svc.GetValue(0);
        }
      })());
    }
    await Promise.all(workers);
  }, workerCount, iterCount);
}

// ---------------------------------------------------------------------------
// Compute performance test -GetValue(0), cache hit after first call
// ---------------------------------------------------------------------------

async function testComputePerformance(svc: IPerfService): Promise<void> {
  const check = await svc.GetValue(0);
  if (check !== 0) throw new Error(`Sanity check failed: GetValue(0) = ${check}`);

  await runBenchmark("compute", async (wc, ic) => {
    const workers: Promise<void>[] = [];
    for (let w = 0; w < wc; w++) {
      workers.push((async () => {
        for (let i = 0; i < ic; i++) {
          await svc.GetValue(0);
        }
      })());
    }
    await Promise.all(workers);
  }, workerCount, iterCount);
}

// ---------------------------------------------------------------------------
// Stream performance test -StreamInt32(count), consume entire stream
// ---------------------------------------------------------------------------

async function testStreamPerformance(svc: IStreamPerfService): Promise<void> {
  // Sanity check: stream 5 items
  const checkStream = await svc.StreamInt32(5);
  const checkItems: number[] = [];
  for await (const item of checkStream) checkItems.push(item);
  if (checkItems.length !== 5 || checkItems[4] !== 4)
    throw new Error(`Sanity check failed: StreamInt32(5) = [${checkItems}]`);

  const itemsPerStream = 1000;

  await runBenchmark("stream", async (wc, ic) => {
    const workers: Promise<void>[] = [];
    for (let w = 0; w < wc; w++) {
      workers.push((async () => {
        for (let i = 0; i < ic; i++) {
          const stream = await svc.StreamInt32(itemsPerStream);
          // Consume entire stream — count items to prevent optimization
          let count = 0;
          for await (const _ of stream) count++;
          if (count !== itemsPerStream)
            throw new Error(`Expected ${itemsPerStream} items, got ${count}`);
        }
      })());
    }
    await Promise.all(workers);
  }, workerCount, iterCount);
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

async function run(): Promise<void> {
  const hub = new FusionHub();
  const peer = new RpcClientPeer(hub, serverUrl);
  const wsFactory = (url: string) => new WebSocket(url) as unknown as WebSocketLike;
  const runPromise = peer.run(wsFactory);

  // Wait for connection + handshake
  await Promise.race([
    new Promise<void>((resolve) => peer.connected.add(() => resolve())),
    timeout(10_000, "Connection timeout"),
  ]);
  await delay(100);

  const svc = hub.addClient<IPerfService>(peer, PerfServiceDef);
  const streamSvc = hub.addClient<IStreamPerfService>(peer, StreamPerfServiceDef);

  console.log(`Configuration: ${workerCount} workers x ${iterCount} iterations = ${workerCount * iterCount} calls/run`);

  try {
    if (scenario === "rpc" || scenario === "all")
      await testRpcPerformance(svc);
    if (scenario === "compute-rpc-unique" || scenario === "all")
      await testComputeRpcUnique(svc);
    if (scenario === "compute-rpc-same" || scenario === "all")
      await testComputeRpcSame(svc);
    if (scenario === "compute" || scenario === "all")
      await testComputePerformance(svc);
    if (scenario === "stream" || scenario === "all")
      await testStreamPerformance(streamSvc);

    console.log("PERF TEST COMPLETE");
  } finally {
    peer.close();
    await runPromise.catch(() => {});
    await delay(50);
  }
}

run().then(
  () => process.exit(0),
  (err) => {
    console.error("FAILED:", err);
    process.exit(1);
  },
);
