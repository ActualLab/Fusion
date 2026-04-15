// Pure TypeScript XXH3-64 implementation using BigInt.
// Sync, zero dependencies. Supports inputs of any length.
//
// Reference: https://github.com/Cyan4973/xxHash/blob/dev/xxhash.h
// Verified against .NET System.IO.Hashing.XxHash3.HashToUInt64.

 

const U64_MASK = 0xffff_ffff_ffff_ffffn;
const U32_MASK = 0xffff_ffffn;

const PRIME32_1 = 0x9e3779b1;
const PRIME32_2 = 0x85ebca77;
const PRIME32_3 = 0xc2b2ae3d;

const PRIME64_1 = 0x9e3779b185ebca87n;
const PRIME64_2 = 0xc2b2ae3d27d4eb4fn;
const PRIME64_3 = 0x165667b19e3779f9n;
const PRIME64_4 = 0x85ebca77c2b2ae63n;
const PRIME64_5 = 0x27d4eb2f165667c5n;

const XXH3_SECRET_SIZE_MIN = 136;
const XXH_SECRET_DEFAULT_SIZE = 192;

const STRIPE_LEN = 64;
const XXH_SECRET_CONSUME_RATE = 8;
const ACC_NB = STRIPE_LEN / 8;

const XXH3_MIDSIZE_MAX = 240;
const XXH3_MIDSIZE_STARTOFFSET = 3;
const XXH3_MIDSIZE_LASTOFFSET = 17;
const XXH_SECRET_LASTACC_START = 7;
const XXH_SECRET_MERGEACCS_START = 11;

const RRMXMX = 0x9fb21c651e98df25n;

// prettier-ignore
const DEFAULT_SECRET = new Uint8Array([
    0xb8, 0xfe, 0x6c, 0x39, 0x23, 0xa4, 0x4b, 0xbe, 0x7c, 0x01, 0x81, 0x2c, 0xf7, 0x21, 0xad, 0x1c,
    0xde, 0xd4, 0x6d, 0xe9, 0x83, 0x90, 0x97, 0xdb, 0x72, 0x40, 0xa4, 0xa4, 0xb7, 0xb3, 0x67, 0x1f,
    0xcb, 0x79, 0xe6, 0x4e, 0xcc, 0xc0, 0xe5, 0x78, 0x82, 0x5a, 0xd0, 0x7d, 0xcc, 0xff, 0x72, 0x21,
    0xb8, 0x08, 0x46, 0x74, 0xf7, 0x43, 0x24, 0x8e, 0xe0, 0x35, 0x90, 0xe6, 0x81, 0x3a, 0x26, 0x4c,
    0x3c, 0x28, 0x52, 0xbb, 0x91, 0xc3, 0x00, 0xcb, 0x88, 0xd0, 0x65, 0x8b, 0x1b, 0x53, 0x2e, 0xa3,
    0x71, 0x64, 0x48, 0x97, 0xa2, 0x0d, 0xf9, 0x4e, 0x38, 0x19, 0xef, 0x46, 0xa9, 0xde, 0xac, 0xd8,
    0xa8, 0xfa, 0x76, 0x3f, 0xe3, 0x9c, 0x34, 0x3f, 0xf9, 0xdc, 0xbb, 0xc7, 0xc7, 0x0b, 0x4f, 0x1d,
    0x8a, 0x51, 0xe0, 0x4b, 0xcd, 0xb4, 0x59, 0x31, 0xc8, 0x9f, 0x7e, 0xc9, 0xd9, 0x78, 0x73, 0x64,
    0xea, 0xc5, 0xac, 0x83, 0x34, 0xd3, 0xeb, 0xc3, 0xc5, 0x81, 0xa0, 0xff, 0xfa, 0x13, 0x63, 0xeb,
    0x17, 0x0d, 0xdd, 0x51, 0xb7, 0xf0, 0xda, 0x49, 0xd3, 0x16, 0x55, 0x26, 0x29, 0xd4, 0x68, 0x9e,
    0x2b, 0x16, 0xbe, 0x58, 0x7d, 0x47, 0xa1, 0xfc, 0x8f, 0xf8, 0xb8, 0xd1, 0x7a, 0xd0, 0x31, 0xce,
    0x45, 0xcb, 0x3a, 0x8f, 0x95, 0x16, 0x04, 0x28, 0xaf, 0xd7, 0xfb, 0xca, 0xbb, 0x4b, 0x40, 0x7e,
]);

function u64(x: bigint): bigint {
    return x & U64_MASK;
}

function read32LE(buf: Uint8Array, off: number): number {
    return (
        (buf[off] |
            (buf[off + 1] << 8) |
            (buf[off + 2] << 16) |
            (buf[off + 3] << 24)) >>>
        0
    );
}

