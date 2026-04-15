import { describe, it, expect } from 'vitest';
import {
    serializeBinaryMessage,
    deserializeBinaryMessage,
    splitBinaryFrame,
    serializeBinaryFrame,
    createBinaryEncoder,
} from '../src/index.js';

describe('RPC Binary Serialization', () => {
    describe('serializeBinaryMessage / deserializeBinaryMessage round-trip', () => {
        it('should round-trip message with no args', () => {
            const original = { Method: '$sys.KeepAlive' };
            const binary = serializeBinaryMessage(original);
            const { message, args } = deserializeBinaryMessage(binary, 0);

            expect(message.Method).toBe('$sys.KeepAlive');
            expect(args).toEqual([]);
        });

        it('should round-trip message with number arg', () => {
            const binary = serializeBinaryMessage(
                { Method: 'Svc.get', RelatedId: 1 },
                [42]
            );
            const { message, args } = deserializeBinaryMessage(binary, 0);

            expect(message.Method).toBe('Svc.get');
            expect(message.RelatedId).toBe(1);
            expect(args).toEqual([42]);
        });

        it('should round-trip message with string arg', () => {
            const binary = serializeBinaryMessage(
                { Method: 'Svc.echo', RelatedId: 2 },
                ['hello world']
            );
            const { message, args } = deserializeBinaryMessage(binary, 0);

            expect(message.Method).toBe('Svc.echo');
            expect(args).toEqual(['hello world']);
        });

        it('should round-trip message with object arg', () => {
            const obj = { nested: true, count: 7, name: 'test' };
            const binary = serializeBinaryMessage(
                { Method: 'Svc.save', RelatedId: 3 },
                [obj]
            );
            const { message, args } = deserializeBinaryMessage(binary, 0);

            expect(message.Method).toBe('Svc.save');
            expect(args).toEqual([obj]);
        });

        it('should round-trip message with array arg', () => {
            const arr = [1, 'two', 3];
            const binary = serializeBinaryMessage(
                { Method: 'Svc.batch', RelatedId: 4 },
                [arr]
            );
            const { message, args } = deserializeBinaryMessage(binary, 0);

            expect(message.Method).toBe('Svc.batch');
            expect(args).toEqual([arr]);
        });

        it('should round-trip message with null arg', () => {
            const binary = serializeBinaryMessage(
                { Method: 'Svc.clear', RelatedId: 5 },
                [null]
            );
            const { message, args } = deserializeBinaryMessage(binary, 0);

            expect(message.Method).toBe('Svc.clear');
            expect(args).toEqual([null]);
        });

        it('should round-trip message with boolean arg', () => {
            const binary = serializeBinaryMessage(
                { Method: 'Svc.toggle', RelatedId: 6 },
                [true, false]
            );
            const { message, args } = deserializeBinaryMessage(binary, 0);

            expect(message.Method).toBe('Svc.toggle');
            expect(args).toEqual([true, false]);
        });

        it('should round-trip message with mixed args', () => {
            const mixedArgs = [42, 'hello', { nested: true }, [1, 2], null, false];
            const binary = serializeBinaryMessage(
                { Method: 'Svc.mixed', RelatedId: 7 },
                mixedArgs
            );
            const { message, args } = deserializeBinaryMessage(binary, 0);

            expect(message.Method).toBe('Svc.mixed');
            expect(message.RelatedId).toBe(7);
            expect(args).toEqual(mixedArgs);
        });

        it('should round-trip message with CallType set', () => {
            const binary = serializeBinaryMessage(
                { Method: 'Svc.call', RelatedId: 10, CallType: 3 },
                ['arg1']
            );
            const { message, args } = deserializeBinaryMessage(binary, 0);

            expect(message.Method).toBe('Svc.call');
            expect(message.RelatedId).toBe(10);
            expect(message.CallType).toBe(3);
            expect(args).toEqual(['arg1']);
        });

        it('should round-trip message with large RelatedId (VarUint encoding)', () => {
            const largeId = 1_000_000;
            const binary = serializeBinaryMessage(
                { Method: 'Svc.get', RelatedId: largeId },
                ['data']
            );
            const { message, args } = deserializeBinaryMessage(binary, 0);

            expect(message.Method).toBe('Svc.get');
            expect(message.RelatedId).toBe(largeId);
            expect(args).toEqual(['data']);
        });

        it('should round-trip message with very large RelatedId', () => {
            const veryLargeId = 0x0fff_ffff; // 28 bits, requires 4 VarUint bytes
            const binary = serializeBinaryMessage(
                { Method: 'Svc.big', RelatedId: veryLargeId }
            );
            const { message } = deserializeBinaryMessage(binary, 0);

            expect(message.RelatedId).toBe(veryLargeId);
        });
    });

    describe('splitBinaryFrame / serializeBinaryFrame', () => {
        it('should split multiple messages from a single frame', () => {
            const msgA = serializeBinaryMessage({ Method: 'a', RelatedId: 1 });
            const msgB = serializeBinaryMessage(
                { Method: 'b', RelatedId: 2 },
                ['hello']
            );
            const msgC = serializeBinaryMessage(
                { Method: 'c', RelatedId: 3 },
                [42, true]
            );

            const frame = serializeBinaryFrame([msgA, msgB, msgC]);
            const results = splitBinaryFrame(frame);

            expect(results.length).toBe(3);
            expect(results[0]!.message.Method).toBe('a');
            expect(results[0]!.args).toEqual([]);
            expect(results[1]!.message.Method).toBe('b');
            expect(results[1]!.args).toEqual(['hello']);
            expect(results[2]!.message.Method).toBe('c');
            expect(results[2]!.args).toEqual([42, true]);
        });

        it('should handle a single message frame', () => {
            const msg = serializeBinaryMessage(
                { Method: 'single', RelatedId: 99 },
                ['only']
            );
            const frame = serializeBinaryFrame([msg]);
            const results = splitBinaryFrame(frame);

            expect(results.length).toBe(1);
            expect(results[0]!.message.Method).toBe('single');
            expect(results[0]!.message.RelatedId).toBe(99);
            expect(results[0]!.args).toEqual(['only']);
        });

        it('should handle an empty frame', () => {
            const frame = new Uint8Array(0);
            const results = splitBinaryFrame(frame);

            expect(results.length).toBe(0);
        });
    });

    describe('createBinaryEncoder', () => {
        it('should return a working Encoder for serializeBinaryMessage', () => {
            const encoder = createBinaryEncoder();
            const binary = serializeBinaryMessage(
                { Method: 'Svc.custom', RelatedId: 42 },
                ['test', 123, { key: 'value' }],
                encoder
            );
            const { message, args } = deserializeBinaryMessage(binary, 0);

            expect(message.Method).toBe('Svc.custom');
            expect(message.RelatedId).toBe(42);
            expect(args).toEqual(['test', 123, { key: 'value' }]);
        });
    });
});
