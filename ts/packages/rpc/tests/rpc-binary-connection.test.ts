import { describe, it, expect } from 'vitest';
import {
    RpcWebSocketConnection,
    serializeBinaryMessage,
    serializeMessage,
} from '../src/index.js';
import type { RpcReceivedMessage } from '../src/index.js';
import { createMockWsPair } from './mock-ws.js';

function delay(ms: number): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

describe('RpcWebSocketConnection binary mode', () => {
    it('constructor sets binaryType to arraybuffer', async () => {
        const [wsA] = createMockWsPair();
        const conn = new RpcWebSocketConnection(wsA, true);
        expect(wsA.binaryType).toBe('arraybuffer');
        conn.close();
    });

    it('binary round-trip via mock WS pair', async () => {
        const [wsA, wsB] = createMockWsPair();
        const connA = new RpcWebSocketConnection(wsA, true);
        const connB = new RpcWebSocketConnection(wsB, true);

        await connA.whenConnected;
        await connB.whenConnected;

        const received: RpcReceivedMessage[] = [];
        connB.messageReceived.add(msg => received.push(msg));

        const message = { Method: 'TestService.Hello', RelatedId: 42, CallType: 1 };
        const args = ['world', 123];
        const data = serializeBinaryMessage(message, args, connA.encoder);
        connA.sendBinary(data);

        await delay(10);

        expect(received.length).toBe(1);
        const msg = received[0]!;
        expect(msg.kind).toBe('binary');
        if (msg.kind === 'binary') {
            expect(msg.message.Method).toBe('TestService.Hello');
            expect(msg.message.RelatedId).toBe(42);
            expect(msg.message.CallType).toBe(1);
            expect(msg.args).toEqual(['world', 123]);
        }

        connA.close();
        connB.close();
    });

    it('text mode still works', async () => {
        const [wsA, wsB] = createMockWsPair();
        const connA = new RpcWebSocketConnection(wsA, false);
        const connB = new RpcWebSocketConnection(wsB, false);

        await connA.whenConnected;
        await connB.whenConnected;

        const received: RpcReceivedMessage[] = [];
        connB.messageReceived.add(msg => received.push(msg));

        const message = { Method: 'TestService.Greet', RelatedId: 7, CallType: 0 };
        const serialized = serializeMessage(message, ['hello']);
        connA.send(serialized);

        await delay(10);

        expect(received.length).toBe(1);
        const msg = received[0]!;
        expect(msg.kind).toBe('text');
        if (msg.kind === 'text') {
            expect(msg.raw).toBe(serialized);
        }

        connA.close();
        connB.close();
    });

    it('sendTextBatch delivers multiple text messages', async () => {
        const [wsA, wsB] = createMockWsPair();
        const connA = new RpcWebSocketConnection(wsA, false);
        const connB = new RpcWebSocketConnection(wsB, false);

        await connA.whenConnected;
        await connB.whenConnected;

        const received: RpcReceivedMessage[] = [];
        connB.messageReceived.add(msg => received.push(msg));

        const msg1 = serializeMessage({ Method: 'Svc.A', RelatedId: 1, CallType: 0 }, ['a']);
        const msg2 = serializeMessage({ Method: 'Svc.B', RelatedId: 2, CallType: 0 }, ['b']);
        connA.sendTextBatch([msg1, msg2]);

        await delay(10);

        expect(received.length).toBe(2);
        expect(received[0]!.kind).toBe('text');
        expect(received[1]!.kind).toBe('text');
        if (received[0]!.kind === 'text') {
            expect(received[0]!.raw).toBe(msg1);
        }
        if (received[1]!.kind === 'text') {
            expect(received[1]!.raw).toBe(msg2);
        }

        connA.close();
        connB.close();
    });

    it('sendBinaryBatch delivers multiple binary messages', async () => {
        const [wsA, wsB] = createMockWsPair();
        const connA = new RpcWebSocketConnection(wsA, true);
        const connB = new RpcWebSocketConnection(wsB, true);

        await connA.whenConnected;
        await connB.whenConnected;

        const received: RpcReceivedMessage[] = [];
        connB.messageReceived.add(msg => received.push(msg));

        const bin1 = serializeBinaryMessage(
            { Method: 'Svc.X', RelatedId: 10, CallType: 1 },
            [100],
            connA.encoder,
        );
        const bin2 = serializeBinaryMessage(
            { Method: 'Svc.Y', RelatedId: 20, CallType: 2 },
            ['two'],
            connA.encoder,
        );
        connA.sendBinaryBatch([bin1, bin2]);

        await delay(10);

        expect(received.length).toBe(2);

        const r0 = received[0]!;
        expect(r0.kind).toBe('binary');
        if (r0.kind === 'binary') {
            expect(r0.message.Method).toBe('Svc.X');
            expect(r0.message.RelatedId).toBe(10);
            expect(r0.args).toEqual([100]);
        }

        const r1 = received[1]!;
        expect(r1.kind).toBe('binary');
        if (r1.kind === 'binary') {
            expect(r1.message.Method).toBe('Svc.Y');
            expect(r1.message.RelatedId).toBe(20);
            expect(r1.args).toEqual(['two']);
        }

        connA.close();
        connB.close();
    });
});
