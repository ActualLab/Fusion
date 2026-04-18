/**
 * Cross-language E2E test script — TypeScript RPC client ↔ .NET RPC server.
 *
 * Invoked by TypeScriptRpcE2ETest.cs via: npx tsx ts/e2e/ts-dotnet-e2e.ts <scenario>
 * Environment: RPC_SERVER_URL=ws://localhost:<port>/rpc/ws
 *
 * Wire method naming: "ServiceName.MethodName:wireArgCount"
 * wireArgCount = args.length + 1 (CancellationToken slot) by default,
 * override with argCount for methods without CT.
 */

import WebSocket from 'ws';
import {
    RpcClientPeer,
    RpcPeerRefBuilder,
    RpcType,
    defineRpcService,
    type RpcConnectionState,
    type WebSocketLike,
} from '@actuallab/rpc';
import { Computed, ComputedState, FixedDelayer } from '@actuallab/fusion';
import {
    FusionHub,
    defineComputeService,
} from '@actuallab/fusion-rpc';

// RpcConnectionState.Connected — inlined to avoid `verbatimModuleSyntax`
// restriction on cross-package `const enum` imports.
const RPC_CONNECTED = 2 as RpcConnectionState;

const serverUrlEnv = process.env['RPC_SERVER_URL'];
if (!serverUrlEnv) {
    console.error('RPC_SERVER_URL not set');
    process.exit(1);
}
const serverUrl: string = serverUrlEnv;

const rpcFormat = process.env['RPC_FORMAT'] ?? 'json5np';
const scenario = process.argv[2] ?? 'all';

// ---------------------------------------------------------------------------
// Service definitions — using defineRpcService with overload support
// ---------------------------------------------------------------------------

// ITypeScriptTestService (IRpcService — no CancellationToken on any method)
// Add has two overloads: Add(a,b) → wire Add:2, Add(a,b,c) → wire Add:3
interface ITypeScriptTestService {
  Add(a: number, b: number): Promise<number>;
  // eslint-disable-next-line @typescript-eslint/unified-signatures
  Add(a: number, b: number, c: number): Promise<number>;
  Greet(name: string): Promise<string>;
  Negate(value: boolean): Promise<boolean>;
  Divide(a: number, b: number): Promise<number>;
  Echo(message: string | null): Promise<string | null>;
  SlowEcho(marker: string, delayMs: number): Promise<string>;
  GetSlowEchoInvocationCount(): Promise<number>;
  ResetSlowEchoCounter(): Promise<void>;
}

const TestServiceDef = defineRpcService('ITypeScriptTestService', {
    Add: { args: [0, 0], wireArgCount: 2 },            // no CT
    'Add:3': { args: [0, 0, 0], wireArgCount: 3 },     // no CT (overload)
    Greet: { args: [''], wireArgCount: 1 },
    Negate: { args: [false], wireArgCount: 1 },
    Divide: { args: [0.0, 0.0], wireArgCount: 2 },
    Echo: { args: [''], wireArgCount: 1 },
    SlowEcho: { args: ['', 0], wireArgCount: 2 },
    GetSlowEchoInvocationCount: { args: [], wireArgCount: 0 },
    ResetSlowEchoCounter: { args: [], wireArgCount: 0 },
});

// ITypeScriptTestComputeService (IComputeService — all methods have CancellationToken)
interface ITypeScriptTestComputeService {
  GetCounter(key: string): Promise<number>;
  Set(key: string, value: number): Promise<void>;
  Increment(key: string): Promise<void>;
  GetCounterNonCompute(key: string): Promise<number>;
  StreamInt32(count: number): Promise<AsyncIterable<number>>;
  StreamInt32NoReconnect(count: number): Promise<AsyncIterable<number>>;
}

