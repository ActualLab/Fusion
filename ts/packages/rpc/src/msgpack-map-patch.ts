import { Encoder } from '@msgpack/msgpack';

/**
 * Patches `@msgpack/msgpack`'s `Encoder` so JS `Map` instances are encoded
 * as proper msgpack `map` types with typed keys (matching .NET's
 * `Dictionary<K, V>` wire format). Without this patch the encoder treats
 * `Map` as a plain object — since `Object.keys(new Map())` returns `[]`,
 * any `Map` is silently serialized as an empty `{}`.
 *
 * Idempotent: the patch is applied at most once per process.
 *
 * This is needed by the `$sys.Reconnect:3` protocol to send
 * `Dictionary<int, byte[]>` over msgpack in a way that a .NET server can
 * deserialize. The JSON side is naturally compatible (keys become strings
 * in JSON regardless) — this patch fixes the binary path only.
 */
/* eslint-disable @typescript-eslint/no-explicit-any,
                  @typescript-eslint/no-unsafe-member-access,
                  @typescript-eslint/no-unsafe-call,
                  @typescript-eslint/no-unsafe-assignment
   -- the entire patch reaches into private Encoder internals to make Map
      encoding work; the type annotations on `@msgpack/msgpack` don't expose
      these methods so all access is necessarily untyped. */
export function patchMsgpackEncoderForMaps(): void {
    const proto = Encoder.prototype as any;
    if (proto._hasMapPatch) return;
    proto._hasMapPatch = true;

    const originalDoEncode = proto.doEncode as (object: unknown, depth: number) => void;

    proto.doEncode = function (this: unknown, object: unknown, depth: number): void {
        if (object instanceof Map) {
            _encodeMapInstance.call(this, object, depth);
            return;
        }
        originalDoEncode.call(this, object, depth);
    };

    function _encodeMapInstance(this: unknown, m: Map<unknown, unknown>, depth: number): void {
        const self = this as any;
        const size = m.size;
        if (size < 16) {
            self.writeU8(0x80 + size);
        } else if (size < 0x10000) {
            self.writeU8(0xde);
            self.writeU16(size);
        } else {
            self.writeU8(0xdf);
            self.writeU32(size);
        }
        for (const [key, value] of m) {
            // Recurse via the (now patched) doEncode so nested Maps work too.
            self.doEncode(key, depth + 1);
            self.doEncode(value, depth + 1);
        }
    }
}
/* eslint-enable @typescript-eslint/no-explicit-any,
                 @typescript-eslint/no-unsafe-member-access,
                 @typescript-eslint/no-unsafe-call,
                 @typescript-eslint/no-unsafe-assignment */

// Apply at module load — idempotent.
patchMsgpackEncoderForMaps();
