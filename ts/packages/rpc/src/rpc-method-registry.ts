// Method name ↔ hash registry for compact binary serialization.
//
// .NET counterpart: RpcMethodResolver — maintains MethodByHashCode dictionary
// for resolving compact 4-byte method hashes back to full method names.
//
// Hashes are computed on demand (first use) and cached. The computeMethodHash
// function uses BigInt-based XXH3-64, which is pure sync TypeScript.

import { getLogs } from './logging.js';
import { computeMethodHash } from './rpc-xxhash3.js';

const { warnLog } = getLogs('RpcMethodRegistry');

/**
 * Registry mapping RPC method wire names to/from their 4-byte XXH3 hashes.
 * Hashes match .NET's RpcMethodRef.ComputeHashCode.
 * Hashes are computed lazily on first access and cached.
 */
export class RpcMethodRegistry {
    private _nameToHash = new Map<string, number>();
    private _hashToName = new Map<number, string>();

    /** Register a method name and compute its hash. */
    register(methodName: string): void {
        if (this._nameToHash.has(methodName)) return;
        const hash = computeMethodHash(methodName);

        // Check for hash collisions
        const existing = this._hashToName.get(hash);
        if (existing !== undefined && existing !== methodName) {
            warnLog?.log(`hash collision: "${methodName}" and "${existing}" both hash to ${hash}`);
            return;
        }

        this._nameToHash.set(methodName, hash);
        this._hashToName.set(hash, methodName);
    }

    /** Register all wire method names from a service definition. */
    registerService(
        serviceName: string,
        methods: ReadonlyMap<string, { name: string; wireArgCount: number }>
    ): void {
        for (const [, def] of methods) {
            const wireName = `${serviceName}.${def.name}:${def.wireArgCount}`;
            this.register(wireName);
        }
    }

    /** Get the hash for a method name. Computes on demand if not registered. */
    getHash(methodName: string): number {
        let hash = this._nameToHash.get(methodName);
        if (hash === undefined) {
            this.register(methodName);
            hash = this._nameToHash.get(methodName)!;
        }
        return hash;
    }

    /** Get the method name for a hash. Returns undefined if not registered. */
    getName(hash: number): string | undefined {
        return this._hashToName.get(hash);
    }

    /** Get the hash for a method name, throwing if not registered. */
    requireHash(methodName: string): number {
        return this.getHash(methodName);
    }
}
