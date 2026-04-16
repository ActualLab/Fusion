// Serialization for the RPC wire format.
//
// Supports two formats:
//   - Text (json5np): JSON envelope + delimiter-separated JSON args (V3 wire format)
//   - Binary (msgpack6np): V5 binary envelope + MessagePack-encoded args
//
// The format is selected at connection time via the `f=` query parameter.

import {
    decode as _msgpackDecode,
    Encoder,
    Decoder,
    type DecodeOptions,
} from '@msgpack/msgpack';
// Side-effect import: patches Encoder.prototype to handle JS Map instances
// as proper msgpack maps with typed keys (.NET-wire-compatible).
import './msgpack-map-patch.js';
import type { RpcMessage } from './rpc-message.js';
import {
    ENVELOPE_DELIMITER,
    ARG_DELIMITER,
    FRAME_DELIMITER,
} from './rpc-message.js';

// ============================================================
// Text format (json5np) — V3 wire format
// ============================================================

/** Serializes an RpcMessage + args into the json5 wire format. */
export function serializeMessage(
    message: RpcMessage,
    args?: unknown[]
): string {
    const envelope = JSON.stringify(message);
    if (args === undefined || args.length === 0) {
        return envelope + ENVELOPE_DELIMITER;
    }
    const argsStr = args.map(a => JSON.stringify(a)).join(ARG_DELIMITER);
    return envelope + ENVELOPE_DELIMITER + argsStr;
}

/** Serializes multiple messages into a single WebSocket frame. */
export function serializeFrame(messages: string[]): string {
    return messages.join(FRAME_DELIMITER);
}

/** Splits a WebSocket text frame into individual message strings. */
export function splitFrame(frame: string): string[] {
    return frame.split(FRAME_DELIMITER);
}

/** Deserializes a single message string into envelope + args. */
export function deserializeMessage(raw: string): {
    message: RpcMessage;
    args: unknown[];
} {
    const nlIndex = raw.indexOf(ENVELOPE_DELIMITER);
    if (nlIndex === -1) {
        return { message: JSON.parse(raw) as RpcMessage, args: [] };
    }

    const envelopeStr = raw.substring(0, nlIndex);
    const argsStr = raw.substring(nlIndex + 1);
    const message = JSON.parse(envelopeStr) as RpcMessage;

    if (argsStr.length === 0) {
        return { message, args: [] };
    }

    const args = argsStr.split(ARG_DELIMITER).map(s => {
        try {
            return JSON.parse(s) as unknown;
        } catch {
            console.warn(
                `[RpcSerialization] failed to parse arg as JSON: ${s.substring(0, 100)}`
            );
            return s; // Return raw string if not valid JSON
        }
    });

    return { message, args };
}

// ============================================================
// Binary format (msgpack6np) — V5 wire format
// ============================================================

// MessagePack decode options
const _msgpackDecodeOptions: DecodeOptions = {};

const textEncoder = new TextEncoder();
const textDecoder = new TextDecoder();

/**
 * Default module-level encoder/decoder. RpcWebSocketConnection passes its own
 * per-connection instances for the hot path; these defaults cover callers
 * that don't bother (tests, one-shot uses).
 *
 * Reusing an Encoder across calls is critical at high message rates because
 * - construction cost (internal resolvers, buffer allocation) is nontrivial;
 * - the internal write buffer stays at its largest observed size after the
 *   first few encodes, eliminating `resizeBuffer` growth churn (which was
 *   ~5% self time in the 300-pull profile).
 *
 * NOT thread-safe and NOT re-entrant. Safe under Node's single-threaded event
 * loop as long as `serializeBinaryMessage` is called synchronously (which it
 * always is — no awaits inside).
 *
 * The `@msgpack/msgpack` Encoder constructor takes positional args:
 *   (extensionCodec, context, maxDepth, initialBufferSize, sortKeys, ...)
 * We only need initialBufferSize here — set large enough to fit a typical
 * video frame (~11 KB) + envelope overhead without any resizeBuffer growth.
 */
const INITIAL_ENCODER_BUFFER_SIZE = 32 * 1024;
export const defaultBinaryEncoder = new Encoder(
    undefined,
    undefined,
    undefined,
    INITIAL_ENCODER_BUFFER_SIZE
);
export const defaultBinaryDecoder = new Decoder();
/** Exposed so connection code can instantiate a matching Encoder
 *  with the same initial buffer size without needing to reach into
 *  this module's private constants. */
export function createBinaryEncoder(): Encoder {
    return new Encoder(
        undefined,
        undefined,
        undefined,
        INITIAL_ENCODER_BUFFER_SIZE
    );
}

// --- LEB128 VarUInt helpers ---

