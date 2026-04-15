import { describe, it, expect, beforeEach } from 'vitest';
import {
    RpcSerializationFormat,
    RpcJsonSerializationFormat,
    RpcMessagePackSerializationFormat,
    RpcMessagePackCompactSerializationFormat,
    RpcMethodRegistry,
} from '../src/index.js';

describe('RpcSerializationFormat registry', () => {
    it('should resolve json5np format', () => {
        const fmt = RpcSerializationFormat.get('json5np');
        expect(fmt).toBeInstanceOf(RpcJsonSerializationFormat);
        expect(fmt.isBinary).toBe(false);
        expect(fmt.key).toBe('json5np');
    });

    it('should resolve json5 format', () => {
        const fmt = RpcSerializationFormat.get('json5');
        expect(fmt).toBeInstanceOf(RpcJsonSerializationFormat);
        expect(fmt.isBinary).toBe(false);
    });

    it('should resolve msgpack6 format', () => {
        const fmt = RpcSerializationFormat.get('msgpack6');
        expect(fmt).toBeInstanceOf(RpcMessagePackSerializationFormat);
        expect(fmt.isBinary).toBe(true);
    });

    it('should resolve mempack6 format', () => {
        const fmt = RpcSerializationFormat.get('mempack6');
        expect(fmt).toBeInstanceOf(RpcMessagePackSerializationFormat);
        expect(fmt.isBinary).toBe(true);
    });

    it('should throw for unknown format', () => {
        expect(() => RpcSerializationFormat.get('unknown')).toThrow(
            /unknown/i
        );
    });

    it('should return undefined from tryGet for unknown format', () => {
        expect(RpcSerializationFormat.tryGet('unknown')).toBeUndefined();
    });
});

describe('RpcJsonSerializationFormat', () => {
    const fmt = RpcSerializationFormat.get('json5np');

    it('should serialize message as string', () => {
        const wire = fmt.serializeMessage(
            { Method: 'Svc.get', RelatedId: 1 },
            ['hello', 42]
        );
        expect(typeof wire).toBe('string');
        expect(wire as string).toContain('"Method":"Svc.get"');
        expect(wire as string).toContain('"hello"');
    });

    it('should round-trip via serializeMessage + deserializeTextMessage', () => {
        const wire = fmt.serializeMessage(
            { Method: 'Svc.get', RelatedId: 5 },
            ['hello', 42]
        );
        const { message, args } = fmt.deserializeTextMessage(wire as string);
        expect(message.Method).toBe('Svc.get');
        expect(message.RelatedId).toBe(5);
        expect(args).toEqual(['hello', 42]);
    });

    it('should throw on binary frame operations', () => {
        expect(() => fmt.splitBinaryFrame(new Uint8Array(0))).toThrow();
        expect(() => fmt.serializeBinaryFrame([])).toThrow();
    });

    it('should return undefined for createEncoder/createDecoder', () => {
        expect(fmt.createEncoder()).toBeUndefined();
        expect(fmt.createDecoder()).toBeUndefined();
    });
});

describe('RpcMessagePackSerializationFormat', () => {
    const fmt = RpcSerializationFormat.get('msgpack6');

    it('should serialize message as Uint8Array', () => {
        const wire = fmt.serializeMessage(
            { Method: 'Svc.get', RelatedId: 1 },
            ['hello']
        );
        expect(wire).toBeInstanceOf(Uint8Array);
    });

    it('should round-trip via serializeMessage + splitBinaryFrame', () => {
        const wire = fmt.serializeMessage(
            { Method: 'Svc.get', RelatedId: 5 },
            ['hello', 42]
        );
        const results = fmt.splitBinaryFrame(wire as Uint8Array);
        expect(results).toHaveLength(1);
        expect(results[0].message.Method).toBe('Svc.get');
        expect(results[0].message.RelatedId).toBe(5);
        expect(results[0].args).toEqual(['hello', 42]);
    });

    it('should handle multiple messages in a frame', () => {
        const msg1 = fmt.serializeMessage(
            { Method: 'a', RelatedId: 1 },
            [10]
        ) as Uint8Array;
        const msg2 = fmt.serializeMessage(
            { Method: 'b', RelatedId: 2 },
            ['hi']
        ) as Uint8Array;
        const frame = fmt.serializeBinaryFrame([msg1, msg2]);
        const results = fmt.splitBinaryFrame(frame);
        expect(results).toHaveLength(2);
        expect(results[0].message.Method).toBe('a');
        expect(results[1].message.Method).toBe('b');
    });

    it('should create encoder and decoder', () => {
        expect(fmt.createEncoder()).toBeDefined();
        expect(fmt.createDecoder()).toBeDefined();
    });
});

