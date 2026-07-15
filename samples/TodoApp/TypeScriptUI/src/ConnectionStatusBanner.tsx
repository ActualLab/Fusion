import React from "react";
import {
  getStateDescription,
  isConnected,
  likelyConnected,
  type RpcPeerState,
  type RpcPeerStateMonitor,
} from "@actuallab/rpc";

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

  if (isConnected(state))
    return null;

  // Connected states are already handled above, so likelyConnected() (kind !== Disconnected)
  // now narrows to the transient "just disconnected" grace period — a soft warning, vs. a
  // full disconnect (error) that may show a reconnect countdown and a manual retry button.
  const isJustDisconnected = likelyConnected(state);
  const alertClass = isJustDisconnected ? "alert-warning" : "alert-danger";
  const reconnectsInSec = state.reconnectsIn > 0 ? Math.ceil(state.reconnectsIn / 1000) : 0;
  const message = reconnectsInSec > 0
    ? `Disconnected. Will reconnect in ${reconnectsInSec}s.`
    : getStateDescription(state, true);

  return (
    <div className={`alert ${alertClass} d-flex justify-content-between align-items-center my-2`}>
      <span>{message}</span>
      {!isJustDisconnected && reconnectsInSec > 0 && (
        <button
          type="button"
          className="btn btn-success btn-sm"
          onClick={() => monitor.peer.hub.reconnectDelayer.cancelDelays()}
        >
          Reconnect now
        </button>
      )}
    </div>
  );
}