/** Number of bytes a VarUint encoding of `value` will occupy. */
function varUintByteLen(value: number): number {
    if (value < 0) value = 0;
    let n = 0;
    do {
        n++;
        value >>>= 7;
    } while (value > 0);
    return n;
}

/** Write VarUint directly into a `Uint8Array` at `pos`. Returns new pos. */
function writeVarUintInto(out: Uint8Array, pos: number, value: number): number {
    if (value < 0) value = 0;
    do {
        let byte = value & 0x7f;
        value >>>= 7;
        if (value > 0) byte |= 0x80;
        out[pos++] = byte;
    } while (value > 0);
    return pos;
}

function readVarUint(
    data: Uint8Array,
    offset: number
): { value: number; bytesRead: number } {
    let value = 0,
        shift = 0,
        bytesRead = 0;
    do {
        if (offset + bytesRead >= data.length) break;
        const byte = data[offset + bytesRead];
        value |= (byte & 0x7f) << shift;
        bytesRead++;
        if ((byte & 0x80) === 0) break;
        shift += 7;
    } while (shift < 35); // Max 5 bytes for uint32
    return { value: value >>> 0, bytesRead };
}

// --- Binary helpers ---

function concatUint8Arrays(arrays: Uint8Array[]): Uint8Array {
    let totalLength = 0;
    for (const arr of arrays) totalLength += arr.length;
    const result = new Uint8Array(totalLength);
    let offset = 0;
    for (const arr of arrays) {
        result.set(arr, offset);
        offset += arr.length;
    }
    return result;
}

/**
 * Serializes an RpcMessage + args into V5 binary format (msgpack6np).
 *
 * Layout (NO frame-level size prefix; V5 with PersistsMessageSize=false):
 *   envelope + argData
 * Envelope:
 *   [byte 0: (CallTypeId<<5) | HeaderCount] [VarUint relatedId]
 *   [LVarSpan methodRef] [4-byte LE argLen]
 * ArgData: concatenated MessagePack-encoded arguments
 *
 * Single-allocation build: pre-encodes args with the supplied reusable
 * `Encoder`, computes the total envelope size, then writes everything
 * into one `Uint8Array`. Eliminates the previous build's intermediate
 * `headerParts` number array + four `Uint8Array` concats per call,
 * which showed up as ~2% self time in the 300-pull profile and
 * pulled the msgpack `resizeBuffer` + `Encoder` constructor along
 * with it for another ~6%.
 *
 * NOTE: `@msgpack/msgpack` `Encoder.encode()` already returns a fresh
 * copy of the internal buffer (there's a separate `encodeSharedRef()`
 * for zero-copy access), so we can push the results straight into
 * `argBufs` without an extra slice. The benefit of reusing the Encoder
 * is that its internal write buffer stays at its high-water mark —
 * once the first ~11 KB frame has grown it to the full size, no
 * subsequent `resizeBuffer` calls are needed.
 */
export function serializeBinaryMessage(
    message: RpcMessage,
    args?: unknown[],
    encoder: Encoder = defaultBinaryEncoder
): Uint8Array {
    // 1. Encode each arg. encode() returns a fresh Uint8Array copy per
    //    call, so the results are safe to retain across subsequent encodes.
    let argsDataLen = 0;
    let argBufs: Uint8Array[] | null = null;
    if (args && args.length > 0) {
        argBufs = new Array<Uint8Array>(args.length);
        for (let i = 0; i < args.length; i++) {
            const buf = encoder.encode(args[i]);
            argBufs[i] = buf;
            argsDataLen += buf.length;
        }
    }

    // 2. Compute header sizes without intermediate allocations.
    const callType = message.CallType ?? 0;
    const relatedId = message.RelatedId ?? 0;
    const methodBytes = textEncoder.encode(message.Method ?? '');
    const relatedIdLen = varUintByteLen(relatedId);
    const methodLenVarintLen = varUintByteLen(methodBytes.length);
    const headerSize =
        1 + relatedIdLen + methodLenVarintLen + methodBytes.length;
    const totalSize = headerSize + 4 + argsDataLen;

    // 3. Allocate the final wire buffer once and fill it in place.
    const out = new Uint8Array(totalSize);
    let pos = 0;
    out[pos++] = (callType << 5) & 0xe0;
    pos = writeVarUintInto(out, pos, relatedId);
    pos = writeVarUintInto(out, pos, methodBytes.length);
    out.set(methodBytes, pos);
    pos += methodBytes.length;
    // argLen as int32 LE
    out[pos++] = argsDataLen & 0xff;
    out[pos++] = (argsDataLen >>> 8) & 0xff;
    out[pos++] = (argsDataLen >>> 16) & 0xff;
    out[pos++] = (argsDataLen >>> 24) & 0xff;
    if (argBufs !== null) {
        for (const buf of argBufs) {
            out.set(buf, pos);
            pos += buf.length;
        }
    }
    return out;
}

