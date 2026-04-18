// Regression tests for reconnection edge cases in RpcClientPeer.run().
//
// These target three latent bugs that could prevent the client from ever
// reconnecting after the server is stopped and restarted:
//
//   1. Lost-close race at the end of each run() iteration:
//      `await new Promise<void>(r => conn.closed.add(() => r()))` registered
//      AFTER `conn.closed` was already triggered. EventHandlerSet does not
//      replay past events, so the handler waits forever.
//
//   2. Missing handshake timeout: if the server accepts the WebSocket but
//      never replies to `$sys.Handshake`, the client used to block on
//      `_pendingHandshake.promise` indefinitely.
//
//   3. `_reconcileReconnect` deadlock: when the connection dies mid-reconcile,
//      the inner `$sys.Reconnect` call's result.promise never resolved
//      (outbound calls aren't rejected on disconnect alone), deadlocking the
//      outer `_reconnect()` and blocking the run loop.

import { describe, it, expect, afterEach } from 'vitest';
import { RetryDelaySeq } from '@actuallab/core';
import {
    RpcHub,
    RpcClientPeer,
    RpcMessageChannelConnection,
    defineRpcService,
    createRpcClient,
    RpcRemoteExecutionMode,
    RpcSystemCalls,
    RpcSystemCallHandler,
    type WebSocketLike,
    type RpcMessage,
    type RpcPeer,
} from '../src/index.js';

function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

interface ICalcService {
    add(a: number, b: number): Promise<number>;
}

const CalcServiceDef = defineRpcService('CalcService', {
    add: { args: [0, 0] },
});

/** FakeWebSocket backed by a MessagePort, with an optional
 *  `afterOpen` hook fired right after the ws transitions to OPEN. */
class FakeWebSocket implements WebSocketLike {
    readyState = 0;
    onopen: ((ev: unknown) => void) | null = null;
    onmessage: ((ev: { data: unknown }) => void) | null = null;
    onclose: ((ev: { code: number; reason: string }) => void) | null = null;
    onerror: ((ev: unknown) => void) | null = null;

    private _port: MessagePort;

    constructor(port: MessagePort) {
        this._port = port;
        port.onmessage = (ev: MessageEvent) => {
            if (this.readyState === 1) this.onmessage?.({ data: ev.data });
        };
        setTimeout(() => {
            if (this.readyState !== 0) return;
            this.readyState = 1;
            this.onopen?.(undefined);
        }, 0);
    }

    send(data: string): void {
        if (this.readyState === 1) this._port.postMessage(data);
    }

    close(code?: number, reason?: string): void {
        if (this.readyState >= 2) return;
        this.readyState = 3;
        try { this._port.close(); } catch { /* ignore */ }
        this.onclose?.({ code: code ?? 1000, reason: reason ?? '' });
    }
}

