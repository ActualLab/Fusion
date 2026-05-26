import { describe, it, expect, afterEach } from 'vitest';
import { RpcHub, RpcClientPeer, RpcLimits } from '../src/index.js';

describe('RpcLimits', () => {
    // Restore process-wide default after each test that touches it.
    const originalDefault = RpcLimits.Default;
    afterEach(() => {
        RpcLimits.Default = originalDefault;
        // Also undo any in-place mutation of the live Default instance.
        Object.assign(originalDefault, new RpcLimits());
    });

    it('exposes documented defaults', () => {
        const limits = new RpcLimits();
        expect(limits.connectTimeoutMs).toBe(10_000);
        expect(limits.handshakeTimeoutMs).toBe(10_000);
        expect(limits.keepAlivePeriodMs).toBe(10_000);
        expect(limits.keepAliveTimeoutMs).toBe(25_000);
    });

    it('constructor accepts a partial override', () => {
        const limits = new RpcLimits({ keepAliveTimeoutMs: 60_000 });
        expect(limits.keepAliveTimeoutMs).toBe(60_000);
        // Other fields keep their defaults
        expect(limits.connectTimeoutMs).toBe(10_000);
    });

    it('hub defaults to RpcLimits.Default', () => {
        const hub = new RpcHub();
        expect(hub.limits).toBe(RpcLimits.Default);
    });

    it('per-peer fields are snapshotted from hub.limits at construction', () => {
        const hub = new RpcHub();
        hub.limits = new RpcLimits({
            connectTimeoutMs: 1234,
            handshakeTimeoutMs: 5678,
            keepAlivePeriodMs: 2000,
            keepAliveTimeoutMs: 9000,
        });

        const peer = new RpcClientPeer(hub, 'ws://test', /* mustStart */ false);
        expect(peer.connectTimeoutMs).toBe(1234);
        expect(peer.handshakeTimeoutMs).toBe(5678);
        expect(peer.keepAlivePeriodMs).toBe(2000);
        expect(peer.keepAliveTimeoutMs).toBe(9000);
        peer.close();
        hub.close();
    });

    it('later mutations of hub.limits do NOT retroactively affect existing peers', () => {
        const hub = new RpcHub();
        hub.limits = new RpcLimits({ connectTimeoutMs: 100 });
        const peer = new RpcClientPeer(hub, 'ws://test', false);
        expect(peer.connectTimeoutMs).toBe(100);

        // Mutate the hub's limits — the peer should still see its snapshot.
        hub.limits.connectTimeoutMs = 9999;
        expect(peer.connectTimeoutMs).toBe(100);

        peer.close();
        hub.close();
    });

    it('mutating RpcLimits.Default fields propagates to hubs that share the reference', () => {
        // Hubs default to using `RpcLimits.Default` (the same object reference),
        // so in-place mutations affect every hub that hasn't replaced its
        // limits with a custom instance.
        const hub1 = new RpcHub();
        const hub2 = new RpcHub();
        expect(hub1.limits).toBe(hub2.limits);
        expect(hub1.limits).toBe(RpcLimits.Default);

        RpcLimits.Default.keepAliveTimeoutMs = 99_999;
        expect(hub1.limits.keepAliveTimeoutMs).toBe(99_999);
        expect(hub2.limits.keepAliveTimeoutMs).toBe(99_999);

        hub1.close();
        hub2.close();
    });

    it('replacing RpcLimits.Default only affects hubs constructed after the swap', () => {
        const hubBefore = new RpcHub();
        const beforeRef = hubBefore.limits;

        RpcLimits.Default = new RpcLimits({ keepAliveTimeoutMs: 12345 });

        const hubAfter = new RpcHub();
        expect(hubAfter.limits).toBe(RpcLimits.Default);
        expect(hubAfter.limits.keepAliveTimeoutMs).toBe(12345);

        // The old hub still holds the old Default reference.
        expect(hubBefore.limits).toBe(beforeRef);
        expect(hubBefore.limits.keepAliveTimeoutMs).toBe(25_000);

        hubBefore.close();
        hubAfter.close();
    });

    it('per-peer field override takes precedence over hub.limits at peer build', () => {
        const hub = new RpcHub();
        const peer = new RpcClientPeer(hub, 'ws://test', false);
        // Default from RpcLimits
        expect(peer.connectTimeoutMs).toBe(10_000);
        // Override per-peer
        peer.connectTimeoutMs = 42;
        expect(peer.connectTimeoutMs).toBe(42);

        peer.close();
        hub.close();
    });
});
