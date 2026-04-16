import { describe, it, expect } from 'vitest';
import { Encoder, Decoder } from '@msgpack/msgpack';
import { base64Encode, base64Decode } from '../src/base64.js';
import { IncreasingSeqCompressor } from '../src/index.js';
// Ensure the Encoder.prototype patch is applied before we encode any Map.
import '../src/msgpack-map-patch.js';

/**
 * Cross-platform wire-format contract tests for `$sys.Reconnect`.
 *
 * Both TS and .NET must agree on the exact bytes produced for the
 * `completedStagesData` argument — otherwise a TS client cannot talk to
 * a .NET server (and vice versa) over either wire format.
 *
 * The fixtures below were captured from .NET and encode
 * `Dictionary<int, byte[]>` / `Dictionary<string, byte[]>` via
 * `MessagePackSerializer.Serialize` and `System.Text.Json.JsonSerializer.Serialize`.
 * They are cross-referenced by the .NET test
 * `RpcReconnectWireFormatTest` — both test files share the same inputs
 * and expected outputs.
 */
describe('$sys.Reconnect wire-format: MessagePack (matches .NET MessagePack-CSharp)', () => {
    const encoder = new Encoder();

    function bytesToHex(b: Uint8Array): string {
        return Array.from(b).map(x => x.toString(16).padStart(2, '0')).join(' ');
    }

    it('encodes Map<number, Uint8Array> {1:[0a,0b]} to the .NET-compatible byte sequence', () => {
        const m = new Map<number, Uint8Array>();
        m.set(1, new Uint8Array([0x0a, 0x0b]));
        expect(bytesToHex(encoder.encode(m))).toBe('81 01 c4 02 0a 0b');
    });

    it('encodes Map<number, Uint8Array> {1:[0a],3:[0b,0c]} to the .NET-compatible byte sequence', () => {
        const m = new Map<number, Uint8Array>();
        m.set(1, new Uint8Array([0x0a]));
        m.set(3, new Uint8Array([0x0b, 0x0c]));
        expect(bytesToHex(encoder.encode(m))).toBe('82 01 c4 01 0a 03 c4 02 0b 0c');
    });

    it('encodes empty Map to an empty fixmap (0x80)', () => {
        expect(bytesToHex(encoder.encode(new Map()))).toBe('80');
    });

    it('decodes .NET-produced map<int, bin> into a string-keyed object with Uint8Array values', () => {
        const decoder = new Decoder();
        const bytes = new Uint8Array([0x82, 0x01, 0xc4, 0x01, 0x0a, 0x03, 0xc4, 0x02, 0x0b, 0x0c]);
        const decoded = decoder.decode(bytes) as Record<string, Uint8Array>;
        expect(Object.keys(decoded)).toEqual(['1', '3']);
        expect(Array.from(decoded['1'])).toEqual([0x0a]);
        expect(Array.from(decoded['3'])).toEqual([0x0b, 0x0c]);
    });

    it('end-to-end round-trip preserves the stage→IDs mapping', () => {
        const decoder = new Decoder();
        // Build the same shape we send from the client.
        const stages = new Map<number, Uint8Array>();
        stages.set(1, IncreasingSeqCompressor.serialize([10, 20, 30]));
        stages.set(3, IncreasingSeqCompressor.serialize([5]));

        const wire = encoder.encode(stages);
        const roundTripped = decoder.decode(wire) as Record<string, Uint8Array>;

        const stage1Ids = IncreasingSeqCompressor.deserialize(roundTripped['1']);
        expect(stage1Ids).toEqual([10, 20, 30]);
        const stage3Ids = IncreasingSeqCompressor.deserialize(roundTripped['3']);
        expect(stage3Ids).toEqual([5]);
    });
});

describe('$sys.Reconnect wire-format: JSON (matches .NET System.Text.Json)', () => {
    it('encodes the stages dict as {"<stage>": "<base64>"} — matching .NET Dictionary<int, byte[]> JSON shape', () => {
        // .NET System.Text.Json emits Dictionary<int, byte[]> with stringified
        // integer keys and base64-string values:
        //   {"1":"Cgs="}
        const bytes = new Uint8Array([0x0a, 0x0b]);
        const obj: Record<string, string> = { '1': base64Encode(bytes) };
        const json = JSON.stringify(obj);
        expect(json).toBe('{"1":"Cgs="}');
    });

    it('encodes multi-entry stages dict in a .NET-compatible order-agnostic shape', () => {
        const obj: Record<string, string> = {
            '1': base64Encode(new Uint8Array([0x0a])),
            '3': base64Encode(new Uint8Array([0x0b, 0x0c])),
        };
        const parsed = JSON.parse(JSON.stringify(obj)) as Record<string, string>;
        expect(Array.from(base64Decode(parsed['1']))).toEqual([0x0a]);
        expect(Array.from(base64Decode(parsed['3']))).toEqual([0x0b, 0x0c]);
    });

    it('decodes .NET-produced JSON bytes back to our stage→IDs mapping', () => {
        const netProducedJson = '{"1":"Cgs="}';  // Dict<int, byte[]> {1: [0x0a, 0x0b]}
        const parsed = JSON.parse(netProducedJson) as Record<string, string>;
        const bytes = base64Decode(parsed['1']);
        expect(Array.from(bytes)).toEqual([0x0a, 0x0b]);
    });
});
