// Helpers shared by the stage-3 decorators of @actuallab/rpc and @actuallab/fusion.

// Returns the record stored under `key` in `metadata`, guaranteeing it is an OWN
// property. Stage-3 decorator metadata of a derived class inherits from the base
// class's metadata via the prototype chain, so writing through an inherited record
// would silently mutate the base contract. When the record is inherited (or missing)
// it is shallow-cloned into an own property first — base entries stay visible on the
// derived contract while derived writes stay local to it.
// eslint-disable-next-line @typescript-eslint/no-unnecessary-type-parameters -- callers pick the record shape
export function ownMetadata<T extends object>(metadata: object, key: symbol): T {
    const bag = metadata as Record<symbol, T>;
    if (!Object.hasOwn(metadata, key))
        bag[key] = { ...bag[key] };

    return bag[key];
}

// Resolves the fixed argument count of a decorated method for the RPC wire name.
// Function.length silently stops at the first default parameter and ignores rest
// parameters, so an inferred count would be wrong; when the parameter list is
// ambiguous the caller must pass an explicit `argCount` or this throws at declaration
// time (the erased count cannot be recovered reliably at runtime).
export function resolveArgCount(
    fn: (...args: never[]) => unknown,
    explicit: number | undefined,
    methodName: string,
): number {
    if (explicit !== undefined)
        return explicit;
    if (hasAmbiguousArity(fn))
        throw new Error(
            `Cannot infer the argument count of "${methodName}": its parameter list uses ` +
            `default and/or rest parameters, for which Function.length is unreliable. ` +
            `Pass an explicit argCount to the decorator.`,
        );

    return fn.length;
}

// Private methods

function hasAmbiguousArity(fn: (...args: never[]) => unknown): boolean {
    const params = parameterListText(fn);
    return params !== undefined && (params.includes('=') || params.includes('...'));
}

function parameterListText(fn: (...args: never[]) => unknown): string | undefined {
    const src = Function.prototype.toString.call(fn);
    const open = src.indexOf('(');
    const arrow = src.indexOf('=>');
    // A parenless single-identifier arrow (`x => ...`) has no default/rest parameters.
    if (open < 0 || (arrow >= 0 && arrow < open))
        return undefined;

    let depth = 0;
    for (let i = open; i < src.length; i++) {
        const ch = src[i];
        if (ch === '(')
            depth++;
        else if (ch === ')') {
            depth--;
            if (depth === 0)
                return src.slice(open + 1, i);
        }
    }
    return undefined;
}
