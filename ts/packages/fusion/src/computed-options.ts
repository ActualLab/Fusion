/**
 * Per-Computed configuration, carried by every {@link Computed} via its
 * {@link ComputeFunction} (compute methods) or its kind's static default.
 * The home for per-method knobs; the C# analog is `ActualLab.Fusion.ComputedOptions`.
 */
export class ComputedOptions {
    static default = new ComputedOptions();
    static mutableStateDefault = new ComputedOptions({
        errorAutoInvalidateDelay: Infinity,
    });

    readonly errorAutoInvalidateDelay: number;

    constructor(overrides?: Partial<ComputedOptions>) {
        this.errorAutoInvalidateDelay =
            overrides?.errorAutoInvalidateDelay ?? 1000;
    }
}
