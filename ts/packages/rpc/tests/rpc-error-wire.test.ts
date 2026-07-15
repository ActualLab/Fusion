import { describe, it, expect } from 'vitest';
import {
    RpcSystemCallSender,
    RpcSystemCallHandler,
    RpcStream,
    RpcStreamSender,
    RpcSerializationFormat,
    RpcSystemCalls,
    RpcOutboundCall,
    REMOTE_EXCEPTION_TYPE_REF,
    toExceptionInfo,
    deserializeMessage,
} from '../src/index.js';
import type { RpcConnection, RpcClientPeer } from '../src/index.js';
import { createTestHubPair, delay } from './rpc-test-helpers.js';

function captureConn(): { conn: RpcConnection; sent: () => string } {
    let last = '';
    const conn = {
        send: (s: string) => { last = s; },
        sendBinary: () => { throw new Error('unexpected binary send'); },
        encoder: undefined,
    } as unknown as RpcConnection;
    return { conn, sent: () => last };
}

describe('R2: JS errors carry a RemoteException TypeRef on the wire', () => {
    it('toExceptionInfo folds the error name into the message', () => {
        expect(toExceptionInfo(new Error('boom'))).toEqual({
            TypeRef: REMOTE_EXCEPTION_TYPE_REF,
            Message: 'Error: boom',
        });
        const typed = new TypeError('bad');
        expect(toExceptionInfo(typed)).toEqual({
            TypeRef: REMOTE_EXCEPTION_TYPE_REF,
            Message: 'TypeError: bad',
        });
        expect(toExceptionInfo('raw string')).toEqual({
            TypeRef: REMOTE_EXCEPTION_TYPE_REF,
            Message: 'Error: raw string',
        });
    });

    it('$sys.Error sends TypeRef + "{name}: {message}"', () => {
        const sender = new RpcSystemCallSender();
        const format = RpcSerializationFormat.get('json5np');
        const { conn, sent } = captureConn();

        sender.error(conn, format, 5, new RangeError('out'));
        const { message, args } = deserializeMessage(sent());
        expect(message.Method).toBe(RpcSystemCalls.error);
        expect(message.RelatedId).toBe(5);
        expect(args[0]).toEqual({
            TypeRef: REMOTE_EXCEPTION_TYPE_REF,
            Message: 'RangeError: out',
        });
    });

    it('stream sendEnd(error) uses the same RemoteException convention', async () => {
        const pair = createTestHubPair('json5np');
        await delay(10);
        const sender = new RpcStreamSender(pair.serverPeer);

        let captured: { TypeRef: string; Message: string } | undefined;
        pair.serverHub.systemCallSender.end = ((
            _conn: unknown, _format: unknown, _localId: number, _index: number,
            error: { TypeRef: string; Message: string }
        ) => {
            captured = error;
        }) as unknown as typeof pair.serverHub.systemCallSender.end;

        sender.sendEnd(new TypeError('mid-stream'));
        expect(captured).toEqual({
            TypeRef: REMOTE_EXCEPTION_TYPE_REF,
            Message: 'TypeError: mid-stream',
        });

        pair.serverHub.close();
        pair.clientHub.close();
    });

    it('stream sendEnd(null) emits an empty ExceptionInfo (clean completion)', async () => {
        const pair = createTestHubPair('json5np');
        await delay(10);
        const sender = new RpcStreamSender(pair.serverPeer);

        let captured: { TypeRef: string; Message: string } | undefined;
        pair.serverHub.systemCallSender.end = ((
            _conn: unknown, _format: unknown, _localId: number, _index: number,
            error: { TypeRef: string; Message: string }
        ) => {
            captured = error;
        }) as unknown as typeof pair.serverHub.systemCallSender.end;

        sender.sendEnd(null);
        expect(captured).toEqual({ TypeRef: '', Message: '' });

        pair.serverHub.close();
        pair.clientHub.close();
    });
});

