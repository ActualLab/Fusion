import { describe, it, expect } from 'vitest';
import {
    deserializeBinaryMessage,
    deserializeCompactBinaryMessage,
    serializeBinaryMessage,
    serializeBinaryFrame,
    splitBinaryFrame,
} from '../src/index.js';

// Minimal V5 envelope with a hand-written 4-byte LE argDataLen: flags byte,
// VarUint relatedId, VarUint method length (0 = empty method), argDataLen.
function envelopeWithArgDataLen(argDataLen: number): Uint8Array {
    const envelope = new Uint8Array(7);
    const view = new DataView(envelope.buffer);
    view.setInt32(3, argDataLen, true);
    return envelope;
}

describe('RPC binary parser hardening', () => {
    it('rejects a negative argDataLen instead of moving the read cursor backwards', () => {
        // -6 made the old signed read land bytesRead at 1 for this 7-byte envelope,
        // so splitBinaryFrame reparsed it at overlapping offsets forever.
        const frame = envelopeWithArgDataLen(-6);

        expect(() => deserializeBinaryMessage(frame, 0)).toThrow(/out of bounds/);
    });

    it('rejects an argDataLen that runs past the end of the frame', () => {
        const frame = envelopeWithArgDataLen(1024);

        expect(() => deserializeBinaryMessage(frame, 0)).toThrow(/out of bounds/);
    });

    it('rejects a truncated envelope with a clear error', () => {
        const frame = new Uint8Array(4);

        expect(() => deserializeBinaryMessage(frame, 0)).toThrow(/truncated envelope/);
        expect(() => deserializeCompactBinaryMessage(frame, 0)).toThrow(/truncated envelope/);
    });

    it('rejects a header that runs past the end of the frame', () => {
        // headerCount = 1, but the header key length points past the frame end —
        // which used to produce a NaN position and thus a NaN bytesRead.
        const frame = new Uint8Array(8);
        frame[0] = 1;
        frame[7] = 0x40;

        expect(() => deserializeBinaryMessage(frame, 0)).toThrow(/out of bounds|truncated header/);
    });

    it('rejects a method length that runs past the end of the frame', () => {
        const frame = new Uint8Array(8);
        frame[2] = 0x7f;

        expect(() => deserializeBinaryMessage(frame, 0)).toThrow(/out of bounds/);
    });

    it('still splits a well-formed multi-message frame', () => {
        const frame = serializeBinaryFrame([
            serializeBinaryMessage({ Method: '$sys.Ok', RelatedId: 1 }, [1]),
            serializeBinaryMessage({ Method: '$sys.Ok', RelatedId: 2 }, [2]),
        ]);
        const messages = splitBinaryFrame(frame);

        expect(messages.map(m => m.message.RelatedId)).toEqual([1, 2]);
        expect(messages.map(m => m.args)).toEqual([[1], [2]]);
    });
});
