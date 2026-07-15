// @vitest-environment jsdom
// Regression tests for docs/plans/ts-port-audit.md items S6 (lost update between
// render and subscription) and S7 (StrictMode double-mount freeze / leaked ComputedState).
import { describe, it, expect, beforeEach } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { StrictMode, createElement, type ReactNode } from 'react';
import { AsyncContext, Result } from '@actuallab/core';
import {
    type ComputedState,
    FixedDelayer,
    MutableState,
} from '@actuallab/fusion';
import { useComputedState, useMutableState } from '../src/index.js';

function tick(ms = 0): Promise<void> {
    return new Promise(r => setTimeout(r, ms));
}

const strictWrapper = ({ children }: { children: ReactNode }) =>
    createElement(StrictMode, null, children);

describe('useSyncExternalStore subscription contract (S6)', () => {
    beforeEach(() => {
        AsyncContext.current = undefined;
    });

    it('an update landing between the render snapshot and subscription is not lost', async () => {
        const state = new MutableState(0);
        // Snapshot captured during render, before the effect subscribes.
        const sinceIndex = state.updateIndex;
        // Update lands in the render→subscribe gap.
        state.set(1);

        // The versioned wait resolves immediately for the missed generation,
        // so the subscription observes value 1 rather than hanging on the next set.
        await expect(state.whenUpdated(sinceIndex)).resolves.toBeUndefined();
        expect(state.valueOrUndefined).toBe(1);
    });
});

describe('useMutableState', () => {
    beforeEach(() => {
        AsyncContext.current = undefined;
    });

    it('renders the initial value and reacts to set (S6)', async () => {
        const { result, unmount } = renderHook(() => useMutableState(0), {
            wrapper: strictWrapper,
        });
        try {
            expect(result.current.value).toBe(0);
            expect(result.current.error).toBeUndefined();

            act(() => result.current.set(5));
            await waitFor(() => expect(result.current.value).toBe(5));
        } finally {
            unmount();
        }
    });

    it('exposes an error result via `error`, value stays undefined (S17 shape)', async () => {
        const { result, unmount } = renderHook(() => useMutableState(1), {
            wrapper: strictWrapper,
        });
        try {
            const boom = new Error('boom');
            act(() => result.current.set(Result.error<number>(boom)));
            await waitFor(() => expect(result.current.error).toBe(boom));
            expect(result.current.value).toBeUndefined();
        } finally {
            unmount();
        }
    });

    it('keeps a single MutableState across StrictMode double-render', () => {
        const { result, unmount } = renderHook(() => useMutableState(7), {
            wrapper: strictWrapper,
        });
        try {
            expect(result.current.state).toBeInstanceOf(MutableState);
            expect(result.current.value).toBe(7);
        } finally {
            unmount();
        }
    });
});

describe('useComputedState (S7)', () => {
    beforeEach(() => {
        AsyncContext.current = undefined;
    });

    it('StrictMode double-mount keeps the value live, not frozen', async () => {
        const source = new MutableState(1);
        const { result, unmount } = renderHook(
            () =>
                useComputedState(() => source.use() * 10, [], {
                    updateDelayer: FixedDelayer.zero,
                }),
            { wrapper: strictWrapper }
        );
        try {
            await waitFor(() => expect(result.current.value).toBe(10));
            expect(result.current.isInitial).toBe(false);
            expect(result.current.state?.isDisposed).toBe(false);

            // Frozen-state regression: a source change after the StrictMode
            // remount must still flow through to a re-render.
            act(() => source.set(3));
            await waitFor(() => expect(result.current.value).toBe(30));
        } finally {
            unmount();
        }
    });

    it('disposes the live state on unmount and leaks no running ComputedState', async () => {
        const source = new MutableState(1);
        let computeCount = 0;
        const { result, unmount } = renderHook(
            () =>
                useComputedState(
                    () => {
                        computeCount++;
                        return source.use() * 10;
                    },
                    [],
                    { updateDelayer: FixedDelayer.zero }
                ),
            { wrapper: strictWrapper }
        );

        await waitFor(() => expect(result.current.value).toBe(10));
        const liveState = result.current.state;
        expect(liveState?.isDisposed).toBe(false);

        unmount();
        expect(liveState?.isDisposed).toBe(true);

        // A leaked live ComputedState would recompute on this invalidation.
        const countAfterUnmount = computeCount;
        source.set(2);
        await tick(30);
        expect(computeCount).toBe(countAfterUnmount);
    });

    it('a deps change renders the fresh state even at the same updateIndex', async () => {
        // A sync computer reaches updateIndex 1 inside the constructor, so the
        // replacement state resubscribes at the exact index the old one rendered
        // with — an index-only snapshot would collide and freeze the old value.
        const { result, rerender, unmount } = renderHook(
            ({ dep }: { dep: number }) =>
                useComputedState(() => dep * 10, [dep], {
                    updateDelayer: FixedDelayer.zero,
                }),
            { wrapper: strictWrapper, initialProps: { dep: 1 } }
        );
        try {
            await waitFor(() => expect(result.current.value).toBe(10));

            rerender({ dep: 2 });
            await waitFor(() => expect(result.current.value).toBe(20));
        } finally {
            unmount();
        }
    });

    it('creates the ComputedState only in the effect (first render has no state)', () => {
        const renderStates: (ComputedState<number> | undefined)[] = [];
        const { unmount } = renderHook(
            () => {
                const r = useComputedState(() => 1, [], {
                    updateDelayer: FixedDelayer.zero,
                });
                renderStates.push(r.state);
                return r;
            },
            { wrapper: strictWrapper }
        );
        try {
            // The first render runs before the subscribe effect, so it must
            // observe no state — render never creates (or disposes) one.
            expect(renderStates[0]).toBeUndefined();
        } finally {
            unmount();
        }
    });
});
