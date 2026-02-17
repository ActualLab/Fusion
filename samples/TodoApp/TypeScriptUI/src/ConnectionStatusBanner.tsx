import React from "react";
import { type RpcPeerState, RpcPeerStateKind, type RpcPeerStateMonitor } from "@actuallab/rpc";

interface Props {
  monitor: RpcPeerStateMonitor;
}

export function ConnectionStatusBanner({ monitor }: Props) {
  const [state, setState] = React.useState<RpcPeerState>(() => monitor.state);

  React.useEffect(() => {
    const handler = (s: RpcPeerState) => setState(s);
    monitor.stateChanged.add(handler);
    // Sync in case state changed between render and effect
    setState(monitor.state);
    return () => monitor.stateChanged.remove(handler);
  }, [monitor]);

  if (state.kind === RpcPeerStateKind.Connected || state.kind === RpcPeerStateKind.JustConnected)
    return null;

  const isJustDisconnected = state.kind === RpcPeerStateKind.JustDisconnected;
  const alertClass = isJustDisconnected ? "alert-warning" : "alert-danger";
  const reconnectsInSec = state.reconnectsIn > 0 ? Math.ceil(state.reconnectsIn / 1000) : 0;

  return (
    <div className={`alert ${alertClass} d-flex justify-content-between align-items-center my-2`}>
      <span>
        {isJustDisconnected
          ? "Just disconnected, reconnecting..."
          : reconnectsInSec > 0
            ? `Disconnected. Will reconnect in ${reconnectsInSec}s.`
            : "Reconnecting..."}
      </span>
      {!isJustDisconnected && reconnectsInSec > 0 && (
        <button
          type="button"
          className="btn btn-success btn-sm"
          onClick={() => monitor.peer.reconnectDelayer.cancelDelays()}
        >
          Reconnect now
        </button>
      )}
    </div>
  );
}
