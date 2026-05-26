/**
 * Pre-resolved promises for the three values most often returned as a
 * "no-op" successful path: `void`, `true`, `false`. Returning these
 * avoids allocating a fresh promise per call site.
 *
 * Idiomatic use:
 * ```ts
 * if (alreadyInitialized) return ResolvedPromise.Void;
 * ```
 */
export const ResolvedPromise = {
    Void: Promise.resolve(),
    True: Promise.resolve(true),
    False: Promise.resolve(false),
} as const;