function read64LE(buf: Uint8Array, off: number): bigint {
    return (
        BigInt(buf[off]) |
        (BigInt(buf[off + 1]) << 8n) |
        (BigInt(buf[off + 2]) << 16n) |
        (BigInt(buf[off + 3]) << 24n) |
        (BigInt(buf[off + 4]) << 32n) |
        (BigInt(buf[off + 5]) << 40n) |
        (BigInt(buf[off + 6]) << 48n) |
        (BigInt(buf[off + 7]) << 56n)
    );
}

function write64LE(buf: Uint8Array, off: number, value: bigint): void {
    let x = u64(value);
    for (let i = 0; i < 8; i++) {
        buf[off + i] = Number(x & 0xffn);
        x >>= 8n;
    }
}

function swap32(x: number): number {
    return (
        (((x << 24) & 0xff000000) |
            ((x << 8) & 0x00ff0000) |
            ((x >>> 8) & 0x0000ff00) |
            ((x >>> 24) & 0x000000ff)) >>>
        0
    );
}

function swap64(x: bigint): bigint {
    x = u64(x);
    return (
        ((x << 56n) & 0xff00000000000000n) |
        ((x << 40n) & 0x00ff000000000000n) |
        ((x << 24n) & 0x0000ff0000000000n) |
        ((x << 8n) & 0x000000ff00000000n) |
        ((x >> 8n) & 0x00000000ff000000n) |
        ((x >> 24n) & 0x0000000000ff0000n) |
        ((x >> 40n) & 0x000000000000ff00n) |
        ((x >> 56n) & 0x00000000000000ffn)
    );
}

function rotl64(x: bigint, amount: number): bigint {
    const r = BigInt(amount & 63);
    return u64((x << r) | (x >> (64n - r)));
}

function mul128Fold64(lhs: bigint, rhs: bigint): bigint {
    const product = u64(lhs) * u64(rhs);
    return u64(product) ^ u64(product >> 64n);
}

// XXH64_avalanche — used by len_0 and len_1to3
function xxh64Avalanche(hash: bigint): bigint {
    let h = u64(hash);
    h ^= h >> 33n;
    h = u64(h * PRIME64_2);
    h ^= h >> 29n;
    h = u64(h * PRIME64_3);
    h ^= h >> 32n;
    return u64(h);
}

// XXH3_avalanche — used by len_9to16, len_17to128, len_129to240
function xxh3Avalanche(hash: bigint): bigint {
    let h = u64(hash);
    h ^= h >> 37n;
    h = u64(h * 0x165667919e3779f9n);
    h ^= h >> 32n;
    return u64(h);
}

function len0(secret: Uint8Array, seed: bigint): bigint {
    const acc = u64(seed ^ read64LE(secret, 56) ^ read64LE(secret, 64));
    return xxh64Avalanche(acc);
}

function len1to3(
    input: Uint8Array,
    length: number,
    secret: Uint8Array,
    seed: bigint
): bigint {
    const byte1 = input[0];
    const byte2 = length > 1 ? input[1] : input[0];
    const byte3 = input[length - 1];
    const combined =
        ((byte1 << 16) | (byte2 << 24) | byte3 | (length << 8)) >>> 0;
    const bitflip = u64(
        BigInt((read32LE(secret, 0) ^ read32LE(secret, 4)) >>> 0) + seed
    );
    const value64 = u64(BigInt(combined) ^ bitflip);
    return xxh64Avalanche(value64);
}

function len4to8(
    input: Uint8Array,
    length: number,
    secret: Uint8Array,
    seed: bigint
): bigint {
    const inputHi = read32LE(input, 0);
    const inputLo = read32LE(input, length - 4);
    const input64 = BigInt(inputLo) | (BigInt(inputHi) << 32n);
    let acc = read64LE(secret, 8) ^ read64LE(secret, 16);
    seed ^= BigInt(swap32(Number(seed & U32_MASK))) << 32n;
    acc = u64(acc - seed);
    acc ^= input64;
    acc ^= rotl64(acc, 49) ^ rotl64(acc, 24);
    acc = u64(acc * RRMXMX);
    acc ^= (acc >> 35n) + BigInt(length);
    acc = u64(acc * RRMXMX);
    acc ^= acc >> 28n;
    return u64(acc);
}

