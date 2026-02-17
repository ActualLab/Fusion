// .NET counterpart: ActualLab.Fusion.Extensions.RpcPeerState

export const enum RpcPeerStateKind {
  Connected = 0,
  JustDisconnected = 1,
  Disconnected = 2,
  JustConnected = 3,
}

export interface RpcPeerState {
  readonly kind: RpcPeerStateKind;
  readonly lastError?: Error;
  readonly reconnectsIn: number; // ms, 0 = no countdown
}

export function isConnected(state: RpcPeerState): boolean {
  return state.kind === RpcPeerStateKind.Connected || state.kind === RpcPeerStateKind.JustConnected;
}

export function likelyConnected(state: RpcPeerState): boolean {
  return state.kind !== RpcPeerStateKind.Disconnected;
}

export function getStateDescription(state: RpcPeerState, useLastError = false): string {
  switch (state.kind) {
    case RpcPeerStateKind.JustConnected:
      return "Just connected.";
    case RpcPeerStateKind.Connected:
      return "Connected.";
    case RpcPeerStateKind.JustDisconnected:
      return "Just disconnected, reconnecting...";
    case RpcPeerStateKind.Disconnected:
      if (state.reconnectsIn > 0)
        break; // fall through to error message below
      return "Reconnecting...";
  }
  if (state.lastError == null || !useLastError)
    return "Disconnected.";

  let message = state.lastError.message.trim();
  if (!(message.endsWith(".") || message.endsWith("!") || message.endsWith("?")))
    message += ".";
  return "Disconnected: " + message;
}
