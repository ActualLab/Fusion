// Regression tests for review-r2 I9 — `resolveStreamRefs` used to walk the whole
// deserialized result of *every* regular RPC call and replace any string that
// `parseStreamRef` accepted with a live `RpcStream`. The acceptance test is a
// pure shape heuristic (4-6 comma-separated parts whose parts 1..3 `parseInt`),
// and ordinary data matches it constantly.
//
// BREAKING: results are no longer scanned. Converting a value into an
// `RpcStream` is now the caller's explicit job, via `toRpcStream`.
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import {
    RpcStream,
    RpcType,
    RpcSystemCalls,
    defineRpcService,
    parseStreamRef,
    toRpcStream,
} from '../src/index.js';
import type { RpcMessage } from '../src/index.js';
import { createTestHubPair, FORMATS, delay } from './rpc-test-helpers.js';
import type { TestHubPair } from './rpc-test-helpers.js';

// Every one of these was verified to parse as a stream reference by the old
// heuristic, so a method returning one handed the caller an RpcStream.
const FALSE_POSITIVES = [
    '1,2,3,4',
    'a,1,2,3',
    '10,20,30,40',
    'John,1,2,3,4,5',
    'x,1,2,3xyz',
    '2024,01,02,03',
];

interface IEchoService {
    echo(value: unknown): Promise<unknown>;
    getStream(): Promise<AsyncIterable<number>>;
}

const EchoServiceDef = defineRpcService('EchoService', {
    echo: { args: [undefined] },
    getStream: { args: [], returns: RpcType.stream },
});

describe.each(FORMATS)('RPC result stream-ref resolution [%s] (I9)', (formatKey) => {
    let pair: TestHubPair;
    let echo: IEchoService;

    beforeEach(async () => {
        pair = createTestHubPair(formatKey);
        pair.serverHub.addService(EchoServiceDef, {
            echo: (value: unknown) => value,
            // eslint-disable-next-line @typescript-eslint/require-await
            getStream: () => new RpcStream<number>((async function* () {
                for (let i = 0; i < 5; i++)
                    yield i * 10;
            })()),
        });
        echo = pair.clientHub.addClient<IEchoService>(pair.clientPeer, EchoServiceDef);
        await delay(10);
    });

    afterEach(() => {
        pair.serverHub.close();
        pair.clientHub.close();
    });

    it('returns stream-ref-shaped strings verbatim', async () => {
        for (const value of FALSE_POSITIVES) {
            const result = await echo.echo(value);
            expect(result).toBe(value);
            expect(result).not.toBeInstanceOf(RpcStream);
        }
    });

    it('returns a real-looking stream ref verbatim too', async () => {
        // Even an unambiguous ref is left alone: one consistent contract beats
        // a per-shape one, since the client can't tell the two apart.
        const value = `${crypto.randomUUID()},7,30,61,1,0`;
        expect(parseStreamRef(value)).not.toBeNull();

        const result = await echo.echo(value);
        expect(result).toBe(value);
        expect([...pair.clientPeer.remoteObjects.keys()]).toEqual([]);
    });

    it('leaves nested values inside objects and arrays untouched', async () => {
        const result = await echo.echo({
            row: '10,20,30,40',
            nested: { point: '1,2,3,4' },
            rows: ['a,1,2,3', 'John,1,2,3,4,5'],
        }) as { row: string; nested: { point: string }; rows: string[] };

        expect(result.row).toBe('10,20,30,40');
        expect(result.nested.point).toBe('1,2,3,4');
        expect(result.rows).toEqual(['a,1,2,3', 'John,1,2,3,4,5']);
    });

    it('still resolves the result of a stream-returning method', async () => {
        const stream = await echo.getStream();
        expect(stream).toBeInstanceOf(RpcStream);

        const items: number[] = [];
        for await (const item of stream)
            items.push(item);
        expect(items).toEqual([0, 10, 20, 30, 40]);
    });
});

describe('toRpcStream (I9)', () => {
    let pair: TestHubPair;

    beforeEach(() => {
        pair = createTestHubPair('json5np');
    });

    afterEach(() => {
        pair.serverHub.close();
        pair.clientHub.close();
    });

    it('converts a text-format reference', () => {
        const stream = toRpcStream<number>(`${crypto.randomUUID()},7,30,61,1,0`, pair.clientPeer);
        expect(stream).toBeInstanceOf(RpcStream);
        expect(stream!.id.localId).toBe(7);
        expect(stream!.ackPeriod).toBe(30);
        expect(stream!.ackAdvance).toBe(61);
    });

    it('converts a binary-format reference object', () => {
        const stream = toRpcStream<number>({
            SerializedId: ['host-1', 9],
            AckPeriod: 10,
            AckAdvance: 20,
            AllowReconnect: true,
        }, pair.clientPeer);

        expect(stream).toBeInstanceOf(RpcStream);
        expect(stream!.id).toEqual({ hostId: 'host-1', localId: 9 });
    });

    it('does not lease anything until the stream is enumerated', () => {
        toRpcStream(`${crypto.randomUUID()},7,30,61,1,0`, pair.clientPeer);
        expect([...pair.clientPeer.remoteObjects.keys()]).toEqual([]);
    });

    it('returns null for values that are not references', () => {
        for (const value of [42, null, undefined, {}, 'a,b,c', 'a,b,c,d,e,f,g'])
            expect(toRpcStream(value, pair.clientPeer)).toBeNull();
    });

    it('still accepts the shape-ambiguous strings — the caller decides', () => {
        // `toRpcStream` is not a classifier: it parses what the caller asserts
        // is a reference. The ambiguity is unfixable in the wire format, which
        // is exactly why the decision moved to the caller.
        for (const value of FALSE_POSITIVES)
            expect(toRpcStream(value, pair.clientPeer)).toBeInstanceOf(RpcStream);
    });
});

describe('$sys.I / $sys.B item payloads (I9)', () => {
    let pair: TestHubPair;

    beforeEach(() => {
        pair = createTestHubPair('json5np');
    });

    afterEach(() => {
        pair.serverHub.close();
        pair.clientHub.close();
    });

    function createRemoteStream(): RpcStream<unknown> {
        const stream = toRpcStream(`h,1,30,61,1,0`, pair.clientPeer)!;
        pair.clientPeer.remoteObjects.register(stream);
        return stream;
    }

    function handle(method: string, relatedId: number, args: unknown[]): void {
        const message: RpcMessage = { Method: method, RelatedId: relatedId };
        pair.clientHub.systemCallHandler.handle(message, args, pair.clientPeer);
    }

    it('delivers $sys.I items verbatim, including ref-shaped strings', async () => {
        const stream = createRemoteStream();
        handle(RpcSystemCalls.item, 1, [0, '10,20,30,40']);
        handle(RpcSystemCalls.item, 1, [1, { row: '1,2,3,4' }]);
        handle(RpcSystemCalls.end, 1, [2, null]);

        const items: unknown[] = [];
        for await (const item of stream)
            items.push(item);
        expect(items).toEqual(['10,20,30,40', { row: '1,2,3,4' }]);
    });

    it('delivers $sys.B batches verbatim', async () => {
        const stream = createRemoteStream();
        handle(RpcSystemCalls.batch, 1, [0, ['a,1,2,3', 'x,1,2,3xyz']]);
        handle(RpcSystemCalls.end, 1, [2, null]);

        const items: unknown[] = [];
        for await (const item of stream)
            items.push(item);
        expect(items).toEqual(['a,1,2,3', 'x,1,2,3xyz']);
    });
});