function len9to16(
    input: Uint8Array,
    length: number,
    secret: Uint8Array,
    seed: bigint
): bigint {
    let inputLo = read64LE(secret, 24) ^ read64LE(secret, 32);
    let inputHi = read64LE(secret, 40) ^ read64LE(secret, 48);
    let acc = BigInt(length);
    inputLo = u64(inputLo + seed);
    inputHi = u64(inputHi - seed);
    inputLo ^= read64LE(input, 0);
    inputHi ^= read64LE(input, length - 8);
    acc = u64(acc + swap64(inputLo));
    acc = u64(acc + inputHi);
    acc = u64(acc + mul128Fold64(inputLo, inputHi));
    return xxh3Avalanche(acc);
}

function mix16B(
    input: Uint8Array,
    inputOff: number,
    secret: Uint8Array,
    secretOff: number,
    seed: bigint
): bigint {
    let lhs = seed;
    let rhs = u64(0n - seed);
    lhs = u64(lhs + read64LE(secret, secretOff));
    rhs = u64(rhs + read64LE(secret, secretOff + 8));
    lhs ^= read64LE(input, inputOff);
    rhs ^= read64LE(input, inputOff + 8);
    return mul128Fold64(lhs, rhs);
}

function len17to128(
    input: Uint8Array,
    length: number,
    secret: Uint8Array,
    seed: bigint
): bigint {
    let i = Math.floor((length - 1) / 32);
    let acc = u64(BigInt(length) * PRIME64_1);
    for (; i >= 0; i--) {
        acc = u64(acc + mix16B(input, 16 * i, secret, 32 * i, seed));
        acc = u64(
            acc +
                mix16B(
                    input,
                    length - 16 * (i + 1),
                    secret,
                    32 * i + 16,
                    seed
                )
        );
    }
    return xxh3Avalanche(acc);
}

function len129to240(
    input: Uint8Array,
    length: number,
    secret: Uint8Array,
    seed: bigint
): bigint {
    let acc = u64(BigInt(length) * PRIME64_1);
    const nbRounds = Math.floor(length / 16);
    for (let i = 0; i < 8; i++) {
        acc = u64(acc + mix16B(input, 16 * i, secret, 16 * i, seed));
    }
    acc = xxh3Avalanche(acc);
    for (let i = 8; i < nbRounds; i++) {
        acc = u64(
            acc +
                mix16B(
                    input,
                    16 * i,
                    secret,
                    16 * (i - 8) + XXH3_MIDSIZE_STARTOFFSET,
                    seed
                )
        );
    }
    acc = u64(
        acc +
            mix16B(
                input,
                length - 16,
                secret,
                XXH3_SECRET_SIZE_MIN - XXH3_MIDSIZE_LASTOFFSET,
                seed
            )
    );
    return xxh3Avalanche(acc);
}

function hashShort(
    input: Uint8Array,
    length: number,
    secret: Uint8Array,
    seed: bigint
): bigint {
    if (length <= 16) {
        if (length > 8) return len9to16(input, length, secret, seed);
        if (length >= 4) return len4to8(input, length, secret, seed);
        if (length !== 0) return len1to3(input, length, secret, seed);
        return len0(secret, seed);
    }
    if (length <= 128) return len17to128(input, length, secret, seed);
    return len129to240(input, length, secret, seed);
}

// --- Long input (> 240 bytes) ---

function accumulate512(
    acc: bigint[],
    input: Uint8Array,
    inputOff: number,
    secret: Uint8Array,
    secretOff: number
): void {
    for (let i = 0; i < ACC_NB; i++) {
        const dataVal = read64LE(input, inputOff + 8 * i);
        const dataKey = dataVal ^ read64LE(secret, secretOff + 8 * i);
        acc[i ^ 1] = u64(acc[i ^ 1] + dataVal);
        const low32 = dataKey & U32_MASK;
        const high32 = dataKey >> 32n;
        acc[i] = u64(acc[i] + low32 * high32);
    }
}

function scrambleAcc(
    acc: bigint[],
    secret: Uint8Array,
    secretOff: number
): void {
    for (let i = 0; i < ACC_NB; i++) {
        let x = acc[i];
        x ^= x >> 47n;
        x ^= read64LE(secret, secretOff + 8 * i);
        x = u64(x * BigInt(PRIME32_1));
        acc[i] = x;
    }
}

function accumulateLoop(
    acc: bigint[],
    input: Uint8Array,
    inputOff: number,
    secret: Uint8Array,
    secretOff: number,
    nbStripes: number
): void {
    for (let n = 0; n < nbStripes; n++) {
        accumulate512(
            acc,
            input,
            inputOff + n * STRIPE_LEN,
            secret,
            secretOff + 8 * n
        );
    }
}

function mix2Accs(
    acc: bigint[],
    accOff: number,
    secret: Uint8Array,
    secretOff: number
): bigint {
    return mul128Fold64(
        acc[accOff] ^ read64LE(secret, secretOff),
        acc[accOff + 1] ^ read64LE(secret, secretOff + 8)
    );
}

