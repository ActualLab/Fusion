import { describe, it, expect } from 'vitest';
import { IncreasingSeqCompressor } from '../src/index.js';

describe('IncreasingSeqCompressor', () => {
    it('serializes an empty sequence to empty bytes', () => {
        const bytes = IncreasingSeqCompressor.serialize([]);
        expect(bytes.length).toBe(0);
    });

    it('deserializes empty bytes to empty list', () => {
        const out = IncreasingSeqCompressor.deserialize(new Uint8Array(0));
        expect(out).toEqual([]);
    });

    it('round-trips small non-decreasing sequences', () => {
        for (const input of [
            [0],
            [1, 2, 3],
            [5, 5, 5],               // duplicates are allowed (delta=0)
            [1, 2, 3, 100, 10_000, 2_000_000],
            [Number.MAX_SAFE_INTEGER],
        ]) {
            const bytes = IncreasingSeqCompressor.serialize(input);
            const out = IncreasingSeqCompressor.deserialize(bytes);
            expect(out).toEqual(input);
        }
    });

    it('rejects decreasing sequences', () => {
        expect(() => IncreasingSeqCompressor.serialize([5, 3])).toThrow();
    });

    it('rejects negative values', () => {
        expect(() => IncreasingSeqCompressor.serialize([-1])).toThrow();
    });

    it('rejects non-integer values', () => {
        expect(() => IncreasingSeqCompressor.serialize([1.5])).toThrow();
    });

    it('produces the expected bytes for small single values (LEB128 fixture)', () => {
        // Known LEB128 encodings. These fixtures are mirrored in the .NET
        // test `CrossPlatformWireFormatFixtures` at
        // tests/ActualLab.Tests/Rpc/IncreasingSeqCompressorTest.cs — both
        // implementations must produce the same bytes for these inputs,
        // which is the cross-platform wire contract for `$sys.Reconnect`.
        expect(Array.from(IncreasingSeqCompressor.serialize([0]))).toEqual([0x00]);
        expect(Array.from(IncreasingSeqCompressor.serialize([1]))).toEqual([0x01]);
        expect(Array.from(IncreasingSeqCompressor.serialize([127]))).toEqual([0x7f]);
        // 128 = 0b10000000 → 0x80 (continuation), 0x01
        expect(Array.from(IncreasingSeqCompressor.serialize([128]))).toEqual([0x80, 0x01]);
        // 300 = 0b100101100 → low 7 bits (44 = 0x2c) with continuation, then 2
        expect(Array.from(IncreasingSeqCompressor.serialize([300]))).toEqual([0xac, 0x02]);
        // 16384 = 2^14 → [0x80, 0x80, 0x01]
        expect(Array.from(IncreasingSeqCompressor.serialize([16384]))).toEqual([0x80, 0x80, 0x01]);
    });

    it('encodes successive deltas (not absolute values)', () => {
        // [10, 11] → delta-encoded as [10, 1]. LEB128 of both fit in one byte.
        expect(Array.from(IncreasingSeqCompressor.serialize([10, 11]))).toEqual([0x0a, 0x01]);

        // [10, 10] → [10, 0]: second delta is 0 (one byte encoding 0x00).
        expect(Array.from(IncreasingSeqCompressor.serialize([10, 10]))).toEqual([0x0a, 0x00]);
    });

    it('round-trips a large absolute value combined with small deltas', () => {
        const input = [1_000_000, 1_000_001, 1_000_002, 2_000_000];
        const bytes = IncreasingSeqCompressor.serialize(input);
        expect(IncreasingSeqCompressor.deserialize(bytes)).toEqual(input);
    });

    it('throws on truncated input', () => {
        // A byte 0x80 signals "more bytes to come" but nothing follows.
        expect(() => IncreasingSeqCompressor.deserialize(new Uint8Array([0x80]))).toThrow();
    });

    it('decodes multi-byte LEB128 fixtures correctly', () => {
        // [128, 128+1] → LEB128 of 128 (= [0x80, 0x01]) then delta=1 (= [0x01]).
        const bytes = new Uint8Array([0x80, 0x01, 0x01]);
        expect(IncreasingSeqCompressor.deserialize(bytes)).toEqual([128, 129]);
    });

    it('accepts Iterable input (not just arrays)', () => {
        function* gen() {
            yield 1;
            yield 3;
            yield 5;
        }
        const out = IncreasingSeqCompressor.deserialize(IncreasingSeqCompressor.serialize(gen()));
        expect(out).toEqual([1, 3, 5]);
    });
});
