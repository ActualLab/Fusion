/**
 * A fixed-capacity circular buffer supporting efficient push/pull at both
 * head and tail.
 *
 * Direct port of .NET `ActualLab.Collections.RingBuffer<T>` at
 * src/ActualLab.Core/Collections/RingBuffer.cs (simplified — TS uses plain
 * modulo instead of the power-of-two-minus-one bit-mask trick, since the
 * performance difference is negligible in JavaScript).
 */
export class RingBuffer<T> {
    private _buffer: (T | undefined)[];
    private _start = 0;
    private _end = 0;
    private _count = 0;

    readonly capacity: number;

    get count(): number { return this._count; }
    get isEmpty(): boolean { return this._count === 0; }
    get isFull(): boolean { return this._count === this.capacity; }
    get remainingCapacity(): number { return this.capacity - this._count; }
    get hasRemainingCapacity(): boolean { return this._count < this.capacity; }

    constructor(capacity: number) {
        if (capacity < 1)
            throw new RangeError(`RingBuffer capacity must be >= 1 (was ${capacity}).`);
        this.capacity = capacity;
        this._buffer = new Array<T | undefined>(capacity);
    }

    /** Read the item at `index` (0-indexed, from head). */
    get(index: number): T {
        if (index < 0 || index >= this._count)
            throw new RangeError(`Index ${index} out of range [0, ${this._count}).`);
        return this._buffer[(this._start + index) % this.capacity] as T;
    }

    /** Add an item at the tail. Throws if the buffer is full. */
    pushTail(item: T): void {
        if (this._count >= this.capacity)
            throw new Error('RingBuffer is full.');
        this._buffer[this._end] = item;
        this._end = (this._end + 1) % this.capacity;
        this._count++;
    }

    /** Add an item at the tail, evicting the head item if the buffer is full. */
    pushTailAndMoveHeadIfFull(item: T): void {
        if (this._count >= this.capacity) {
            this._buffer[this._start] = undefined;
            this._start = (this._start + 1) % this.capacity;
            this._count--;
        }
        this.pushTail(item);
    }

    /** Remove the head item. Throws if the buffer is empty. */
    pullHead(): T {
        if (this._count === 0)
            throw new Error('RingBuffer is empty.');
        const head = this._buffer[this._start] as T;
        this._buffer[this._start] = undefined;
        this._start = (this._start + 1) % this.capacity;
        this._count--;
        return head;
    }

    /**
     * Advance the head by `skipCount` slots, releasing references to skipped
     * items so they can be garbage-collected.
     */
    moveHead(skipCount: number): void {
        if (skipCount === 0) return;
        if (skipCount < 0 || skipCount > this._count)
            throw new RangeError(`skipCount ${skipCount} out of range [0, ${this._count}].`);
        for (let k = 0; k < skipCount; k++) {
            this._buffer[(this._start + k) % this.capacity] = undefined;
        }
        this._start = (this._start + skipCount) % this.capacity;
        this._count -= skipCount;
    }

    /** Remove all items. */
    clear(): void {
        for (let i = 0; i < this.capacity; i++) this._buffer[i] = undefined;
        this._start = 0;
        this._end = 0;
        this._count = 0;
    }

    /** Copy the buffer's contents to a new array, head-first. */
    toArray(): T[] {
        const out = new Array<T>(this._count);
        for (let i = 0; i < this._count; i++) {
            out[i] = this._buffer[(this._start + i) % this.capacity] as T;
        }
        return out;
    }
}
