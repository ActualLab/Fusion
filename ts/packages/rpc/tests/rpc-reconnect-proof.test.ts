import { describe, it, expect, afterEach, vi } from 'vitest';
import { RetryDelaySeq } from '@actuallab/core';
import {
    RpcHub,
    RpcClientPeer,
    RpcConnectionState,
    RpcSystemCalls,
    computeReconnectProof,
    isReconnectProofSupported,
    serializeMessage,
    deserializeMessage,
    splitFrame,
    type WebSocketLike,
} from '../src/index.js';
import { sanitizeUrl } from '../src/rpc-peer.js';

/**
 * Client half of the reconnect proof-of-possession protocol: the server issues a
 * per-peer secret inside its `$sys.Handshake`, and every later connect attempt
 * carries `c` (counter) + `p` (HMAC-SHA256 proof) in the connect URL.
 *
 * The mock server here stands in for .NET — it is the only peer that issues
 * secrets, and it may spell the handshake as a positional array or as an object
 * in either casing.
 */

// PINNED CROSS-RUNTIME VECTOR — must match .NET's RpcReconnectProof.Compute
// byte for byte. key = UTF8(secret) (the secret is an opaque token, NOT
// base64url-decoded); message = UTF8(clientId + "\n" + counterText) with a
// single 0x0A separator; proof = unpadded base64url of the 32-byte HMAC.
const VECTOR = {
    secret: 'xlHGbajpOkxzI-yS7ZqjKRzncF4sC25YezNcdQD9yOI',
    clientId: 'x7FTKcK88zakKdYBij3p-w',
    cases: [
        { counter: '1', proof: '-F2GMXBbj8WkL_gwF9KkzNwbIodkryKUXf_hE1__HjU' },
        { counter: '2', proof: 'kK7heeSM_I3uNW-kR0cpeBGG_kx-G-q3w0PMwWevJNM' },
        { counter: '1234567890', proof: 'LPhpcJJw1fVbqvuYdKOExV2ONdvo--7d63SAXUnS7ac' },
    ],
};

type HandshakeShape = 'array' | 'PascalCase' | 'camelCase';

interface ServerState {
    hubId: string;
    peerId: string;
    handshakeIndex: number;
    /** Secret the next handshake carries; `undefined` = legacy server. */
    secret: string | undefined;
}

function makeHandshakeArg(shape: HandshakeShape, state: ServerState, index: number): unknown {
    const { peerId, hubId, secret } = state;
    if (shape === 'array') {
        return secret === undefined
            ? [peerId, null, hubId, 2, index]
            : [peerId, null, hubId, 2, index, secret];
    }
    if (shape === 'PascalCase') {
        const arg: Record<string, unknown> = {
            RemotePeerId: peerId, RemoteApiVersionSet: null, RemoteHubId: hubId,
            ProtocolVersion: 2, Index: index,
        };
        if (secret !== undefined) arg.Secret = secret;
        return arg;
    }
    const arg: Record<string, unknown> = {
        remotePeerId: peerId, remoteApiVersionSet: null, remoteHubId: hubId,
        protocolVersion: 2, index,
    };
    if (secret !== undefined) arg.secret = secret;
    return arg;
}

function attachMockServer(port: MessagePort, shape: HandshakeShape, state: ServerState): void {
    port.onmessage = (ev: MessageEvent): void => {
        const data = typeof ev.data === 'string' ? ev.data : String(ev.data);
        for (const raw of splitFrame(data)) {
            if (raw.length === 0)
                continue;

            const { message } = deserializeMessage(raw);
            if ((message.Method ?? '') !== RpcSystemCalls.handshake)
                continue;

            const index = ++state.handshakeIndex;
            port.postMessage(serializeMessage(
                { Method: RpcSystemCalls.handshake }, [makeHandshakeArg(shape, state, index)]));
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
            if (this.readyState === 1)
                this.onmessage?.({ data: ev.data });
        };
        setTimeout(() => {
            if (this.readyState !== 0)
                return;

            this.readyState = 1;
            this.onopen?.(undefined);
        }, 0);
    }

    send(data: string): void {
        if (this.readyState === 1)
            this._port.postMessage(data);
    }

    close(code?: number, reason?: string): void {
        if (this.readyState >= 2)
            return;

        this.readyState = 3;
        this._port.close();
        this.onclose?.({ code: code ?? 1000, reason: reason ?? '' });
    }
}