describe('RPC reconnect edge cases', () => {
    const hubs: RpcHub[] = [];
    const peers: RpcClientPeer[] = [];

    afterEach(() => {
        for (const p of peers) p.close();
        for (const h of hubs) h.close();
        hubs.length = 0;
        peers.length = 0;
    });

    function createServerHub(name: string, addResult = 0): RpcHub {
        const hub = new RpcHub(name);
        hub.addService(CalcServiceDef, {
            add: (a: unknown, b: unknown) =>
                (a as number) + (b as number) + addResult,
        });
        hubs.push(hub);
        return hub;
    }

    // -----------------------------------------------------------------
    // Issue #2: Missing handshake timeout.
    //
    // If the server accepts the WS but never sends `$sys.Handshake`, the
    // client used to block forever on `_pendingHandshake.promise`.
    // Expected: after `handshakeTimeoutMs` ms, the client force-closes the
    // socket, applies the retry delay, and tries again. Once a real server
    // is swapped in, it reconnects successfully.
    // -----------------------------------------------------------------
    it('aborts and retries when the server never replies to handshake', async () => {
        // `useBlackHole` = true means the factory creates a socket that
        // accepts the WS upgrade but has NO server peer on the other side,
        // so the client's `$sys.Handshake` is never answered.
        const state = { useBlackHole: true, hub: createServerHub('real-server', 100) };

        const clientHub = new RpcHub('client');
        hubs.push(clientHub);
        const peer = new RpcClientPeer(clientHub, 'ws://test', false);
        clientHub.addPeer(peer);
        peers.push(peer);

        // Short timings so the test completes quickly.
        peer.handshakeTimeoutMs = 150;
        peer.hub.reconnectDelayer.delays = RetryDelaySeq.fixed(20);

        let blackHoleAttempts = 0;
        const factory = (_url: string): WebSocketLike => {
            const channel = new MessageChannel();
            if (state.useBlackHole) {
                // No server-side peer — just hold the port open so the
                // handshake message lands in a void.
                blackHoleAttempts++;
                // Keep a reference so GC doesn't close the port prematurely.
                channel.port2.onmessage = () => { /* intentionally ignored */ };
            } else {
                const ref = `server://${crypto.randomUUID()}`;
                const serverPeer = state.hub.getServerPeer(ref);
                serverPeer.accept(new RpcMessageChannelConnection(channel.port2));
            }
            return new FakeWebSocket(channel.port1);
        };

        peer.webSocketFactory = factory;
        peer.start();

        // Wait until at least one handshake timeout has elapsed.
        await delay(250);
        expect(blackHoleAttempts).toBeGreaterThanOrEqual(1);

        // Swap in the real server — next connect attempt should succeed.
        state.useBlackHole = false;
        await peer.whenConnected();
        await delay(20);

        const calc = createRpcClient<ICalcService>(peer, CalcServiceDef);
        expect(await calc.add(1, 2)).toBe(103);
    }, 10_000);

    // -----------------------------------------------------------------
    // Issue #1: Lost-close race at end of run() iteration.
    //
    // If `conn.closed` fires BEFORE the run loop registers its final
    // `conn.closed.add(() => r())` handler, the handler is added to an
    // already-triggered EventHandlerSet and never fires. The iteration
    // blocks forever, preventing reconnection.
    //
    // Reproduction: a server that closes its side of the WS immediately
    // after replying to `$sys.Handshake`. Under the old code, the close
    // often arrived in the microtask gap between `_reconnect()` returning
    // and the final `await new Promise<void>(...)` being reached.
    // -----------------------------------------------------------------
    it('still reconnects when the server closes the socket right after handshake', async () => {
        // Two-phase state: first attempt uses a self-closing server,
        // subsequent attempts go to a normal server.
        const state = {
            mode: 'close-after-handshake' as 'close-after-handshake' | 'normal',
            hub: createServerHub('server-1'),
        };

        const clientHub = new RpcHub('client');
        hubs.push(clientHub);
        const peer = new RpcClientPeer(clientHub, 'ws://test', false);
        clientHub.addPeer(peer);
        peers.push(peer);

        peer.hub.reconnectDelayer.delays = RetryDelaySeq.fixed(30);

        const factory = (_url: string): WebSocketLike => {
            const channel = new MessageChannel();
            const ref = `server://${crypto.randomUUID()}`;
            const serverPeer = state.hub.getServerPeer(ref);
            serverPeer.accept(new RpcMessageChannelConnection(channel.port2));

            const fakeWs = new FakeWebSocket(channel.port1);
            if (state.mode === 'close-after-handshake') {
                // Hook the port: as soon as the server replies with its
                // $sys.Handshake, schedule a close on the client side. This
                // lands the close event in the narrow window between
                // `_reconnect()` resolving and the final close-wait
                // handler being registered.
                const origHandler = channel.port1.onmessage;
                channel.port1.onmessage = (ev: MessageEvent) => {
                    origHandler?.call(channel.port1, ev);
                    // Close on the next microtask — we want the onclose
                    // callback to fire after the client processes the
                    // handshake but (often) before the final close-wait.
                    queueMicrotask(() => {
                        if (fakeWs.readyState < 2) fakeWs.close(1001, 'Bye');
                    });
                };
            }
            return fakeWs;
        };

        peer.webSocketFactory = factory;
        peer.start();

        // Wait for the first (bad) connection attempt, then swap in the
        // normal server so reconnect has somewhere to land.
        await peer.whenConnected();
        state.mode = 'normal';
        state.hub = createServerHub('server-2', 50);

        // Before the fix this would hang forever. Give a generous timeout
        // so CI jitter doesn't cause flakes.
        await peer.whenConnected();
        await delay(20);

        const calc = createRpcClient<ICalcService>(peer, CalcServiceDef);
        expect(await calc.add(1, 2)).toBe(53);
    }, 10_000);

    // -----------------------------------------------------------------
    // Issue #3: `_reconcileReconnect` deadlock.
    //
    // On same-peer reconnect with pending outbound calls (mode has
    // AllowReconnect bit), the client sends `$sys.Reconnect` and awaits
    // the server's reply. If the WS dies mid-await, outbound calls are
    // NOT rejected on disconnect (only on peer.close()), so the pending
    // result.promise deadlocks `_reconcileReconnect`, which deadlocks
    // `_reconnect()`, which blocks the outer run loop.
    //
    // The fix passes an AbortSignal to the inner `$sys.Reconnect` call
    // that aborts on close, causing the call's promise to reject and
    // reconcileReconnect to fall through to resend-all.
    // -----------------------------------------------------------------
    it('does not deadlock when $sys.Reconnect hangs and the new socket dies', async () => {
        // Keep the "same server" hubId across restarts so isPeerChanged=false
        // and `_reconcileReconnect` is actually called.
        const stableServerHubId = 'stable-server-hub';
        const state = {
            // First attempt: a normal server, we make a long-running call.
            // Second attempt: a server that NEVER responds to $sys.Reconnect.
            // Third attempt: a normal server that we want the client to reach.
            attempt: 0,
            serverHub: new RpcHub(stableServerHubId),
        };
        hubs.push(state.serverHub);
        state.serverHub.addService(CalcServiceDef, {
            add: async (a: unknown, b: unknown) => {
                // Never-resolving long call — simulates an in-flight call
                // at the moment of disconnect.
                await new Promise(() => { /* forever */ });
                return (a as number) + (b as number);
            },
        });

        const clientHub = new RpcHub('client');
        hubs.push(clientHub);
        const peer = new RpcClientPeer(clientHub, 'ws://test', false);
        clientHub.addPeer(peer);
        peers.push(peer);

        peer.hub.reconnectDelayer.delays = RetryDelaySeq.fixed(20);

        let currentServerWs: FakeWebSocket | undefined;
        const factory = (_url: string): WebSocketLike => {
            const attempt = state.attempt++;
            const channel = new MessageChannel();

            if (attempt === 1) {
                // Second attempt: swallow $sys.Reconnect. We build a stub
                // server peer whose system-call handler silently drops the
                // reconnect request so the client's `_reconcileReconnect`
                // sits awaiting a reply that never arrives.
                const stubHub = new RpcHub(stableServerHubId);
                hubs.push(stubHub);
                // Register the calc service so method lookup doesn't blow up.
                stubHub.addService(CalcServiceDef, {
                    add: async () => new Promise<number>(() => { /* forever */ }),
                });
                class DroppingHandler extends RpcSystemCallHandler {
                    handle(message: RpcMessage, args: unknown[], peer: RpcPeer): void {
                        if (message.Method === RpcSystemCalls.reconnect) return;
                        super.handle(message, args, peer);
                    }
                }
                stubHub.systemCallHandler = new DroppingHandler();
                const serverPeer = stubHub.getServerPeer(`server://${crypto.randomUUID()}`);
                serverPeer.accept(new RpcMessageChannelConnection(channel.port2));
                // Schedule a close after the handshake + any reconnect
                // call has had a chance to be sent. This kills the WS
                // while `_reconcileReconnect` is mid-await.
                setTimeout(() => {
                    currentServerWs?.close(1001, 'mid-reconcile death');
                }, 50);
            } else {
                // First and third attempts: normal server.
                if (attempt > 1) {
                    // Replace state.serverHub on the third attempt so
                    // the new socket connects to a fresh, responsive hub
                    // (but keep the same hubId for same-peer behavior).
                    state.serverHub = new RpcHub(stableServerHubId);
                    hubs.push(state.serverHub);
                    state.serverHub.addService(CalcServiceDef, {
                        add: (a: unknown, b: unknown) =>
                            (a as number) + (b as number) + 1000,
                    });
                }
                const serverPeer = state.serverHub.getServerPeer(
                    `server://${crypto.randomUUID()}`);
                serverPeer.accept(new RpcMessageChannelConnection(channel.port2));
            }

            const fakeWs = new FakeWebSocket(channel.port1);
            currentServerWs = fakeWs;
            return fakeWs;
        };

        peer.webSocketFactory = factory;
        peer.start();

        // Phase 1: connect to first server, issue a long-running call
        // with AllowReconnect so it becomes an eligible reconnect call.
        await peer.whenConnected();
        await delay(20);

        const longCall = peer.call('CalcService.add:3', [1, 2], {
            remoteExecutionMode: RpcRemoteExecutionMode.Default, // includes AllowReconnect bit
        });
        longCall.result.promise.catch(() => { /* swallow */ });

        // Kill the first socket, forcing a reconnect into the
        // "never-replies-to-$sys.Reconnect" server for attempt 1.
        await delay(20);
        currentServerWs?.close(1001, 'restart');

        // Phase 3: before the fix, the client deadlocks inside
        // `_reconcileReconnect` on attempt 1 and never reaches attempt 2.
        // With the fix, the mid-reconcile close aborts the inner call and
        // the run loop proceeds to the normal server.
        await peer.whenConnected();
        await delay(50);

        // We should now be connected to the third server. Verify with a
        // fresh, short-lived call.
        const shortCall = peer.call('CalcService.add:3', [2, 3]);
        const result = await shortCall.result.promise;
        // shortCall goes to attempt 2 server which adds 1000.
        expect(result).toBe(1005);
    }, 15_000);
});
