import { describe, it, expect } from 'vitest';
import { xxhash3 } from 'hash-wasm';
import { xxh3_64, xxh3_64str, computeMethodHash } from '../src/rpc-xxhash3.js';

// --- Helpers ---

/** Convert hash-wasm hex output to BigInt */
function hexToBigInt(hex: string): bigint {
    return BigInt('0x' + hex);
}

/** Generate deterministic byte array of given length */
function makeBytes(len: number, seed = 13): Uint8Array {
    const arr = new Uint8Array(len);
    for (let i = 0; i < len; i++) arr[i] = (seed + i * 7) % 256;
    return arr;
}

/** Simple PRNG (xorshift32) for deterministic random generation */
function xorshift32(state: { s: number }): number {
    let x = state.s;
    x ^= x << 13;
    x ^= x >>> 17;
    x ^= x << 5;
    state.s = x >>> 0;
    return state.s;
}

// --- Edge-case vectors from .NET XxHash3.HashToUInt64 ---

// These cover every code path in the XXH3-64 implementation:
//   len=0           → len0 (xxh64Avalanche)
//   len=1..3        → len1to3 (xxh64Avalanche, no PRIME mul)
//   len=4..8        → len4to8 (rrmxmx, seed swap)
//   len=9..16       → len9to16 (swap64, mul128Fold64, xxh3Avalanche)
//   len=17..128     → len17to128 (mix16B loop, xxh3Avalanche)
//   len=129..240    → len129to240 (two-phase mix16B, xxh3Avalanche)
//   len>240         → hashLong (accumulate512, scrambleAcc, mergeAccs)

const DOTNET_VECTORS: [string, bigint][] = [
    // len0
    ['', 3244421341483603138n],
    // len1to3
    ['a', 16629034431890738719n],
    ['ab', 12138170336140424028n],
    ['abc', 8696274497037089104n],
    // len4to8
    ['abcd', 7248448420886124688n],
    ['hello', 10760762337991515389n],
    ['abcdefgh', 8017998777839871107n],
    // len9to16
    ['123456789', 8276685427497336319n],
    ['abcdefghijklmnop', 4412624702490859688n],
    // len17to128
    ['ITypeScriptTestService.Add:2', 12691831522105987730n],
    ['ITypeScriptTestComputeService.GetCounter:2', 11035071790144238159n],
    ['$sys.KeepAlive', 9250077078001227578n],
    ['$sys.Handshake', 3429007717786837182n],
    ['$sys.Ok', 13089043970195582208n],
    ['$sys.I', 17273334572127442430n],
    ['$sys.B', 8919635718310672040n],
    ['$sys.End', 15994401423933406906n],
    // Unicode
    ['hello\0world', 7387166017187604645n],
    ['\u3053\u3093\u306b\u3061\u306f', 3973994090912779455n],
    ['\uD83C\uDF89\uD83D\uDD25\uD83D\uDCAF', 18404621122577759711n],
];

// Test vectors for computeMethodHash (.NET RpcMethodRef.ComputeHashCode)
const METHOD_HASH_VECTORS: [string, number][] = [
    ['ITypeScriptTestService.Add:2', 368407966],
    ['ITypeScriptTestService.Add:3', -1430677206],
    ['ITypeScriptTestService.Greet:1', -949795572],
    ['ITypeScriptTestComputeService.GetCounter:2', -24618632],
    ['$sys.KeepAlive', -2104790457],
    ['$sys.Handshake', -386831013],
    ['$sys.Ok', 225426712],
    ['$sys.I', -52950668],
    ['$sys.B', -1197916366],
    ['$sys.End', -1224767354],
    ['$sys.Ack', -1793137119],
    ['$sys.AckEnd', -1225014432],
    ['$sys.Cancel', 1995375879],
    ['$sys.Error', -1844021631],
    ['', 385620285],
    ['a', 1823657521],
    ['X', -914284492],
];

// --- Tests ---

describe('XXH3-64 — .NET compatibility', () => {
    it('should match .NET XxHash3.HashToUInt64 for all static vectors', () => {
        for (const [input, expected] of DOTNET_VECTORS) {
            const actual = xxh3_64str(input);
            expect(actual).toBe(expected);
        }
    });
});