/** A socket that never opens — stands in for a connect attempt that fails
 *  after the URL was already built (and the counter already consumed). */
class FailingWebSocket implements WebSocketLike {
    readyState = 0;
    onopen: ((ev: unknown) => void) | null = null;
    onmessage: ((ev: { data: unknown }) => void) | null = null;
    onclose: ((ev: { code: number; reason: string }) => void) | null = null;
    onerror: ((ev: unknown) => void) | null = null;

    constructor() {
        setTimeout(() => {
            this.readyState = 3;
            this.onclose?.({ code: 1006, reason: '' });
        }, 0);
    }

    send(): void { /* never open */ }

    close(): void {
        this.readyState = 3;
    }
}

function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

async function waitFor(cond: () => boolean, timeoutMs = 2000): Promise<void> {
    const start = Date.now();
    while (!cond()) {
        if (Date.now() - start > timeoutMs)
            throw new Error('waitFor timed out');

        await delay(5);
    }
}

function parseQuery(url: string): URLSearchParams {
    return new URL(url).searchParams;
}

describe('reconnect proof', () => {
    const hubs: RpcHub[] = [];
    const peers: RpcClientPeer[] = [];

    afterEach(() => {
        for (const p of peers) p.close();
        for (const h of hubs) h.close();
        hubs.length = 0;
        peers.length = 0;
        vi.unstubAllGlobals();
        vi.restoreAllMocks();
    });

    interface Harness {
        peer: RpcClientPeer;
        state: ServerState;
        connectUrls: string[];
        /** Completed handshakes — the secret is stored just before this ticks. */
        connectedCount: () => number;
        closeWs: () => void;
        failNextConnect: () => void;
    }

    function setupClient(shape: HandshakeShape = 'PascalCase', secret?: string): Harness {
        const state: ServerState = {
            hubId: 'server-hub-1',
            peerId: crypto.randomUUID(),
            handshakeIndex: 0,
            secret,
        };
        const hub = new RpcHub('client');
        hubs.push(hub);
        const peer = new RpcClientPeer(hub, 'ws://test/rpc/ws', false);
        hub.addPeer(peer);
        peers.push(peer);
        hub.reconnectDelayer.delays = RetryDelaySeq.fixed(10);

        const connectUrls: string[] = [];
        let connectedCount = 0;
        peer.connectionStateChanged.add(s => {
            if (s === RpcConnectionState.Connected) connectedCount++;
        });
        let currentWs: FakeWebSocket | undefined;
        let isNextConnectFailing = false;
        peer.webSocketFactory = (url: string) => {
            connectUrls.push(url);
            if (isNextConnectFailing) {
                isNextConnectFailing = false;
                return new FailingWebSocket();
            }
            const channel = new MessageChannel();
            attachMockServer(channel.port2, shape, state);
            currentWs = new FakeWebSocket(channel.port1);
            return currentWs;
        };
        peer.start();
        return {
            peer,
            state,
            connectUrls,
            connectedCount: () => connectedCount,
            closeWs: () => { currentWs?.close(1001, 'drop'); currentWs = undefined; },
            failNextConnect: () => { isNextConnectFailing = true; },
        };
    }

    /** Drives the loop until `connectUrls` holds `count` entries. */
    async function waitForConnects(h: Harness, count: number): Promise<void> {
        await waitFor(() => h.connectUrls.length >= count);
    }

    /** Drives the loop until `count` handshakes have completed — i.e. the
     *  secret from the `count`-th one is stored. */
    async function waitForHandshakes(h: Harness, count: number): Promise<void> {
        await waitFor(() => h.connectedCount() >= count);
    }

    describe('crypto', () => {
        it.each(VECTOR.cases)('matches the pinned cross-runtime vector (c=$counter)', async ({ counter, proof }) => {
            expect(await computeReconnectProof(VECTOR.secret, VECTOR.clientId, counter)).toBe(proof);
        });

        it('produces an unpadded 43-char base64url proof', async () => {
            const proof = await computeReconnectProof(VECTOR.secret, VECTOR.clientId, '1');
            expect(proof).toMatch(/^[A-Za-z0-9_-]{43}$/);
        });

        it('separates clientId and counter with LF, not CRLF', async () => {
            // 'a\n1' vs the CRLF reading of the same pair — a divergent separator
            // would make these equal for one of the two spellings.
            const lf = await computeReconnectProof(VECTOR.secret, 'a', '1');
            const crlf = await computeReconnectProof(VECTOR.secret, 'a\r', '1');
            expect(lf).not.toBe(crlf);
        });

        it('is supported in this runtime', () => {
            expect(isReconnectProofSupported()).toBe(true);
        });
    });

    describe('connect URL', () => {
        it('sends no c/p on the first connect, then both on every later attempt', async () => {
            const h = setupClient('PascalCase', 'secret-one');
            await h.peer.whenConnected();
            await waitForConnects(h, 1);

            const first = parseQuery(h.connectUrls[0]);
            expect(first.get('clientId')).toBe(h.peer.clientId);
            expect(first.get('c')).toBeNull();
            expect(first.get('p')).toBeNull();

            h.closeWs();
            await waitForHandshakes(h, 2);
            const second = parseQuery(h.connectUrls[1]);
            expect(second.get('c')).toBe('1');
            expect(second.get('p'))
                .toBe(await computeReconnectProof('secret-one', h.peer.clientId, '1'));

            h.closeWs();
            await waitForHandshakes(h, 3);
            expect(parseQuery(h.connectUrls[2]).get('c')).toBe('2');
        });

        it('increments the counter per connect attempt, including a failed one', async () => {
            const h = setupClient('PascalCase', 'secret-one');
            await h.peer.whenConnected();
            h.closeWs();
            await waitForHandshakes(h, 2);
            expect(parseQuery(h.connectUrls[1]).get('c')).toBe('1');

            h.failNextConnect();
            h.closeWs();
            await waitForConnects(h, 4);
            expect(parseQuery(h.connectUrls[2]).get('c')).toBe('2');
            expect(parseQuery(h.connectUrls[3]).get('c')).toBe('3');
        });

        it('keeps the stored secret when a legacy server sends none', async () => {
            const h = setupClient('PascalCase', 'secret-one');
            await h.peer.whenConnected();
            h.state.secret = undefined;

            h.closeWs();
            await waitForHandshakes(h, 2);
            h.closeWs();
            await waitForHandshakes(h, 3);

            expect(parseQuery(h.connectUrls[2]).get('p'))
                .toBe(await computeReconnectProof('secret-one', h.peer.clientId, '2'));
        });

        it('adopts a new secret when the handshake carries a different one', async () => {
            const h = setupClient('PascalCase', 'secret-one');
            await h.peer.whenConnected();
            h.state.secret = 'secret-two';

            h.closeWs();
            await waitForHandshakes(h, 2);
            h.closeWs();
            await waitForHandshakes(h, 3);

            expect(parseQuery(h.connectUrls[2]).get('p'))
                .toBe(await computeReconnectProof('secret-two', h.peer.clientId, '2'));
        });

        it.each<HandshakeShape>(['array', 'PascalCase', 'camelCase'])(
            'reads the secret from a %s handshake', async shape => {
                const h = setupClient(shape, 'secret-one');
                await h.peer.whenConnected();
                h.closeWs();
                await waitForHandshakes(h, 2);

                expect(parseQuery(h.connectUrls[1]).get('p'))
                    .toBe(await computeReconnectProof('secret-one', h.peer.clientId, '1'));
            });
    });

    describe('insecure context', () => {
        it('falls back to a legacy URL without crypto.subtle, warning once', async () => {
            const warn = vi.spyOn(console, 'warn').mockImplementation(() => { /* silence */ });
            const realCrypto = globalThis.crypto;
            vi.stubGlobal('crypto', {
                randomUUID: () => realCrypto.randomUUID(),
                getRandomValues: (a: Uint8Array) => realCrypto.getRandomValues(a),
            });

            const h = setupClient('PascalCase', 'secret-one');
            await h.peer.whenConnected();
            h.closeWs();
            await waitForHandshakes(h, 2);
            h.closeWs();
            await waitForHandshakes(h, 3);

            for (const url of h.connectUrls) {
                const q = parseQuery(url);
                expect(q.get('c')).toBeNull();
                expect(q.get('p')).toBeNull();
                expect(q.get('clientId')).toBe(h.peer.clientId);
                expect(q.get('f')).toBe(h.peer.serializationFormat.key);
            }
            // Reconnects still succeed — the fallback must not break the loop.
            expect(h.state.handshakeIndex).toBeGreaterThanOrEqual(3);

            const proofWarnings = warn.mock.calls
                .filter(c => c.some(a => typeof a === 'string' && a.includes('reconnect proof')));
            expect(proofWarnings).toHaveLength(1);
        });
    });

    describe('storage', () => {
        it('never writes the secret to localStorage, sessionStorage or cookies', async () => {
            const entries = new Map<string, string>();
            const storage = {
                getItem: (k: string) => entries.get(k) ?? null,
                setItem: (k: string, v: string) => { entries.set(k, v); },
                removeItem: (k: string) => { entries.delete(k); },
                clear: () => entries.clear(),
                key: () => null,
                get length() { return entries.size; },
            };
            const documentStub = { cookie: '' };
            vi.stubGlobal('localStorage', storage);
            vi.stubGlobal('sessionStorage', storage);
            vi.stubGlobal('document', documentStub);

            const h = setupClient('PascalCase', 'secret-one');
            await h.peer.whenConnected();
            h.closeWs();
            await waitForHandshakes(h, 2);

            expect([...entries.values()].join('|')).not.toContain('secret-one');
            expect(entries.size).toBe(0);
            expect(documentStub.cookie).toBe('');
        });
    });

    describe('sanitizeUrl', () => {
        it('redacts p, clientId and session, and keeps c and f', () => {
            const url = sanitizeUrl(
                'wss://h/rpc/ws?clientId=CLIENT_ID_VALUE&f=json5np&c=7&p=PROOF_VALUE&session=SESSION_VALUE');
            const q = parseQuery(url);
            expect(q.get('clientId')).toBe('<redacted>');
            expect(q.get('p')).toBe('<redacted>');
            expect(q.get('session')).toBe('<redacted>');
            expect(q.get('c')).toBe('7');
            expect(q.get('f')).toBe('json5np');
            expect(url).not.toContain('CLIENT_ID_VALUE');
            expect(url).not.toContain('PROOF_VALUE');
            expect(url).not.toContain('SESSION_VALUE');
        });

        it('redacts in the non-URL fallback path', () => {
            const url = sanitizeUrl('test-ref?clientId=CLIENT_ID_VALUE&c=7&p=PROOF_VALUE');
            expect(url).not.toContain('CLIENT_ID_VALUE');
            expect(url).not.toContain('PROOF_VALUE');
            expect(url).toContain('c=7');
        });

        it('leaves a URL without sensitive parameters untouched', () => {
            const url = 'wss://h/rpc/ws?f=json5np&c=7';
            expect(sanitizeUrl(url)).toBe(url);
        });
    });
});
