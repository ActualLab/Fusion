// RPC serialization format definitions — mirrors .NET RpcSerializationFormat.
//
// Each format knows how to serialize/deserialize RPC messages on the wire.
// Consumers call format methods instead of checking isBinary/isCompact flags.

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
 * Each format provides serialize/deserialize handlers for the wire protocol.
 */
export abstract class RpcSerializationFormat {
    abstract readonly key: string;
    abstract readonly isBinary: boolean;

    /** Serialize an outbound message. */
    abstract serializeMessage(
        message: RpcMessage,
        args?: unknown[],
        encoder?: Encoder
    ): RpcWireData;

    /** Deserialize a single text message (only called for text formats). */
    abstract deserializeTextMessage(raw: string): RpcDeserializedMessage;

    /** Split + deserialize a binary frame (only called for binary formats). */
    abstract splitBinaryFrame(
        frame: Uint8Array,
        decoder?: Decoder
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
// Concrete formats
// ============================================================

/**
 * JSON text format — "json5np" (System.Text.Json, no polymorphism).
 * Wire: JSON envelope + delimiter-separated JSON args.
 */
export class RpcJsonSerializationFormat extends RpcSerializationFormat {
    readonly key: string;
    readonly isBinary = false;

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
 * Wire: binary envelope with LVar method name + MessagePack args.
 */
export class RpcMessagePackSerializationFormat extends RpcSerializationFormat {
    readonly key: string;
    readonly isBinary = true;

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
 * MessagePack compact binary format — "msgpack6c" / "mempack6c" (V5Compact wire format).
 * Wire: binary envelope with 4-byte method hash + MessagePack args.
 * Requires an RpcMethodRegistry for hash ↔ name resolution.
 */
export class RpcMessagePackCompactSerializationFormat extends RpcSerializationFormat {
    readonly key: string;
    readonly isBinary = true;
    readonly methodRegistry: RpcMethodRegistry;

    constructor(key: string, methodRegistry: RpcMethodRegistry) {
        super();
        this.key = key;
        this.methodRegistry = methodRegistry;
    }

    serializeMessage(
        message: RpcMessage,
        args?: unknown[],
        encoder?: Encoder
    ): Uint8Array {
        return serializeCompactBinaryMessage(
            message,
            args,
            this.methodRegistry,
            encoder
        );
    }

    deserializeTextMessage(raw: string): RpcDeserializedMessage {
        return deserializeMessage(raw);
    }

    splitBinaryFrame(
        frame: Uint8Array,
        decoder?: Decoder
    ): RpcDeserializedMessage[] {
        return splitCompactBinaryFrame(
            frame,
            this.methodRegistry,
            decoder
        );
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
// Register default formats
// ============================================================

// Text formats
RpcSerializationFormat.register(new RpcJsonSerializationFormat('json5'));
RpcSerializationFormat.register(new RpcJsonSerializationFormat('json5np'));
RpcSerializationFormat.register(new RpcJsonSerializationFormat('njson5'));
RpcSerializationFormat.register(new RpcJsonSerializationFormat('njson5np'));

// Binary formats (non-compact) — note: TS uses msgpack for both msgpack and mempack
// since both use the same V5 envelope; argument encoding is handled by @msgpack/msgpack
RpcSerializationFormat.register(
    new RpcMessagePackSerializationFormat('msgpack6')
);
RpcSerializationFormat.register(
    new RpcMessagePackSerializationFormat('mempack6')
);

// Compact formats are registered dynamically via registerCompactFormat()
// because they require an RpcMethodRegistry instance.

/**
 * Register compact formats with a method registry.
 * Called during hub initialization when compact format support is needed.
 */
export function registerCompactFormats(
    registry: RpcMethodRegistry
): void {
    RpcSerializationFormat.register(
        new RpcMessagePackCompactSerializationFormat('msgpack6c', registry)
    );
    RpcSerializationFormat.register(
        new RpcMessagePackCompactSerializationFormat('mempack6c', registry)
    );
}
