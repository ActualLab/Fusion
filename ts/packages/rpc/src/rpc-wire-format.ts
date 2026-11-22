// RPC wire format definitions — mirrors .NET RpcWireFormat.
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
 * Base class for RPC wire formats.
 * Instances are immutable singletons — one per format key.
 * State (like method registries) is passed via parameters.
 */
export abstract class RpcWireFormat {
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

    // Named format instances — initialized in initFormats() below (after subclasses are defined)
    static SystemJsonV5: RpcWireFormat;
    static SystemJsonV5NP: RpcWireFormat;
    static NewtonsoftJsonV5: RpcWireFormat;
    static NewtonsoftJsonV5NP: RpcWireFormat;
    static MessagePackV6: RpcWireFormat;
    static MessagePackV6C: RpcWireFormat;
    static All: readonly RpcWireFormat[];

    /** Convenience shortcut for RpcWireFormatResolver.Default.get(key). */
    static get(key: string): RpcWireFormat {
        return RpcWireFormatResolver.Default.get(key);
    }

    /** Convenience shortcut for RpcWireFormatResolver.Default.tryGet(key). */
    static tryGet(key: string): RpcWireFormat | undefined {
        return RpcWireFormatResolver.Default.tryGet(key);
    }
}

// ============================================================
// Concrete formats (immutable singletons)
// ============================================================

/**
 * JSON text format — "json5np" (System.Text.Json, no polymorphism).
 */
export class RpcJsonWireFormat extends RpcWireFormat {
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
 * MessagePack binary format — "msgpack6" (V5 wire format).
 */
export class RpcMessagePackWireFormat extends RpcWireFormat {
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
 * MessagePack compact binary format — "msgpack6c" (V5Compact).
 * Uses 4-byte method hash instead of full method name.
 * Registry is passed via parameters — format instance is immutable.
 */
export class RpcMessagePackCompactWireFormat extends RpcWireFormat {
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
// Initialize named format instances (after subclasses are defined)
// ============================================================

// Text: System.Text.Json
RpcWireFormat.SystemJsonV5 = new RpcJsonWireFormat('json5');
RpcWireFormat.SystemJsonV5NP = new RpcJsonWireFormat('json5np');
// Text: Newtonsoft.Json
RpcWireFormat.NewtonsoftJsonV5 = new RpcJsonWireFormat('njson5');
RpcWireFormat.NewtonsoftJsonV5NP = new RpcJsonWireFormat('njson5np');
// Binary: MessagePack (non-compact)
RpcWireFormat.MessagePackV6 = new RpcMessagePackWireFormat('msgpack6');
// Binary: MessagePack (compact)
RpcWireFormat.MessagePackV6C = new RpcMessagePackCompactWireFormat('msgpack6c');

// The mempack6* formats are intentionally NOT registered: .NET's mempack6 uses
// MemoryPack argument serialization (a different byte format), which the TS
// client does not implement. Resolving one throws via isMemoryPackKey below.
RpcWireFormat.All = [
    RpcWireFormat.SystemJsonV5,
    RpcWireFormat.SystemJsonV5NP,
    RpcWireFormat.NewtonsoftJsonV5,
    RpcWireFormat.NewtonsoftJsonV5NP,
    RpcWireFormat.MessagePackV6,
    RpcWireFormat.MessagePackV6C,
];

// ============================================================
// RpcWireFormatResolver — mirrors .NET RpcWireFormatResolver
// ============================================================

/**
 * Resolves RpcWireFormat instances by key, with a configurable default.
 * Similar to .NET RpcWireFormatResolver, which takes its formats from RpcConfiguration.WireFormats.
 */
export class RpcWireFormatResolver {
    static DefaultFormats: readonly RpcWireFormat[] = RpcWireFormat.All;
    static Default = new RpcWireFormatResolver('json5np');

    readonly defaultFormatKey: string;
    readonly formats: ReadonlyMap<string, RpcWireFormat>;

    get defaultFormat(): RpcWireFormat {
        return this.get(this.defaultFormatKey);
    }

    constructor(defaultFormatKey: string, formats?: Iterable<RpcWireFormat>) {
        const map = new Map<string, RpcWireFormat>();
        for (const f of formats ?? RpcWireFormatResolver.DefaultFormats)
            map.set(f.key, f);

        if (!map.has(defaultFormatKey))
            throw new Error(`No format with key '${defaultFormatKey}'.`);

        this.defaultFormatKey = defaultFormatKey;
        this.formats = map;
    }

    get(key: string): RpcWireFormat {
        if (isMemoryPackKey(key))
            throw new Error(`Wire format '${key}': MemoryPack formats are not supported by the TS client.`);

        const wireFormat = this.tryGet(key);
        if (!wireFormat)
            throw new Error(`No format with key '${key}'.`);
        return wireFormat;
    }

    tryGet(key: string): RpcWireFormat | undefined {
        if (!key) key = this.defaultFormatKey;
        if (isMemoryPackKey(key))
            throw new Error(`Wire format '${key}': MemoryPack formats are not supported by the TS client.`);
        return this.formats.get(key);
    }
}

/** True for .NET MemoryPack format keys (mempack5, mempack6, mempack6c, …). */
function isMemoryPackKey(key: string): boolean {
    return key.startsWith('mempack');
}