describe('XXH3-64 — edge cases by code path', () => {
    it('len=0 (empty)', () => {
        expect(xxh3_64(new Uint8Array([]))).toBe(3244421341483603138n);
    });

    it('len=1 (single byte)', () => {
        expect(xxh3_64str('a')).toBe(16629034431890738719n);
    });

    it('len=2..3 (len1to3 path)', () => {
        expect(xxh3_64str('ab')).toBe(12138170336140424028n);
        expect(xxh3_64str('abc')).toBe(8696274497037089104n);
    });

    it('len=4..8 (len4to8 path with seed swap + rrmxmx)', () => {
        expect(xxh3_64str('abcd')).toBe(7248448420886124688n);
        expect(xxh3_64str('abcdefgh')).toBe(8017998777839871107n);
    });

    it('len=9..16 (len9to16 path with swap64 + mul128)', () => {
        expect(xxh3_64str('123456789')).toBe(8276685427497336319n);
        expect(xxh3_64str('abcdefghijklmnop')).toBe(
            4412624702490859688n
        );
    });

    it('len=17..128 (mix16B loop)', () => {
        // 27 bytes
        expect(xxh3_64str('ITypeScriptTestService.Add:2')).toBe(
            12691831522105987730n
        );
    });

    it('len=129..240 (two-phase mix16B)', () => {
        expect(xxh3_64str('w'.repeat(128))).toBe(1998539121132424851n);
        expect(xxh3_64str('w'.repeat(200))).toBe(8543447662465871197n);
    });

    it('len>240 (hashLong: accumulate + scramble + merge)', () => {
        expect(xxh3_64str('v'.repeat(256))).toBe(15805419833403538331n);
    });

    it('null byte in input', () => {
        expect(xxh3_64str('hello\0world')).toBe(7387166017187604645n);
    });

    it('unicode (multi-byte UTF-8)', () => {
        expect(xxh3_64str('\u3053\u3093\u306b\u3061\u306f')).toBe(
            3973994090912779455n
        );
    });
});

describe('XXH3-64 — comparison against hash-wasm on all code paths', () => {
    // Test every length from 0..260 to cover all branch transitions
    const boundaryLengths = Array.from({ length: 261 }, (_, i) => i);

    // Also test larger sizes hitting the hashLong loop/scramble boundaries
    const largeLengths = [
        300, 500, 512, 1000, 1023, 1024, 1025, 2048, 3000, 4096,
    ];

    const allLengths = [...boundaryLengths, ...largeLengths];

    it('should match hash-wasm for every length from 0..260 + large sizes', async () => {
        for (const len of allLengths) {
            const input = makeBytes(len);
            const ours = xxh3_64(input);
            const refHex = await xxhash3(input);
            const ref = hexToBigInt(refHex);
            expect(ours).toBe(
                ref,
                `Mismatch at len=${len}: ours=${ours}, ref=${ref}`
            );
        }
    });
});

describe('XXH3-64 — 1000 random inputs vs hash-wasm', () => {
    it('should match hash-wasm for 1000 random byte arrays (lengths 0..5000)', async () => {
        const state = { s: 42 }; // PRNG seed
        let failures = 0;

        for (let i = 0; i < 1000; i++) {
            const len = xorshift32(state) % 5001;
            const input = new Uint8Array(len);
            for (let j = 0; j < len; j++) {
                input[j] = xorshift32(state) & 0xff;
            }

            const ours = xxh3_64(input);
            const refHex = await xxhash3(input);
            const ref = hexToBigInt(refHex);

            if (ours !== ref) {
                failures++;
                if (failures <= 3) {
                    // Report first few failures for debugging
                    console.error(
                        `FAIL i=${i} len=${len}: ours=${ours}, ref=${ref}`
                    );
                }
            }
        }

        expect(failures).toBe(0);
    });
});

describe('computeMethodHash', () => {
    it('should match .NET RpcMethodRef.ComputeHashCode for all vectors', () => {
        for (const [method, expected] of METHOD_HASH_VECTORS) {
            expect(computeMethodHash(method)).toBe(expected);
        }
    });

    it('should be consistent', () => {
        expect(computeMethodHash('$sys.Ok')).toBe(
            computeMethodHash('$sys.Ok')
        );
    });

    it('should differ for different methods', () => {
        expect(computeMethodHash('$sys.Ok')).not.toBe(
            computeMethodHash('$sys.Error')
        );
    });

    it('should differ for overloads with different arity', () => {
        expect(
            computeMethodHash('ITypeScriptTestService.Add:2')
        ).not.toBe(computeMethodHash('ITypeScriptTestService.Add:3'));
    });
});
