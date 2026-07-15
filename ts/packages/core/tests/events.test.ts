import { describe, it, expect } from 'vitest';
import { EventHandlerSet } from '../src/index.js';

describe('EventHandlerSet', () => {
    it('should trigger all handlers', () => {
        const events = new EventHandlerSet<number>();
        const values: number[] = [];
        events.add(v => values.push(v));
        events.add(v => values.push(v * 10));
        events.trigger(3);
        expect(values).toEqual([3, 30]);
    });

    it('should remove handlers', () => {
        const events = new EventHandlerSet<number>();
        const values: number[] = [];
        const handler = (v: number) => values.push(v);
        events.add(handler);
        events.trigger(1);
        events.remove(handler);
        events.trigger(2);
        expect(values).toEqual([1]);
    });

    it('should track count', () => {
        const events = new EventHandlerSet<void>();
        expect(events.count).toBe(0);
        const handler = () => { /* noop */ };
        events.add(handler);
        expect(events.count).toBe(1);
        events.remove(handler);
        expect(events.count).toBe(0);
    });

    it('whenNext should resolve on next trigger', async () => {
        const events = new EventHandlerSet<string>();
        const promise = events.whenNext();
        events.trigger('hello');
        expect(await promise).toBe('hello');
    });

    it('whenNext should auto-remove handler after firing', async () => {
        const events = new EventHandlerSet<string>();
        const promise = events.whenNext();
        expect(events.count).toBe(1);
        events.trigger('hello');
        await promise;
        expect(events.count).toBe(0);
    });

    it('a handler added during dispatch does not run in the same dispatch (C4)', () => {
        const events = new EventHandlerSet<number>();
        const calls: string[] = [];
        events.add(() => {
            calls.push('a');
            events.add(() => calls.push('b'));
        });
        events.trigger(1);
        expect(calls).toEqual(['a']);
    });

    it('a handler removed during dispatch still runs in the same dispatch (C4)', () => {
        const events = new EventHandlerSet<number>();
        const calls: string[] = [];
        const b = () => calls.push('b');
        events.add(() => {
            calls.push('a');
            events.remove(b);
        });
        events.add(b);
        events.trigger(1);
        expect(calls).toEqual(['a', 'b']);

        events.trigger(2);
        expect(calls).toEqual(['a', 'b', 'a']);
    });

    it('whenNext() from inside a handler resolves with the NEXT event (C4)', async () => {
        const events = new EventHandlerSet<number>();
        let seen: number | undefined;
        events.add(() => {
            void events.whenNext().then(v => { seen = v; });
        });

        events.trigger(1);
        await new Promise(r => setTimeout(r, 10));
        expect(seen).toBeUndefined();

        events.trigger(2);
        await new Promise(r => setTimeout(r, 10));
        expect(seen).toBe(2);
    });
});
