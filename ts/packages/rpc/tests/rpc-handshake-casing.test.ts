import { describe, it, expect, afterEach } from 'vitest';
import { RetryDelaySeq } from '@actuallab/core';
import {
    RpcHub,
    RpcClientPeer,
    RpcSystemCalls,
    IncreasingSeqCompressor,
    createRpcClient,
    defineRpcService,
    serializeMessage,
    deserializeMessage,
    splitFrame,
    type RpcError,
    type WebSocketLike,
} from '../src/index.js';

/**
 * E2E coverage for the text (`json5np`) transport when the remote peer
 * serializes `$sys.*` payloads with a JSON serializer tuned to camelCase
 * (ASP.NET's `JsonSerializerDefaults.Web`) vs PascalCase. .NET is the only
 * peer that emits the object form over JSON, so these tests stand in for a
 * .NET server by hand-rolling a mock that replies in either casing while the
 * real client `run()` loop drives the handshake / reconnect / call paths.
 *
 * Object-shaped `$sys.*` arguments the TS client reads by name — and must
 * therefore tolerate both casings:
 *   - `$sys.Handshake` → RpcHandshake (RemotePeerId / RemoteHubId / Index)
 *   - `$sys.Error`     → ExceptionInfo (Message / TypeRef)
 * Every other `$sys.*` argument is positional/scalar or a stage-keyed dict,
 * so casing can't affect it.
 */

type Casing = 'PascalCase' | 'camelCase';

interface ServerState {
    hubId: string;
    handshakeIndex: number;
    lastReconnectIndex: number | null;
}

function makeHandshakeArg(casing: Casing, peerId: string, hubId: string, index: number): Record<string, unknown> {
    return casing === 'PascalCase'
        ? { RemotePeerId: peerId, RemoteApiVersionSet: null, RemoteHubId: hubId, ProtocolVersion: 2, Index: index }
        : { remotePeerId: peerId, remoteApiVersionSet: null, remoteHubId: hubId, protocolVersion: 2, index };
}

function makeErrorArg(casing: Casing, message: string, typeRef: string): Record<string, unknown> {
    return casing === 'PascalCase'
        ? { Message: message, TypeRef: typeRef }
        : { message, typeRef };
}

const EXC_TYPE_REF = 'System.InvalidOperationException, System.Private.CoreLib';

/** Wires a mock .NET-style server onto a MessagePort, replying in `casing`. */
function attachMockServer(port: MessagePort, casing: Casing, state: ServerState): void {
    const serverPeerId = crypto.randomUUID();
    const send = (envelope: { Method: string; RelatedId?: number }, args: unknown[]): void =>
        port.postMessage(serializeMessage(envelope, args));

    port.onmessage = (ev: MessageEvent): void => {
        const data = typeof ev.data === 'string' ? ev.data : String(ev.data);
        for (const raw of splitFrame(data)) {
            if (raw.length === 0) continue;
            const { message, args } = deserializeMessage(raw);
            const method = message.Method ?? '';
            const relatedId = message.RelatedId ?? 0;

            if (method === RpcSystemCalls.handshake) {
                const index = ++state.handshakeIndex;
                send({ Method: RpcSystemCalls.handshake },
                    [makeHandshakeArg(casing, serverPeerId, state.hubId, index)]);
            } else if (method === RpcSystemCalls.reconnect) {
                state.lastReconnectIndex = args[0] as number;
                send({ Method: RpcSystemCalls.ok, RelatedId: relatedId },
                    [IncreasingSeqCompressor.serialize([])]);
            } else if (method.startsWith('CalcService.add')) {
                send({ Method: RpcSystemCalls.ok, RelatedId: relatedId },
                    [(args[0] as number) + (args[1] as number)]);
            } else if (method.startsWith('FailService.fail')) {
                send({ Method: RpcSystemCalls.error, RelatedId: relatedId },
                    [makeErrorArg(casing, 'boom', EXC_TYPE_REF)]);
            }
            // HoldService.wait / $sys.KeepAlive / $sys.Cancel: intentionally ignored.
        }
    };
}

/** MessagePort-backed WebSocket so the client `run()` loop talks to the mock server. */
class FakeWebSocket implements WebSocketLike {
    readyState = 0;
    onopen: ((ev: unknown) => void) | null = null;
    onmessage: ((ev: { data: unknown }) => void) | null = null;
    onclose: ((ev: { code: number; reason: string }) => void) | null = null;
    onerror: ((ev: unknown) => void) | null = null;