describe('R15: $sys.End error detection keys on TypeRef presence', () => {
    function makeStream(localId: number, peer: RpcClientPeer): { stream: RpcStream<string>; ended: () => Error | null | undefined } {
        const ref = {
            hostId: 'h',
            localId,
            ackPeriod: 10,
            ackAdvance: 5,
            allowReconnect: true,
            isRealTime: false,
        };
        const stream = new RpcStream<string>(ref, peer);
        let captured: Error | null | undefined;
        stream.onEnd = (_index: number, error: Error | null) => {
            captured = error;
        };
        peer.remoteObjects.register(stream);
        return { stream, ended: () => captured };
    }

    it('treats a TypeRef-only End (empty Message) as an error and carries the type name', () => {
        const pair = createTestHubPair('json5np');
        const handler = new RpcSystemCallHandler();
        const { ended } = makeStream(101, pair.clientPeer);

        handler.handle(
            { Method: RpcSystemCalls.end, RelatedId: 101 },
            [3, { TypeRef: 'System.InvalidOperationException, System.Private.CoreLib', Message: '' }],
            pair.clientPeer
        );
        const error = ended();
        expect(error).toBeInstanceOf(Error);
        expect(error!.name).toBe('System.InvalidOperationException');

        pair.serverHub.close();
        pair.clientHub.close();
    });

    it('treats an empty TypeRef + empty Message as clean completion (null)', () => {
        const pair = createTestHubPair('json5np');
        const handler = new RpcSystemCallHandler();
        const { ended } = makeStream(102, pair.clientPeer);

        handler.handle(
            { Method: RpcSystemCalls.end, RelatedId: 102 },
            [4, { TypeRef: '', Message: '' }],
            pair.clientPeer
        );
        expect(ended()).toBeNull();

        pair.serverHub.close();
        pair.clientHub.close();
    });

    it('falls back to Message when TypeRef is absent', () => {
        const pair = createTestHubPair('json5np');
        const handler = new RpcSystemCallHandler();
        const { ended } = makeStream(103, pair.clientPeer);

        handler.handle(
            { Method: RpcSystemCalls.end, RelatedId: 103 },
            [2, { Message: 'stream broke' }],
            pair.clientPeer
        );
        const error = ended();
        expect(error).toBeInstanceOf(Error);
        expect(error!.message).toBe('stream broke');

        pair.serverHub.close();
        pair.clientHub.close();
    });
});

describe('R7: polymorphic payloads fail loudly via the arity guard', () => {
    // A polymorphic .NET payload decodes its type-marker bytes as extra leading
    // msgpack values, so $sys.Ok arrives with >1 values and $sys.I/$sys.B with >2.
    function makeStream(localId: number, peer: RpcClientPeer): { ended: () => Error | null | undefined } {
        const ref = {
            hostId: 'h',
            localId,
            ackPeriod: 10,
            ackAdvance: 5,
            allowReconnect: true,
            isRealTime: false,
        };
        const stream = new RpcStream<string>(ref, peer);
        let captured: Error | null | undefined;
        stream.onEnd = (_index: number, error: Error | null) => {
            captured = error;
        };
        peer.remoteObjects.register(stream);
        return { ended: () => captured };
    }

    it('$sys.Ok with extra values rejects the call instead of resolving a marker byte', async () => {
        const pair = createTestHubPair('json5np');
        const handler = new RpcSystemCallHandler();
        const call = new RpcOutboundCall(11, 'Svc.get:1');
        pair.clientPeer.outboundCalls.register(call);

        handler.handle(
            { Method: RpcSystemCalls.ok, RelatedId: 11 },
            [0, 0, 'actual result'],
            pair.clientPeer
        );
        await expect(call.result.promise).rejects.toThrow(
            /polymorphic payloads are not supported/i
        );
        expect(pair.clientPeer.outboundCalls.get(11)).toBeUndefined();

        pair.serverHub.close();
        pair.clientHub.close();
    });

    it('$sys.I with extra values errors the stream', () => {
        const pair = createTestHubPair('json5np');
        const handler = new RpcSystemCallHandler();
        const { ended } = makeStream(201, pair.clientPeer);

        handler.handle(
            { Method: RpcSystemCalls.item, RelatedId: 201 },
            [0, 0, 0, 'item'],
            pair.clientPeer
        );
        const error = ended();
        expect(error).toBeInstanceOf(Error);
        expect(error!.message).toMatch(/polymorphic payloads are not supported/i);

        pair.serverHub.close();
        pair.clientHub.close();
    });

    it('$sys.B with extra values errors the stream', () => {
        const pair = createTestHubPair('json5np');
        const handler = new RpcSystemCallHandler();
        const { ended } = makeStream(202, pair.clientPeer);

        handler.handle(
            { Method: RpcSystemCalls.batch, RelatedId: 202 },
            [0, 0, 0, ['a', 'b']],
            pair.clientPeer
        );
        const error = ended();
        expect(error).toBeInstanceOf(Error);
        expect(error!.message).toMatch(/polymorphic payloads are not supported/i);

        pair.serverHub.close();
        pair.clientHub.close();
    });
});
