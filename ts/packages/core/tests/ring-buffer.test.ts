import { describe, it, expect } from 'vitest';
import { RingBuffer } from '../src/index.js';

describe('RingBuffer', () => {
    it('constructs with the requested capacity', () => {
        const rb = new RingBuffer<number>(4);
        expect(rb.capacity).toBe(4);
        expect(rb.count).toBe(0);
        expect(rb.isEmpty).toBe(true);
        expect(rb.isFull).toBe(false);
        expect(rb.hasRemainingCapacity).toBe(true);
        expect(rb.remainingCapacity).toBe(4);
    });

    it('rejects zero / negative capacity', () => {
        expect(() => new RingBuffer<number>(0)).toThrow();
        expect(() => new RingBuffer<number>(-1)).toThrow();
    });

    it('pushTail + get reads items in FIFO order', () => {
        const rb = new RingBuffer<number>(3);
        rb.pushTail(10);
        rb.pushTail(20);
        rb.pushTail(30);
        expect(rb.count).toBe(3);
        expect(rb.isFull).toBe(true);
        expect(rb.get(0)).toBe(10);
        expect(rb.get(1)).toBe(20);
        expect(rb.get(2)).toBe(30);
    });

    it('pushTail throws when full', () => {
        const rb = new RingBuffer<number>(2);
        rb.pushTail(1);
        rb.pushTail(2);
        expect(() => rb.pushTail(3)).toThrow();
    });

    it('pullHead returns items FIFO and updates count', () => {
        const rb = new RingBuffer<string>(4);
        rb.pushTail('a');
        rb.pushTail('b');
        rb.pushTail('c');

        expect(rb.pullHead()).toBe('a');
        expect(rb.count).toBe(2);
        expect(rb.pullHead()).toBe('b');
        expect(rb.pullHead()).toBe('c');
        expect(rb.count).toBe(0);
        expect(rb.isEmpty).toBe(true);
        expect(() => rb.pullHead()).toThrow();
    });

    it('moveHead advances past multiple items', () => {
        const rb = new RingBuffer<number>(5);
        for (let i = 0; i < 5; i++) rb.pushTail(i);
        rb.moveHead(2);
        expect(rb.count).toBe(3);
        expect(rb.get(0)).toBe(2);
        expect(rb.get(1)).toBe(3);
        expect(rb.get(2)).toBe(4);
    });

    it('moveHead(0) is a no-op', () => {
        const rb = new RingBuffer<number>(2);
        rb.pushTail(7);
        rb.moveHead(0);
        expect(rb.count).toBe(1);
        expect(rb.get(0)).toBe(7);
    });

    it('moveHead rejects out-of-range skipCount', () => {
        const rb = new RingBuffer<number>(3);
        rb.pushTail(1);
        expect(() => rb.moveHead(-1)).toThrow();
        expect(() => rb.moveHead(2)).toThrow();
    });

    it('wraps around the backing array correctly', () => {
        const rb = new RingBuffer<number>(3);
        rb.pushTail(1);
        rb.pushTail(2);
        rb.pushTail(3);
        expect(rb.pullHead()).toBe(1);  // head advances to slot 1
        expect(rb.pullHead()).toBe(2);  // head advances to slot 2
        rb.pushTail(4);                 // tail wraps to slot 0
        rb.pushTail(5);                 // tail advances to slot 1
        expect(rb.count).toBe(3);
        expect(rb.get(0)).toBe(3);
        expect(rb.get(1)).toBe(4);
        expect(rb.get(2)).toBe(5);
    });

    it('pushTailAndMoveHeadIfFull evicts the head when full', () => {
        const rb = new RingBuffer<number>(3);
        rb.pushTail(1);
        rb.pushTail(2);
        rb.pushTail(3);

        rb.pushTailAndMoveHeadIfFull(4);
        expect(rb.count).toBe(3);
        expect(rb.get(0)).toBe(2);
        expect(rb.get(1)).toBe(3);
        expect(rb.get(2)).toBe(4);

        rb.pushTailAndMoveHeadIfFull(5);
        expect(rb.toArray()).toEqual([3, 4, 5]);
    });

    it('pushTailAndMoveHeadIfFull behaves like pushTail when not full', () => {
        const rb = new RingBuffer<number>(3);
        rb.pushTailAndMoveHeadIfFull(10);
        rb.pushTailAndMoveHeadIfFull(20);
        expect(rb.toArray()).toEqual([10, 20]);
    });

    it('clear empties the buffer', () => {
        const rb = new RingBuffer<number>(4);
        rb.pushTail(1);
        rb.pushTail(2);
        rb.pushTail(3);
        rb.clear();
        expect(rb.count).toBe(0);
        expect(rb.isEmpty).toBe(true);
        expect(() => rb.pullHead()).toThrow();
    });

    it('get rejects out-of-range indexes', () => {
        const rb = new RingBuffer<number>(3);
        rb.pushTail(42);
        expect(() => rb.get(-1)).toThrow();
        expect(() => rb.get(1)).toThrow();
        expect(rb.get(0)).toBe(42);
    });

    it('toArray returns items in head-first order across wrap', () => {
        const rb = new RingBuffer<number>(4);
        rb.pushTail(1);
        rb.pushTail(2);
        rb.pushTail(3);
        rb.pullHead();
        rb.pullHead();
        rb.pushTail(4);
        rb.pushTail(5);
        rb.pushTail(6); // wraps
        expect(rb.toArray()).toEqual([3, 4, 5, 6]);
    });

    it('moveHead clears slots so references can be GC-ed', () => {
        const rb = new RingBuffer<{ id: number }>(3);
        const a = { id: 1 };
        const b = { id: 2 };
        rb.pushTail(a);
        rb.pushTail(b);
        rb.moveHead(1);
        expect(rb.count).toBe(1);
        expect(rb.get(0)).toBe(b);
        // @ts-expect-error — verifying internal slot is cleared
        expect(rb['_buffer'][0]).toBeUndefined();
    });
});