/**
 * Deserializes a single V5 binary message starting at `offset`.
 * Returns parsed message, args, and number of bytes consumed.
 *
 * Accepts an optional `Decoder` for reuse across calls on the same
 * connection — avoids constructing a new `@msgpack/msgpack.Decoder`
 * (and its internal resolver/table state) on every inbound message.
 */
export function deserializeBinaryMessage(
    data: Uint8Array,
    offset: number,
    decoder: Decoder = defaultBinaryDecoder
): { message: RpcMessage; args: unknown[]; bytesRead: number } {
    const view = new DataView(data.buffer, data.byteOffset, data.byteLength);

    // V5: no frame-level size prefix. Parse envelope directly.
    let pos = offset;

    // Byte 0: upper 3 bits = CallTypeId, lower 5 bits = HeaderCount
    const byte0 = data[pos++];
    const callTypeId = (byte0 >> 5) & 0x7;
    const headerCount = byte0 & 0x1f;

    // RelatedId as VarUint
    const relId = readVarUint(data, pos);
    pos += relId.bytesRead;

    // MethodRef as LVarSpan
    const methodLen = readVarUint(data, pos);
    pos += methodLen.bytesRead;
    const methodBytes = data.subarray(pos, pos + methodLen.value);
    const method = textDecoder.decode(methodBytes);
    pos += methodLen.value;

    // ArgData length as fixed 4-byte LE
    const argDataLen = view.getInt32(pos, true);
    pos += 4;

    // Skip headers (if any — TS client doesn't use them)
    if (headerCount > 0) {
        for (let h = 0; h < headerCount; h++) {
            // L1Memory: 1-byte length prefix + key bytes
            const keyLen = data[pos++];
            pos += keyLen;
            // LVarSpan: VarUint length + value bytes
            const valLen = readVarUint(data, pos);
            pos += valLen.bytesRead + valLen.value;
        }
    }

    // Deserialize arguments from argData — multiple concatenated MessagePack values
    const args: unknown[] = [];
    const argEnd = pos + argDataLen;
    if (argDataLen > 0) {
        const argSlice = data.subarray(pos, argEnd);
        // Reuse the provided decoder across calls; `decodeMulti` is a generator
        // so each yield completes before the next `decode` begins, which is
        // safe on a single-threaded event loop.
        for (const decoded of decoder.decodeMulti(argSlice)) {
            args.push(decoded);
        }
        pos = argEnd;
    }

    const message: RpcMessage = {
        Method: method,
        RelatedId: relId.value,
        CallType: callTypeId,
    };

    return { message, args, bytesRead: pos - offset };
}

/**
 * Splits a binary WebSocket frame into individual deserialized messages.
 * V5 envelopes are self-delimiting (header + method + argLen + argData), so
 * multiple envelopes concatenated in a single WebSocket frame can be decoded
 * by iterating with the bytesRead returned from deserializeBinaryMessage.
 *
 * This must stay symmetric with serializeBinaryFrame() (which concatenates
 * multiple envelopes). The .NET server batches outbound RPC messages into a
 * single WebSocket frame via WebSocketChannel/WriteDelayer, so any assumption
 * that a frame is a single envelope silently drops every trailing message.
 *
 * Accepts an optional `Decoder` for reuse across calls on the same
 * connection — forwarded to `deserializeBinaryMessage`.
 */
export function splitBinaryFrame(
    frame: Uint8Array,
    decoder: Decoder = defaultBinaryDecoder
): { message: RpcMessage; args: unknown[] }[] {
    const results: { message: RpcMessage; args: unknown[] }[] = [];
    let offset = 0;
    while (offset < frame.length) {
        const { message, args, bytesRead } = deserializeBinaryMessage(
            frame,
            offset,
            decoder
        );
        if (bytesRead <= 0) break; // defensive — avoid infinite loop on malformed data
        results.push({ message, args });
        offset += bytesRead;
    }
    return results;
}

/**
 * Serializes multiple binary messages into a single WebSocket frame.
 * Simply concatenates the already size-prefixed messages.
 */
export function serializeBinaryFrame(messages: Uint8Array[]): Uint8Array {
    return concatUint8Arrays(messages);
}

// ============================================================
// Compact binary format (V5Compact) — 4-byte method hash
// ============================================================

import type { RpcMethodRegistry } from './rpc-method-registry.js';

/**
 * Serializes an RpcMessage + args into V5Compact binary format.
 * Same as V5 except the method is encoded as a 4-byte LE hash
 * instead of an LVar-prefixed UTF-8 string.
 *
 * Layout:
 *   [byte 0: (CallTypeId<<5) | HeaderCount] [VarUint relatedId]
 *   [4-byte LE methodHash] [4-byte LE argLen]
 *   [argData]
 */
