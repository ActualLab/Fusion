/**
 * Compresses and decompresses monotonically increasing sequences of
 * integers using variable-length LEB128 encoding of successive deltas.
 *
 * Wire-compatible with .NET `ActualLab.Rpc.Internal.IncreasingSeqCompressor`
 * (src/ActualLab.Rpc/Internal/IncreasingSeqCompressor.cs) — which
 * underlies the `$sys.Reconnect` protocol.
 *
 * Inputs MUST be sorted non-decreasing. Maximum supported value is
 * 2^53 - 1 (JavaScript `Number` safe-integer limit); call IDs in TS
 * never exceed this because they're generated from a 32-bit counter.
 */
export const IncreasingSeqCompressor = {
    /**
     * Serialize a non-decreasing sequence of non-negative integers as
     * LEB128-encoded deltas.
     *
     * Throws `RangeError` if the input is not monotonically non-decreasing,
     * or if a value exceeds `Number.MAX_SAFE_INTEGER`.
     */
    serialize(values: Iterable<number>): Uint8Array {
        // First pass: compute total byte length; second pass: write.
        // (A growable buffer would also work, but values are small here.)
        const deltas: number[] = [];
        let last = 0;
        let totalBytes = 0;
        for (const value of values) {
            if (!Number.isSafeInteger(value) || value < 0)
                throw new RangeError(
                    `IncreasingSeqCompressor: value must be a non-negative safe integer (got ${value}).`);
            const delta = value - last;
            if (delta < 0)
                throw new RangeError(
                    `IncreasingSeqCompressor: sequence must be non-decreasing (saw ${value} after ${last}).`);
            deltas.push(delta);
            last = value;
            totalBytes += _varUintByteLength(delta);
        }

        const out = new Uint8Array(totalBytes);
        let offset = 0;
        for (const delta of deltas) {
            offset = _writeVarUint(out, delta, offset);
        }
        return out;
    },

    /**
     * Decode bytes produced by {@link serialize} back into the original
     * sorted sequence of non-negative integers.
     */
    deserialize(data: Uint8Array): number[] {
        const result: number[] = [];
        let last = 0;
        let offset = 0;
        while (offset < data.length) {
            const [delta, next] = _readVarUint(data, offset);
            offset = next;
            last += delta;
            result.push(last);
        }
        return result;
    },
} as const;

// -- LEB128 (variable-length unsigned) encoding -----------------------------
// Kept in sync with .NET SpanExt.WriteVarUInt64 / ReadVarUInt64 at
// src/ActualLab.Core/Collections/SpanExt.ReadWriteVarUInt.cs:22-91.
// We use `Number` arithmetic up to the 53-bit safe-integer ceiling; values
// beyond that would need BigInt, which is not required for TS call IDs.

const LOW_BITS = 0x7f;
const HIGH_BIT = 0x80;

function _varUintByteLength(value: number): number {
    let n = 0;
    let v = value;
    while (v >= HIGH_BIT) {
        n++;
        v = Math.floor(v / 128);
    }
    return n + 1;
}

function _writeVarUint(out: Uint8Array, value: number, offset: number): number {
    let v = value;
    while (v >= HIGH_BIT) {
        out[offset++] = (HIGH_BIT | (v & LOW_BITS)) & 0xff;
        v = Math.floor(v / 128);
    }
    out[offset++] = v & 0xff;
    return offset;
}

function _readVarUint(data: Uint8Array, offset: number): [number, number] {
    let value = 0;
    let shift = 0;
    // Up to 9 bytes contribute 7 bits each = 63 bits.
    while (shift < 63) {
        if (offset >= data.length)
            throw new RangeError('IncreasingSeqCompressor: truncated VarUInt.');
        const b = data[offset++];
        // Multiply by 2^shift using Math.pow to stay in Number arithmetic.
        // For shift up to 49 (7*7) the intermediate `(b & LOW_BITS) * 2^shift`
        // stays within Number.MAX_SAFE_INTEGER; beyond that the caller is
        // expected to keep call IDs within 2^53.
        value += (b & LOW_BITS) * Math.pow(2, shift);
        if (b <= LOW_BITS) return [value, offset];
        shift += 7;
    }
    // 10th byte — matches .NET's 1-bit allowance.
    if (offset >= data.length)
        throw new RangeError('IncreasingSeqCompressor: truncated VarUInt.');
    const b = data[offset++];
    if (b > 1)
        throw new RangeError('IncreasingSeqCompressor: VarUInt value exceeds 64 bits.');
    value += b * Math.pow(2, 63);
    return [value, offset];
}
