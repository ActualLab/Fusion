// .NET counterpart: ActualLab.Fusion.Extensions.RpcPeerStateMonitor
// Simplified TS version using EventHandlerSet + setTimeout (no Fusion computed state).

import { EventHandlerSet } from "@actuallab/core";
import type { RpcClientPeer } from "./rpc-peer.js";
import { type RpcPeerState, RpcPeerStateKind } from "./rpc-peer-state.js";

const JustConnectedPeriodMs = 1500;
const JustDisconnectedPeriodMs = 3000;
const MinReconnectsInMs = 1000;

export class RpcPeerStateMonitor {
  readonly peer: RpcClientPeer;
  readonly stateChanged = new EventHandlerSet<RpcPeerState>();

  private _rawConnected: boolean;
  private _connectedAt = 0;
  private _disconnectedAt = 0;
  private _lastError: Error | undefined;
  private _state: RpcPeerState;
  private _timer: ReturnType<typeof setTimeout> | undefined;

  // Arrow functions to preserve `this` when used as event handlers
  private _onConnected = (): void => {
    this._rawConnected = true;
    this._connectedAt = Date.now();
    this._lastError = undefined;
    this._recompute();
  };

  private _onDisconnected = (ev: { code: number; reason: string }): void => {
    const wasConnected = this._rawConnected;
    this._rawConnected = false;
    this._lastError = new Error(`Connection closed: ${ev.reason} (code ${ev.code})`);
    if (wasConnected) {
      // True connected→disconnected transition — start JustDisconnected period
      this._disconnectedAt = Date.now();
    }
    // When wasConnected=false, this is a failed reconnect attempt —
    // keep the original _disconnectedAt so the countdown isn't reset.
    this._recompute();
  };

  private _onReconnectsAtChanged = (): void => {
    this._recompute();
  };

  private _onCancelDelaysChanged = (): void => {
    this._recompute();
  };

  constructor(peer: RpcClientPeer) {
    this.peer = peer;
    this._rawConnected = peer.isConnected;

    if (this._rawConnected) {
      this._connectedAt = Date.now();
      this._state = { kind: RpcPeerStateKind.JustConnected, reconnectsIn: 0 };
    } else {
      this._disconnectedAt = Date.now();
      this._state = { kind: RpcPeerStateKind.JustDisconnected, reconnectsIn: 0 };
    }

    peer.connected.add(this._onConnected);
    peer.disconnected.add(this._onDisconnected);
    peer.reconnectsAtChanged.add(this._onReconnectsAtChanged);
    peer.reconnectDelayer.cancelDelaysChanged.add(this._onCancelDelaysChanged);

    // Schedule initial transition from Just* states
    this._scheduleRecompute(this._rawConnected ? JustConnectedPeriodMs : JustDisconnectedPeriodMs);
  }

  get state(): RpcPeerState {
    return this._state;
  }

  dispose(): void {
    if (this._timer !== undefined) {
      clearTimeout(this._timer);
      this._timer = undefined;
    }
    this.peer.connected.remove(this._onConnected);
    this.peer.disconnected.remove(this._onDisconnected);
    this.peer.reconnectsAtChanged.remove(this._onReconnectsAtChanged);
    this.peer.reconnectDelayer.cancelDelaysChanged.remove(this._onCancelDelaysChanged);
  }

  private _recompute(): void {
    if (this._timer !== undefined) {
      clearTimeout(this._timer);
      this._timer = undefined;
    }

    const now = Date.now();
    let newState: RpcPeerState;

    if (this._rawConnected) {
      const connectedFor = now - this._connectedAt;
      if (connectedFor < JustConnectedPeriodMs) {
        newState = { kind: RpcPeerStateKind.JustConnected, reconnectsIn: 0 };
        this._scheduleRecompute(JustConnectedPeriodMs - connectedFor);
      } else {
        newState = { kind: RpcPeerStateKind.Connected, reconnectsIn: 0 };
      }
    } else {
      const disconnectedFor = now - this._disconnectedAt;
      if (disconnectedFor < JustDisconnectedPeriodMs) {
        newState = { kind: RpcPeerStateKind.JustDisconnected, lastError: this._lastError, reconnectsIn: 0 };
        this._scheduleRecompute(JustDisconnectedPeriodMs - disconnectedFor);
      } else {
        const reconnectsAt = this.peer.reconnectsAt;
        const reconnectsIn = reconnectsAt > 0 ? Math.max(0, reconnectsAt - now) : 0;
        if (reconnectsIn < MinReconnectsInMs) {
          newState = { kind: RpcPeerStateKind.Disconnected, lastError: this._lastError, reconnectsIn: 0 };
        } else {
          newState = { kind: RpcPeerStateKind.Disconnected, lastError: this._lastError, reconnectsIn };
          // Recompute periodically to update countdown
          this._scheduleRecompute(Math.min(1000, reconnectsIn - MinReconnectsInMs));
        }
      }
    }

    this._setState(newState);
  }

  private _setState(newState: RpcPeerState): void {
    const old = this._state;
    if (old.kind === newState.kind
      && old.lastError === newState.lastError
      && old.reconnectsIn === newState.reconnectsIn)
      return;

    this._state = newState;
    this.stateChanged.trigger(newState);
  }

  private _scheduleRecompute(delayMs: number): void {
    if (this._timer !== undefined) clearTimeout(this._timer);
    this._timer = setTimeout(() => {
      this._timer = undefined;
      this._recompute();
    }, Math.max(0, delayMs));
  }
}
