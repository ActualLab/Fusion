// RPC serialization format definitions — mirrors .NET RpcSerializationFormat.
//
// Each format knows how to serialize/deserialize RPC messages on the wire.
// Format instances are immutable singletons — state like method registries
// is passed in via parameters, not held by the format.

import { Encoder, Decoder } from '@msgpack/msgpack';
import type { RpcMessage } from './rpc-message.js';
import {
    serializeMessage,
    deserializeMessage,
    serializeBinaryMessage,
    splitBinaryFrame,
    serializeBinaryFrame,
    createBinaryEncoder,
    serializeCompactBinaryMessage,
    splitCompactBinaryFrame,
} from './rpc-serialization.js';
import type { RpcMethodRegistry } from './rpc-method-registry.js';

/** Parsed inbound message — result of deserialization. */
export interface RpcDeserializedMessage {
    message: RpcMessage;
    args: unknown[];
}

/** Wire data produced by serialization — either text or binary. */
export type RpcWireData = string | Uint8Array;

/**
 * Base class for RPC serialization formats.
 * Instances are immutable singletons — one per format key.
 * State (like method registries) is passed via parameters.
 */
export abstract class RpcSerializationFormat {
    abstract readonly key: string;
    abstract readonly isBinary: boolean;
    /** Whether this format uses compact method hashes (needs RpcMethodRegistry). */
    abstract readonly isCompact: boolean;

    /** Serialize an outbound message. */
    abstract serializeMessage(
        message: RpcMessage,
        args?: unknown[],
        encoder?: Encoder,
        registry?: RpcMethodRegistry
    ): RpcWireData;

    /** Deserialize a single text message (only called for text formats). */
    abstract deserializeTextMessage(raw: string): RpcDeserializedMessage;

    /** Split + deserialize a binary frame (only called for binary formats). */
    abstract splitBinaryFrame(
        frame: Uint8Array,
        decoder?: Decoder,
        registry?: RpcMethodRegistry
    ): RpcDeserializedMessage[];

    /** Concatenate multiple serialized binary messages into one frame. */
    abstract serializeBinaryFrame(messages: Uint8Array[]): Uint8Array;

    /** Create a reusable binary encoder for this format. Returns undefined for text formats. */
    createEncoder(): Encoder | undefined {
        return undefined;
    }

    /** Create a reusable binary decoder for this format. Returns undefined for text formats. */
    createDecoder(): Decoder | undefined {
        return undefined;
    }

    // --- Static registry ---

    private static _all = new Map<string, RpcSerializationFormat>();

    static register(format: RpcSerializationFormat): void {
        RpcSerializationFormat._all.set(format.key, format);
    }

    static get(key: string): RpcSerializationFormat {
        const format = RpcSerializationFormat._all.get(key);
        if (!format)
            throw new Error(`Unknown RPC serialization format: "${key}"`);
        return format;
    }

    static tryGet(key: string): RpcSerializationFormat | undefined {
        return RpcSerializationFormat._all.get(key);
    }
}

// ============================================================
// Concrete formats (immutable singletons)
// ============================================================

/**
 * JSON text format — "json5np" (System.Text.Json, no polymorphism).
 */
export class RpcJsonSerializationFormat extends RpcSerializationFormat {
    readonly key: string;
    readonly isBinary = false;
    readonly isCompact = false;

    constructor(key: string) {
        super();
        this.key = key;
    }

    serializeMessage(message: RpcMessage, args?: unknown[]): string {
        return serializeMessage(message, args);
    }

    deserializeTextMessage(raw: string): RpcDeserializedMessage {
        return deserializeMessage(raw);
    }

    splitBinaryFrame(): RpcDeserializedMessage[] {
        throw new Error('Text format does not support binary frames');
    }

    serializeBinaryFrame(): Uint8Array {
        throw new Error('Text format does not support binary frames');
    }
}

/**
 * MessagePack binary format — "msgpack6" / "mempack6" (V5 wire format).
 */
export class RpcMessagePackSerializationFormat extends RpcSerializationFormat {
    readonly key: string;
    readonly isBinary = true;
    readonly isCompact = false;

    constructor(key: string) {
        super();
        this.key = key;
    }

    serializeMessage(
        message: RpcMessage,
        args?: unknown[],
        encoder?: Encoder
    ): Uint8Array {
        return serializeBinaryMessage(message, args, encoder);
    }

    deserializeTextMessage(raw: string): RpcDeserializedMessage {
        return deserializeMessage(raw);
    }

    splitBinaryFrame(
        frame: Uint8Array,
        decoder?: Decoder
    ): RpcDeserializedMessage[] {
        return splitBinaryFrame(frame, decoder);
    }

    serializeBinaryFrame(messages: Uint8Array[]): Uint8Array {
        return serializeBinaryFrame(messages);
    }

    override createEncoder(): Encoder {
        return createBinaryEncoder();
    }

    override createDecoder(): Decoder {
        return new Decoder();
    }
}

/**
 * MessagePack compact binary format — "msgpack6c" / "mempack6c" (V5Compact).
 * Uses 4-byte method hash instead of full method name.
 * Registry is passed via parameters — format instance is immutable.
 */
export class RpcMessagePackCompactSerializationFormat extends RpcSerializationFormat {
    readonly key: string;
    readonly isBinary = true;
    readonly isCompact = true;

    constructor(key: string) {
        super();
        this.key = key;
    }

    serializeMessage(
        message: RpcMessage,
        args?: unknown[],
        encoder?: Encoder,
        registry?: RpcMethodRegistry
    ): Uint8Array {
        return serializeCompactBinaryMessage(
            message,
            args,
            registry,
            encoder
        );
    }

    deserializeTextMessage(raw: string): RpcDeserializedMessage {
        return deserializeMessage(raw);
    }

    splitBinaryFrame(
        frame: Uint8Array,
        decoder?: Decoder,
        registry?: RpcMethodRegistry
    ): RpcDeserializedMessage[] {
        return splitCompactBinaryFrame(frame, registry, decoder);
    }

    serializeBinaryFrame(messages: Uint8Array[]): Uint8Array {
        return serializeBinaryFrame(messages);
    }

    override createEncoder(): Encoder {
        return createBinaryEncoder();
    }

    override createDecoder(): Decoder {
        return new Decoder();
    }
}

// ============================================================
// Register all default formats
// ============================================================

// Text
RpcSerializationFormat.register(new RpcJsonSerializationFormat('json5'));
RpcSerializationFormat.register(new RpcJsonSerializationFormat('json5np'));
RpcSerializationFormat.register(new RpcJsonSerializationFormat('njson5'));
RpcSerializationFormat.register(new RpcJsonSerializationFormat('njson5np'));

// Binary (non-compact)
RpcSerializationFormat.register(
    new RpcMessagePackSerializationFormat('msgpack6')
);
RpcSerializationFormat.register(
    new RpcMessagePackSerializationFormat('mempack6')
);

// Binary (compact) — immutable, registry passed at call time
RpcSerializationFormat.register(
    new RpcMessagePackCompactSerializationFormat('msgpack6c')
);
RpcSerializationFormat.register(
    new RpcMessagePackCompactSerializationFormat('mempack6c')
);