// Only GetCounter has [ComputeMethod] — Set and Increment are regular methods
const TestComputeServiceDef = defineComputeService('ITypeScriptTestComputeService', {
    GetCounter: { args: [''] },
    Set: { args: ['', 0], callTypeId: 0 },
    Increment: { args: [''], callTypeId: 0 },
    GetCounterNonCompute: { args: [''], callTypeId: 0 },
    StreamInt32: { args: [0], wireArgCount: 1, callTypeId: 0, returns: RpcType.stream },
    StreamInt32NoReconnect: { args: [0], wireArgCount: 1, callTypeId: 0, returns: RpcType.stream },
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

/** Resolve on the NEXT transition into `Connected`, ignoring the current
 *  state — mirrors the old `peer.connected` event semantics. */
function whenNextConnected(peer: RpcClientPeer): Promise<void> {
    return new Promise<void>((resolve) => {
        const handler = (state: RpcConnectionState): void => {
            if (state !== RPC_CONNECTED) return;
            peer.connectionStateChanged.remove(handler);
            resolve();
        };
        peer.connectionStateChanged.add(handler);
    });
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

async function run(): Promise<void> {
    console.log(`  format: ${rpcFormat}`);
    const hub = new FusionHub();
    // Bake the serialization format into the URL via ?f=... so the peer ctor
    // picks it up. Pass `mustStart=false` — we need to set webSocketFactory first.
    const peer = new RpcClientPeer(hub, RpcPeerRefBuilder.forClient(serverUrl, rpcFormat), false);

    // wsFactory: create ws WebSocket (Node.js)
    peer.webSocketFactory = (url: string) => new WebSocket(url) as unknown as WebSocketLike;

    // Start connection loop with handshake exchange
    peer.start();
    const runPromise = peer.whenRunning;

    // Wait for connection + handshake
    await Promise.race([
        peer.whenConnected(),
        timeout(10_000, 'Connection timeout'),
    ]);

    // Small delay for handshake exchange to complete
    await delay(100);

    try {
        if (scenario === 'basic-types' || scenario === 'all')
            await testBasicTypes(hub, peer);
        if (scenario === 'overload-resolution' || scenario === 'all')
            await testOverloadResolution(hub, peer);
        if (scenario === 'compute-invalidation' || scenario === 'all')
            await testComputeInvalidation(hub, peer);
        if (scenario === 'auto-reconnect' || scenario === 'all')
            await testAutoReconnect(hub, peer);
        if (scenario === 'reconnect-no-duplicate' || scenario === 'all')
            await testReconnectNoDuplicate(hub, peer);
        if (scenario === 'stream-no-reconnect' || scenario === 'all')
            await testStreamNoReconnect(hub, peer);
        if (scenario === 'reconnection-torture')
            await testReconnectionTorture(hub, peer);
        if (scenario === 'server-restart')
            await testServerRestart(hub, peer);

        console.log('ALL TESTS PASSED');
    } finally {
        peer.close();
        await runPromise?.catch(() => { /* noop */ });
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
    const r3 = await svc.Greet('World');
    assert(r3 === 'Hello, World!', `Greet expected "Hello, World!", got "${r3}"`);

    // Negate(true) → false
    const r4 = await svc.Negate(true);
    assert(!r4, `Negate(true) expected false, got ${r4}`);

    // Divide(10, 3) → ≈3.333
    const r5 = (await svc.Divide(10.0, 3.0));
    assert(Math.abs(r5 - 10 / 3) < 0.001, `Divide(10,3) expected ≈3.333, got ${r5}`);

    // Echo(null) → null
    // eslint-disable-next-line @typescript-eslint/no-explicit-any, @typescript-eslint/no-unsafe-argument
    const r6 = await svc.Echo(null as any);
    assert(r6 === null, `Echo(null) expected null, got ${r6}`);

    // Echo("test") → "test"
    const r7 = await svc.Echo('test');
    assert(r7 === 'test', `Echo("test") expected "test", got "${r7}"`);

    console.log('  basic-types: PASSED');
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

    console.log('  overload-resolution: PASSED');
}

// ---------------------------------------------------------------------------
// Test: compute-invalidation — uses Computed.capture() for clean invalidation tracking
// ---------------------------------------------------------------------------

async function testComputeInvalidation(hub: FusionHub, peer: RpcClientPeer): Promise<void> {
    const svc = hub.addClient<ITypeScriptTestComputeService>(peer, TestComputeServiceDef);

    // 1. Set counter — regular call (non-compute, non-noWait)
    await svc.Set('myKey', 42);

    // 2. Capture the Computed backing GetCounter("myKey")
    const captured = await Computed.capture(() => svc.GetCounter('myKey'));
    assert(captured.value === 42, `GetCounter after Set expected 42, got ${captured.value}`);

    // 3. Increment — triggers server-side invalidation
    await svc.Increment('myKey');

    // 4. Wait for invalidation
    await Promise.race([
        captured.whenInvalidated(),
        timeout(5_000, 'Invalidation timeout — $sys-c.Invalidate not received'),
    ]);

    // 5. Re-fetch after invalidation
    const captured2 = await Computed.capture(() => svc.GetCounter('myKey'));
    assert(captured2.value === 43, `GetCounter after Increment expected 43, got ${captured2.value}`);

    console.log('  compute-invalidation: PASSED');
}

// ---------------------------------------------------------------------------
// Test: stream-no-reconnect — AllowReconnect=false stream fails on disconnect
// ---------------------------------------------------------------------------

async function testStreamNoReconnect(hub: FusionHub, peer: RpcClientPeer): Promise<void> {
    const svc = hub.addClient<ITypeScriptTestComputeService>(peer, TestComputeServiceDef);

    // 1. Verify normal stream works
    const normalStream = await svc.StreamInt32(5);
    const normalItems: number[] = [];
    for await (const item of normalStream) normalItems.push(item);
    assert(normalItems.length === 5, `StreamInt32(5) expected 5 items, got ${normalItems.length}`);
    assert(normalItems[4] === 4, `StreamInt32(5) last item expected 4, got ${normalItems[4]}`);

    // 2. Verify AllowReconnect=false stream works when connection is stable
    const stableStream = await svc.StreamInt32NoReconnect(5);
    const stableItems: number[] = [];
    for await (const item of stableStream) stableItems.push(item);
    assert(stableItems.length === 5, `StreamInt32NoReconnect(5) expected 5 items, got ${stableItems.length}`);

    // 3. Start a large AllowReconnect=false stream, then disconnect mid-stream
    const stream = await svc.StreamInt32NoReconnect(100_000);
    const items: number[] = [];
    let gotError = false;

    // Set up reconnection waiter
    const reconnectPromise = whenNextConnected(peer);

    // Disconnect after a short delay to ensure some items arrive
    setTimeout(() => peer.connection?.close(), 50);

    try {
        for await (const item of stream) {
            items.push(item);
        }
    } catch (e) {
        gotError = true;
        assert(
            (e as Error).message.includes('disconnected'),
            `Expected disconnect error, got: ${(e as Error).message}`,
        );
    }

    assert(gotError, 'AllowReconnect=false stream should have thrown on disconnect');
    assert(items.length > 0, 'Should have received some items before disconnect');
    assert(items.length < 100_000, `Should not have received all items (got ${items.length})`);

    // 4. Wait for auto-reconnection so the peer is ready for further tests
    await Promise.race([reconnectPromise, timeout(10_000, 'Auto-reconnection timeout')]);
    await delay(200);

    console.log(`  stream-no-reconnect: PASSED (received ${items.length} items before disconnect)`);
}

// ---------------------------------------------------------------------------
// Test: auto-reconnect — verifies RPC client auto-reconnects after disconnect
// ---------------------------------------------------------------------------

async function testAutoReconnect(hub: FusionHub, peer: RpcClientPeer): Promise<void> {
    const svc = hub.addClient<ITypeScriptTestComputeService>(peer, TestComputeServiceDef);
    const basicSvc = hub.addClient<ITypeScriptTestService>(peer, TestServiceDef);

    // 1. Set initial counter value and capture computed
    await svc.Set('reconnKey', 100);
    const captured = await Computed.capture(() => svc.GetCounter('reconnKey'));
    assert(captured.value === 100, `Expected 100 before disconnect, got ${captured.value}`);
    assert(captured.isConsistent, 'Computed should be consistent before disconnect');

    // 2. Set up reconnection waiter before disconnecting
    const reconnectPromise = whenNextConnected(peer);

    // 3. Close WebSocket to simulate disconnect
    peer.connection?.close();

    // 4. Wait for auto-reconnection (run() loop reconnects automatically)
    await Promise.race([reconnectPromise, timeout(10_000, 'Auto-reconnection timeout')]);
    await delay(200); // Wait for handshake exchange to complete

    // 5. Verify the captured computed was invalidated on reconnect
    //    (invalidateAll() during reconnect resolves whenInvalidated, triggering invalidation)
    await Promise.race([
        captured.whenInvalidated(),
        timeout(1_000, 'Expected captured computed to be invalidated on reconnect'),
    ]);

    // 6. Verify basic RPC calls work after reconnection
    const addResult = await basicSvc.Add(10, 20);
    assert(addResult === 30, `Add(10,20) after reconnect expected 30, got ${addResult}`);

    // 7. Re-fetch counter value — should still be 100
    const captured2 = await Computed.capture(() => svc.GetCounter('reconnKey'));
    assert(captured2.value === 100, `GetCounter after reconnect expected 100, got ${captured2.value}`);

    // 8. Verify compute invalidation still works after reconnection
    await svc.Increment('reconnKey');
    await Promise.race([
        captured2.whenInvalidated(),
        timeout(5_000, 'Post-reconnect invalidation timeout'),
    ]);

    // 9. Confirm updated value
    const captured3 = await Computed.capture(() => svc.GetCounter('reconnKey'));
    assert(captured3.value === 101, `GetCounter after post-reconnect Increment expected 101, got ${captured3.value}`);

    console.log('  auto-reconnect: PASSED');
}

// ---------------------------------------------------------------------------
// Test: reconnect-no-duplicate — verifies the $sys.Reconnect:3 protocol
// prevents the server from invoking a long-running call's handler twice
// when the WebSocket bounces while the call is still in flight.
//
// This is the end-to-end guard for Bug 3 (see docs/RPC_TS_FIXES): TS client
// must group in-flight call IDs by completedStage, compress them with
// IncreasingSeqCompressor, and send them to the .NET server as
// Dictionary<int, byte[]> in a format the .NET server can deserialize
// (JSON: {"stage":"base64"}, MessagePack: map<int, bin>). The .NET server
// replies with a byte[] of unknown IDs; TS only resends those.
//
// With the bug: every reconnect spawns a second invocation on the server —
// for streaming calls like PushAudio this meant every audio frame was
// processed twice. With the fix the server sees exactly one invocation.
// ---------------------------------------------------------------------------

async function testReconnectNoDuplicate(hub: FusionHub, peer: RpcClientPeer): Promise<void> {
    const svc = hub.addClient<ITypeScriptTestService>(peer, TestServiceDef);

    // 1. Start from a clean counter.
    await svc.ResetSlowEchoCounter();
    const before = await svc.GetSlowEchoInvocationCount();
    assert(before === 0, `Counter should start at 0, got ${before}`);

    // 2. Kick off a slow call; hold the promise without awaiting.
    //    The delay must cover:
    //      * pre-disconnect observation window (100ms)
    //      * the client's auto-reconnect back-off (~1s default)
    //      * post-reconnect $sys.Reconnect round-trip (~200ms)
    //      * enough slack after to let us assert the handler is still running.
    //    3 seconds is comfortably above the ~1.3s worst-case.
    const slowDelayMs = 3_000;
    const slowPromise = svc.SlowEcho('hello', slowDelayMs);
    slowPromise.catch(() => { /* prevent unhandled-rejection noise */ });

    // 3. Give the server enough time to register the inbound call.
    await delay(100);

    // 4. Force a same-peer reconnect in the middle of the slow call.
    //    .NET server's inbound tracker still has the call, so when TS
    //    sends $sys.Reconnect the server answers "I know this call",
    //    and the client MUST NOT resend.
    const reconnectPromise = whenNextConnected(peer);
    peer.connection?.close();
    await Promise.race([reconnectPromise, timeout(15_000, 'Reconnect timeout')]);
    await delay(200); // wait for handshake + $sys.Reconnect round-trip

    // 5. The original call should resolve normally.
    const result = await Promise.race([
        slowPromise,
        timeout(10_000, 'SlowEcho did not complete after reconnect'),
    ]);
    assert(result === 'hello', `SlowEcho expected 'hello', got '${result}'`);

    // 6. THE invariant: the server invoked SlowEcho exactly ONCE.
    //    Without the $sys.Reconnect protocol the client would blind-resend,
    //    and the server would start a second SlowEcho handler — counter = 2.
    const after = await svc.GetSlowEchoInvocationCount();
    assert(after === 1,
        `Server invocation count should be 1 after reconnect, got ${after} — ` +
        'suggests client blind-resent the call instead of honoring the ' +
        '$sys.Reconnect protocol.');

    console.log('  reconnect-no-duplicate: PASSED');
}

// ---------------------------------------------------------------------------
// Test: reconnection-torture — stress-tests eventual consistency across many disconnects
// ---------------------------------------------------------------------------

async function testReconnectionTorture(hub: FusionHub, peer: RpcClientPeer): Promise<void> {
    const svc = hub.addClient<ITypeScriptTestComputeService>(peer, TestComputeServiceDef);

    // Helper: wait for connection to be fully established (WS open + handshake)
    async function waitForConnection(): Promise<void> {
        if (peer.isConnected) {
            await delay(100);
            return;
        }
        await Promise.race([
            whenNextConnected(peer),
            timeout(10_000, 'Reconnection timeout'),
        ]);
        await delay(200); // wait for handshake exchange
    }

    // Helper: disconnect and wait for auto-reconnection
    async function disconnectAndReconnect(): Promise<void> {
        const reconnectPromise = whenNextConnected(peer);
        peer.connection?.close();
        await Promise.race([reconnectPromise, timeout(10_000, 'Reconnection timeout')]);
        await delay(200); // wait for handshake exchange
    }

    // 1. Set initial counter value
    await svc.Set('tortureKey', 0);

    // 2. Perform increments with periodic disconnections
    const totalIncrements = 20;
    const disconnectInterval = 3; // disconnect every N successful increments
    let successCount = 0;
    let disconnectCount = 0;

    while (successCount < totalIncrements) {
        try {
            await svc.Increment('tortureKey');
            successCount++;

            // Disconnect periodically (not after the last batch)
            if (successCount % disconnectInterval === 0 && successCount < totalIncrements) {
                disconnectCount++;
                await disconnectAndReconnect();
            }
        } catch {
            // Call failed due to disconnection — wait for reconnection and retry
            await waitForConnection();
        }
    }

    // 3. Ensure connection is stable
    await waitForConnection();

    // 4. Get raw counter value (non-compute) — this is the ground truth
    const rawValue = await svc.GetCounterNonCompute('tortureKey');

    // Raw value may exceed totalIncrements if some in-flight increments
    // were processed by the server but their responses were lost during disconnect
    assert(rawValue >= totalIncrements,
        `Raw counter ${rawValue} should be >= ${totalIncrements}`);

    // 5. Verify eventual consistency: computed value must match raw value
    const computed = await Computed.capture(() => svc.GetCounter('tortureKey'));
    assert(computed.value === rawValue,
        `Eventual consistency violated: computed=${computed.value}, raw=${rawValue}`);

    console.log(
        `  reconnection-torture: PASSED ` +
    `(${disconnectCount} disconnects, ${successCount} successful increments, final value=${rawValue})`,
    );
}

// ---------------------------------------------------------------------------
// Test: server-restart — verifies ComputedState resets after real .NET server restart
// ---------------------------------------------------------------------------

async function testServerRestart(hub: FusionHub, peer: RpcClientPeer): Promise<void> {
    const svc = hub.addClient<ITypeScriptTestComputeService>(peer, TestComputeServiceDef);

    // 1. Set counter to a known value
    await svc.Set('rstKey', 42);

    // 2. Create ComputedState backed by the compute method
    const state = new ComputedState(() => svc.GetCounter('rstKey'), {
        updateDelayer: FixedDelayer.get(300), // 300ms — ensures recomputation happens after handshake
    });
    await Promise.race([
        state.whenFirstTimeUpdated(),
        timeout(5_000, 'ComputedState first update timeout'),
    ]);
    assert(state.value === 42, `Expected 42 initially, got ${state.value}`);
    console.log('  server-restart: initial value confirmed (42)');

    // 3. Signal the .NET server to restart (fire-and-forget)
    //    The C# test method receives this signal, stops the host, and restarts it.
    peer.callNoWait('IServerControlService.RequestRestart:0', []);
    console.log('  server-restart: restart requested');

    // 4. Wait for reconnection to the restarted server
    await Promise.race([
        whenNextConnected(peer),
        timeout(15_000, 'Reconnection timeout after server restart'),
    ]);
    await delay(200); // wait for handshake exchange to complete
    console.log('  server-restart: reconnected');

    // 5. Wait for ComputedState to settle to 0 (server restarted = in-memory state is gone)
    const deadline = Date.now() + 15_000;
    while (state.valueOrUndefined !== 0) {
        await Promise.race([
            state.whenUpdated(),
            delay(500),
        ]);
        if (Date.now() > deadline)
            throw new Error(`ComputedState did not reset to 0 after server restart (current value: ${state.valueOrUndefined})`);
    }
    assert(state.value === 0, `Expected 0 after server restart, got ${state.value}`);
    console.log('  server-restart: value reset to 0 confirmed');

    // 6. Verify mutations work on the new server
    const update2 = state.whenUpdated();
    await svc.Set('rstKey', 99);
    await Promise.race([
        update2,
        timeout(5_000, 'ComputedState did not update after Set on new server'),
    ]);
    // Allow a couple more updates if the first one was transitional
    for (let i = 0; i < 5 && state.value !== 99; i++) {
        await Promise.race([state.whenUpdated(), delay(500)]);
    }
    assert(state.value === 99, `Expected 99 after Set on new server, got ${state.value}`);
    console.log('  server-restart: post-restart mutation confirmed (99)');

    state.dispose();
    console.log('  server-restart: PASSED');
}

// ---------------------------------------------------------------------------
// Entry point
// ---------------------------------------------------------------------------

run().then(
    () => process.exit(0),
    (err: unknown) => {
        console.error('FAILED:', err);
        process.exit(1);
    },
);
