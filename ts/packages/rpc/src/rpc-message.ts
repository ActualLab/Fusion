/** Identifies the type of an RPC call on the wire. */
export const enum RpcCallTypeId {
  Regular = 0,
  Reliable = 1,
}

/** Identifies system call methods. */
export const RpcSystemCalls = {
  handshake: "$sys.Handshake",
  ok: "$sys.Ok",
  error: "$sys.Error",
  cancel: "$sys.Cancel",
  keepAlive: "$sys.KeepAlive",
  reconnect: "$sys.Reconnect",
  invalidate: "$sys-c.Invalidate",
} as const;

/** Wire-format RPC message envelope. */
export interface RpcMessage {
  CallType?: number;
  RelatedId?: number;
  Method?: string;
  Headers?: unknown[];
}

// Delimiters used in the wire format
export const ENVELOPE_DELIMITER = "\n";        // \x0A between envelope and args
export const ARG_DELIMITER = "\x1F";           // Unit Separator between args
export const FRAME_DELIMITER = "\n\x1E";       // \x0A\x1E between messages in a frame
