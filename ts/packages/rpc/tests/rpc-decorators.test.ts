import { describe, it, expect } from 'vitest';
import {
    rpcService,
    rpcMethod,
    getServiceMeta,
    getMethodsMeta,
    RpcType,
} from '../src/index.js';

describe('@rpcService decorator', () => {
    it('should store service name in metadata', () => {
        @rpcService('ProductService')
        class ProductService {
            getProduct(_id: string): unknown {
                return undefined;
            }
        }

        const meta = getServiceMeta(ProductService);
        expect(meta).toBeDefined();
        expect(meta!.name).toBe('ProductService');
    });
});

describe('@rpcMethod decorator', () => {
    it('should store method metadata', () => {
        class Svc {
            @rpcMethod()
            getProduct(_id: string): unknown {
                return undefined;
            }

            @rpcMethod({ returns: RpcType.stream })
            // eslint-disable-next-line @typescript-eslint/require-await
            async *getProducts(
                _query: string,
                _limit: number
            ): AsyncGenerator {
                yield undefined;
            }
        }

        const meta = getMethodsMeta(Svc);
        expect(meta).toBeDefined();

        expect(meta!['getProduct']).toEqual({ argCount: 1 });
        expect(meta!['getProducts']).toEqual({
            argCount: 2,
            returns: RpcType.stream,
        });
    });

    it('should not wrap the method', () => {
        class Svc {
            @rpcMethod()
            getItem(id: string): string {
                return id;
            }
        }

        // rpcMethod returns target unchanged — method still works normally
        const svc = new Svc();
        expect(svc.getItem('abc')).toBe('abc');

        const meta = getMethodsMeta(Svc);
        expect(meta).toBeDefined();
        expect(meta!['getItem']).toEqual({ argCount: 1 });
    });
});

describe('@rpcService + @rpcMethod combined', () => {
    it('should store both service and method metadata on same class', () => {
        @rpcService('CounterService')
        class ICounterService {
            @rpcMethod()
            getCount(_key: string): number {
                return 0;
            }

            @rpcMethod()
            setCount(_key: string, _value: number): void { /* noop */ }

            @rpcMethod({ returns: RpcType.stream })
            // eslint-disable-next-line @typescript-eslint/require-await
            async *watchCount(_key: string): AsyncGenerator<number> {
                yield 0;
            }
        }

        const svcMeta = getServiceMeta(ICounterService);
        expect(svcMeta).toEqual({ name: 'CounterService' });

        const methods = getMethodsMeta(ICounterService);
        expect(methods).toBeDefined();
        expect(methods!['getCount']).toEqual({ argCount: 1 });
        expect(methods!['setCount']).toEqual({ argCount: 2 });
        expect(methods!['watchCount']).toEqual({
            argCount: 1,
            returns: RpcType.stream,
        });
    });

    it('should return undefined for non-decorated classes', () => {
        class Plain {
            doStuff(): void { /* noop */ }
        }

        expect(getServiceMeta(Plain)).toBeUndefined();
        expect(getMethodsMeta(Plain)).toBeUndefined();
    });
});