    constructor(private _port: MessagePort) {
        _port.onmessage = (ev: MessageEvent) => {
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
        this._port.close();
        this.onclose?.({ code: code ?? 1000, reason: reason ?? '' });
    }
}

interface ICalc { add(a: number, b: number): Promise<number>; }
const CalcServiceDef = defineRpcService('CalcService', { add: { args: [0, 0] } });

interface IHold { wait(): Promise<never>; }
const HoldServiceDef = defineRpcService('HoldService', { wait: { args: [] } });

interface IFail { fail(): Promise<never>; }
const FailServiceDef = defineRpcService('FailService', { fail: { args: [] } });

function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

async function waitFor(cond: () => boolean, timeoutMs = 2000): Promise<void> {
    const start = Date.now();
    while (!cond()) {
        if (Date.now() - start > timeoutMs) throw new Error('waitFor timed out');
        await delay(5);
    }
}

describe.each<Casing>(['PascalCase', 'camelCase'])('handshake JSON casing [%s]', (casing) => {
    const hubs: RpcHub[] = [];
    const peers: RpcClientPeer[] = [];

    afterEach(() => {
        for (const p of peers) p.close();
        for (const h of hubs) h.close();
        hubs.length = 0;
        peers.length = 0;
    });

    function setupClient(state: ServerState): { peer: RpcClientPeer; closeWs: () => void } {
        const clientHub = new RpcHub('client');
        hubs.push(clientHub);
        const peer = new RpcClientPeer(clientHub, 'ws://test', false);
        clientHub.addPeer(peer);
        peers.push(peer);
        peer.hub.reconnectDelayer.delays = RetryDelaySeq.fixed(20);

        let currentWs: FakeWebSocket | undefined;
        peer.webSocketFactory = () => {
            const channel = new MessageChannel();
            attachMockServer(channel.port2, casing, state);
            currentWs = new FakeWebSocket(channel.port1);
            return currentWs;
        };
        peer.start();
        return {
            peer,
            closeWs: () => { currentWs?.close(1001, 'drop'); currentWs = undefined; },
        };
    }

    function newState(hubId = 'server-hub-1'): ServerState {
        return { hubId, handshakeIndex: 0, lastReconnectIndex: null };
    }

    it('connects and round-trips a regular call', async () => {
        const state = newState();
        const { peer } = setupClient(state);
        await peer.whenConnected();

        const calc = createRpcClient<ICalc>(peer, CalcServiceDef);
        expect(await calc.add(3, 4)).toBe(7);
    });

    it('parses the server handshake Index and echoes it in $sys.Reconnect', async () => {
        const state = newState();
        const { peer, closeWs } = setupClient(state);
        await peer.whenConnected();
        await waitFor(() => state.handshakeIndex === 1);

        // In-flight, reconnectable call the server never answers — keeps it
        // eligible so the same-peer reconnect triggers $sys.Reconnect.
        const hold = createRpcClient<IHold>(peer, HoldServiceDef);
        hold.wait().catch(() => { /* never resolves */ });
        await delay(10);

        closeWs();
        await waitFor(() => state.lastReconnectIndex !== null);

        // Server's reconnect handshake was Index 2; the client must echo 2.
        // With the camelCase parse bug it would fall back to 0.
        expect(state.handshakeIndex).toBe(2);
        expect(state.lastReconnectIndex).toBe(2);
    });

    it('detects peer change when RemoteHubId changes', async () => {
        const state = newState();
        const { peer, closeWs } = setupClient(state);
        await peer.whenConnected();
        await waitFor(() => state.handshakeIndex === 1);

        let peerChanged = false;
        peer.peerChanged.add(() => { peerChanged = true; });

        // Server "restarts" with a new identity.
        state.hubId = 'server-hub-2';
        closeWs();
        await waitFor(() => state.handshakeIndex === 2);
        await delay(10);

        // Keyed off RemoteHubId — undefined under the bug, so it never fires.
        expect(peerChanged).toBe(true);
    });

    it('propagates a server $sys.Error with message and type name', async () => {
        const state = newState();
        const { peer } = setupClient(state);
        await peer.whenConnected();

        const fail = createRpcClient<IFail>(peer, FailServiceDef);
        const err = await fail.fail().then(() => null, (e: unknown) => e);
        expect(err).toBeInstanceOf(Error);
        expect((err as Error).message).toBe('boom');
        expect((err as RpcError).typeName).toBe('System.InvalidOperationException');
    });
});
