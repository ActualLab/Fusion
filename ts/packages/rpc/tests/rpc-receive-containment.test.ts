import { describe, it, expect } from 'vitest';
import {
    RpcWebSocketConnection,
    serializeBinaryFrame,
    serializeBinaryMessage,
    serializeFrame,
    serializeMessage,
    splitBinaryFrame,
    type RpcReceivedMessage,
} from '../src/index.js';
import { createMockWsPair } from './mock-ws.js';

function relatedIdsOf(received: RpcReceivedMessage[]): number[] {
    return received.map(m =>
        m.kind === 'binary'
            ? (m.message.RelatedId ?? 0)
            : (JSON.parse(m.raw.split('\n')[0]) as { RelatedId?: number }).RelatedId ?? 0);
}

// A frame the receiver must not be able to fully parse: a valid envelope followed
// by one whose argDataLen points past the end of the frame.
function frameWithMalformedTail(): Uint8Array {
    const malformed = new Uint8Array(7);
    new DataView(malformed.buffer).setUint32(3, 0xffff, true);
    return serializeBinaryFrame([
        serializeBinaryMessage({ Method: '$sys.Ok', RelatedId: 1 }, [1]),
        malformed,
    ]);
}

describe('RPC receive path containment', () => {
    it('keeps the messages decoded before a malformed one in the same frame', () => {
        const messages = splitBinaryFrame(frameWithMalformedTail());

        expect(messages.map(m => m.message.RelatedId)).toEqual([1]);
    });

    it('does not drop the frame-mates of a binary message whose handler throws', async () => {
        const [wsA, wsB] = createMockWsPair();
        const connA = new RpcWebSocketConnection(wsA, true);
        const connB = new RpcWebSocketConnection(wsB, true);
        const received: RpcReceivedMessage[] = [];
        connB.messageReceived.add(m => {
            if (m.kind === 'binary' && m.message.RelatedId === 2)
                throw new Error('handler blew up');

            received.push(m);
        });

        await connA.whenConnected;
        connA.sendBinary(serializeBinaryFrame([1, 2, 3].map(id =>
            serializeBinaryMessage({ Method: '$sys.Ok', RelatedId: id }, [id]))));
        await new Promise(resolve => setTimeout(resolve, 10));

        expect(relatedIdsOf(received)).toEqual([1, 3]);
    });

    it('does not drop the frame-mates of a text message whose handler throws', async () => {
        const [wsA, wsB] = createMockWsPair();
        const connA = new RpcWebSocketConnection(wsA);
        const connB = new RpcWebSocketConnection(wsB);
        const received: RpcReceivedMessage[] = [];
        connB.messageReceived.add(m => {
            if (m.kind === 'text' && m.raw.includes('"RelatedId":2'))
                throw new Error('handler blew up');

            received.push(m);
        });

        await connA.whenConnected;
        connA.send(serializeFrame([1, 2, 3].map(id =>
            serializeMessage({ Method: '$sys.Ok', RelatedId: id }, [id]))));
        await new Promise(resolve => setTimeout(resolve, 10));

        expect(relatedIdsOf(received)).toEqual([1, 3]);
    });
});