function mergeAccs(
    acc: bigint[],
    key: Uint8Array,
    keyOff: number,
    start: bigint
): bigint {
    let result = start;
    for (let i = 0; i < 4; i++) {
        result = u64(result + mix2Accs(acc, 2 * i, key, keyOff + 16 * i));
    }
    return xxh3Avalanche(result);
}

function hashLong(
    input: Uint8Array,
    length: number,
    secret: Uint8Array,
    secretSize: number
): bigint {
    const nbRounds = Math.floor(
        (secretSize - STRIPE_LEN) / XXH_SECRET_CONSUME_RATE
    );
    const blockLen = STRIPE_LEN * nbRounds;
    const nbBlocks = Math.floor((length - 1) / blockLen);
    const nbStripes = Math.floor(
        ((length - 1) - blockLen * nbBlocks) / STRIPE_LEN
    );

    const acc: bigint[] = [
        BigInt(PRIME32_3),
        PRIME64_1,
        PRIME64_2,
        PRIME64_3,
        PRIME64_4,
        BigInt(PRIME32_2),
        PRIME64_5,
        BigInt(PRIME32_1),
    ];

    for (let n = 0; n < nbBlocks; n++) {
        accumulateLoop(acc, input, n * blockLen, secret, 0, nbRounds);
        scrambleAcc(acc, secret, secretSize - STRIPE_LEN);
    }

    accumulateLoop(acc, input, nbBlocks * blockLen, secret, 0, nbStripes);

    // Last stripe — always applied (C reference: unconditional)
    accumulate512(
        acc,
        input,
        length - STRIPE_LEN,
        secret,
        secretSize - STRIPE_LEN - XXH_SECRET_LASTACC_START
    );

    return mergeAccs(
        acc,
        secret,
        XXH_SECRET_MERGEACCS_START,
        u64(BigInt(length) * PRIME64_1)
    );
}

function hashLongWithSeed(
    input: Uint8Array,
    length: number,
    seed: bigint
): bigint {
    const secret = new Uint8Array(XXH_SECRET_DEFAULT_SIZE);
    for (let i = 0; i < XXH_SECRET_DEFAULT_SIZE / 16; i++) {
        const off = 16 * i;
        write64LE(secret, off, u64(read64LE(DEFAULT_SECRET, off) + seed));
        write64LE(
            secret,
            off + 8,
            u64(read64LE(DEFAULT_SECRET, off + 8) - seed)
        );
    }
    return hashLong(input, length, secret, secret.length);
}

// --- Public API ---

const textEncoder = new TextEncoder();

/**
 * Compute XXH3-64 hash of a byte array. Returns a BigInt (unsigned 64-bit).
 * Pure TypeScript, synchronous, supports any input length.
 */
export function xxh3_64(input: Uint8Array, seed = 0n): bigint {
    const len = input.length;
    if (len <= XXH3_MIDSIZE_MAX) {
        return hashShort(input, len, DEFAULT_SECRET, u64(seed));
    }
    return hashLongWithSeed(input, len, u64(seed));
}

/**
 * Compute XXH3-64 hash of a UTF-8 string. Returns a BigInt (unsigned 64-bit).
 */
export function xxh3_64str(input: string, seed = 0n): bigint {
    return xxh3_64(textEncoder.encode(input), seed);
}

/**
 * Compute the RPC method hash matching .NET's RpcMethodRef.ComputeHashCode.
 * Returns a 32-bit signed integer (lower 32 bits of XXH3-64 of [prefix + utf8Name]).
 *
 * The prefix is uint32 LE of (67211 * utf8Name.length), which ensures methods
 * with different name lengths but similar text produce different hashes.
 *
 * Computed on demand — caller should cache the result per method name.
 */
export function computeMethodHash(methodName: string): number {
    const utf8 = textEncoder.encode(methodName);
    // Prefix: uint32 LE of (67211 * utf8.length)
    const prefixValue = Math.imul(67211, utf8.length) >>> 0;
    const prefixed = new Uint8Array(4 + utf8.length);
    prefixed[0] = prefixValue & 0xff;
    prefixed[1] = (prefixValue >>> 8) & 0xff;
    prefixed[2] = (prefixValue >>> 16) & 0xff;
    prefixed[3] = (prefixValue >>> 24) & 0xff;
    prefixed.set(utf8, 4);
    const hash64 = xxh3_64(prefixed);
    // Truncate to signed int32 (same as C#'s unchecked((int)hash64))
    return Number(hash64 & 0xffffffffn) | 0;
}