describe('RpcMessagePackCompactSerializationFormat', () => {
    let registry: RpcMethodRegistry;
    const fmt = RpcSerializationFormat.get(
        'msgpack6c'
    ) as RpcMessagePackCompactSerializationFormat;

    beforeEach(() => {
        registry = new RpcMethodRegistry();
    });

    it('should serialize using method hash instead of name', () => {
        registry.register('Svc.get:2');
        const wire = fmt.serializeMessage(
            { Method: 'Svc.get:2', RelatedId: 1 },
            ['hello'],
            undefined,
            registry
        );

        // Should be shorter than non-compact (4-byte hash vs LVar "Svc.get:2")
        const nonCompactFmt = RpcSerializationFormat.get('msgpack6');
        const nonCompactWire = nonCompactFmt.serializeMessage(
            { Method: 'Svc.get:2', RelatedId: 1 },
            ['hello']
        ) as Uint8Array;
        expect(wire.length).toBeLessThan(nonCompactWire.length);
    });

    it('should round-trip when method is registered', () => {
        registry.register('Svc.get:2');
        const wire = fmt.serializeMessage(
            { Method: 'Svc.get:2', RelatedId: 5 },
            ['hello', 42],
            undefined,
            registry
        );
        const results = fmt.splitBinaryFrame(wire, undefined, registry);
        expect(results).toHaveLength(1);
        expect(results[0].message.Method).toBe('Svc.get:2');
        expect(results[0].message.RelatedId).toBe(5);
        expect(results[0].args).toEqual(['hello', 42]);
    });

    it('should use hex hash placeholder for unregistered methods on deserialize', () => {
        registry.register('Svc.get:2');
        const wire = fmt.serializeMessage(
            { Method: 'Svc.get:2', RelatedId: 1 },
            ['x'],
            undefined,
            registry
        );

        // Deserialize with a fresh registry that doesn't know the method
        const freshRegistry = new RpcMethodRegistry();
        const results = fmt.splitBinaryFrame(
            wire,
            undefined,
            freshRegistry
        );
        expect(results).toHaveLength(1);
        expect(results[0].message.Method).toMatch(/^<hash:0x[0-9a-f]+>$/);
    });

    it('should compute hashes on demand via getHash', () => {
        const h1 = registry.getHash('Svc.get:2');
        const h2 = registry.getHash('Svc.get:2');
        expect(h1).toBe(h2); // cached
        expect(registry.getName(h1)).toBe('Svc.get:2');
    });
});

describe('RpcMethodRegistry', () => {
    it('should register and resolve method names', () => {
        const registry = new RpcMethodRegistry();
        registry.register('Svc.get:2');
        const hash = registry.getHash('Svc.get:2');
        expect(registry.getName(hash)).toBe('Svc.get:2');
    });

    it('should register all methods from a service definition', () => {
        const registry = new RpcMethodRegistry();
        const methods = new Map([
            ['get:2', { name: 'get', wireArgCount: 2 }],
            ['set:3', { name: 'set', wireArgCount: 3 }],
        ]);
        registry.registerService('MySvc', methods);
        expect(registry.getName(registry.getHash('MySvc.get:2'))).toBe(
            'MySvc.get:2'
        );
        expect(registry.getName(registry.getHash('MySvc.set:3'))).toBe(
            'MySvc.set:3'
        );
    });

    it('should compute hash on demand for unregistered methods', () => {
        const registry = new RpcMethodRegistry();
        // getHash auto-registers
        const hash = registry.getHash('NewMethod:1');
        expect(registry.getName(hash)).toBe('NewMethod:1');
    });

    it('should return undefined for unknown hash', () => {
        const registry = new RpcMethodRegistry();
        expect(registry.getName(999999)).toBeUndefined();
    });
});