export function serializeCompactBinaryMessage(
    message: RpcMessage,
    args?: unknown[],
    registry?: RpcMethodRegistry,
    encoder: Encoder = defaultBinaryEncoder
): Uint8Array {
    // 1. Encode args
    let argsDataLen = 0;
    let argBufs: Uint8Array[] | null = null;
    if (args && args.length > 0) {
        argBufs = new Array<Uint8Array>(args.length);
        for (let i = 0; i < args.length; i++) {
            const buf = encoder.encode(args[i]);
            argBufs[i] = buf;
            argsDataLen += buf.length;
        }
    }

    // 2. Get method hash
    const methodName = message.Method ?? '';
    const methodHash = registry?.requireHash(methodName) ?? 0;

    // 3. Compute sizes
    const callType = message.CallType ?? 0;
    const relatedId = message.RelatedId ?? 0;
    const relatedIdLen = varUintByteLen(relatedId);
    // Compact: 4 bytes for hash (instead of LVar method name)
    const headerSize = 1 + relatedIdLen + 4;
    const totalSize = headerSize + 4 + argsDataLen;

    // 4. Build wire buffer
    const out = new Uint8Array(totalSize);
    let pos = 0;
    out[pos++] = (callType << 5) & 0xe0;
    pos = writeVarUintInto(out, pos, relatedId);
    // Method hash as 4-byte LE uint32
    const hashU32 = methodHash >>> 0;
    out[pos++] = hashU32 & 0xff;
    out[pos++] = (hashU32 >>> 8) & 0xff;
    out[pos++] = (hashU32 >>> 16) & 0xff;
    out[pos++] = (hashU32 >>> 24) & 0xff;
    // argLen as int32 LE
    out[pos++] = argsDataLen & 0xff;
    out[pos++] = (argsDataLen >>> 8) & 0xff;
    out[pos++] = (argsDataLen >>> 16) & 0xff;
    out[pos++] = (argsDataLen >>> 24) & 0xff;
    if (argBufs !== null) {
        for (const buf of argBufs) {
            out.set(buf, pos);
            pos += buf.length;
        }
    }
    return out;
}

/**
 * Deserializes a single V5Compact binary message starting at `offset`.
 * Reads a 4-byte LE method hash and resolves it via the registry.
 */
export function deserializeCompactBinaryMessage(
    data: Uint8Array,
    offset: number,
    registry?: RpcMethodRegistry,
    decoder: Decoder = defaultBinaryDecoder
): { message: RpcMessage; args: unknown[]; bytesRead: number } {
    const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    let pos = offset;

    // Byte 0: upper 3 bits = CallTypeId, lower 5 bits = HeaderCount
    const byte0 = data[pos++];
    const callTypeId = (byte0 >> 5) & 0x7;
    const headerCount = byte0 & 0x1f;

    // RelatedId as VarUint
    const relId = readVarUint(data, pos);
    pos += relId.bytesRead;

    // Method hash as fixed 4-byte LE uint32
    const methodHash = view.getUint32(pos, true);
    pos += 4;
    const method =
        registry?.getName(methodHash | 0) ??
        `<hash:0x${methodHash.toString(16).padStart(8, '0')}>`;

    // ArgData length as fixed 4-byte LE
    const argDataLen = view.getInt32(pos, true);
    pos += 4;

    // Skip headers (if any)
    if (headerCount > 0) {
        for (let h = 0; h < headerCount; h++) {
            const keyLen = data[pos++];
            pos += keyLen;
            const valLen = readVarUint(data, pos);
            pos += valLen.bytesRead + valLen.value;
        }
    }

    // Deserialize arguments
    const args: unknown[] = [];
    const argEnd = pos + argDataLen;
    if (argDataLen > 0) {
        const argSlice = data.subarray(pos, argEnd);
        for (const decoded of decoder.decodeMulti(argSlice)) {
            args.push(decoded);
        }
        pos = argEnd;
    }

    const message: RpcMessage = {
        Method: method,
        RelatedId: relId.value,
        CallType: callTypeId,
    };

    return { message, args, bytesRead: pos - offset };
}

/**
 * Splits a compact binary frame into individual deserialized messages.
 */
export function splitCompactBinaryFrame(
    frame: Uint8Array,
    registry?: RpcMethodRegistry,
    decoder: Decoder = defaultBinaryDecoder
): { message: RpcMessage; args: unknown[] }[] {
    const results: { message: RpcMessage; args: unknown[] }[] = [];
    let offset = 0;
    while (offset < frame.length) {
        const { message, args, bytesRead } =
            deserializeCompactBinaryMessage(
                frame,
                offset,
                registry,
                decoder
            );
        if (bytesRead <= 0) break;
        results.push({ message, args });
        offset += bytesRead;
    }
    return results;
}
