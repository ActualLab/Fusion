// .NET counterparts:
//   RpcMessage — .NET has no single "message" type; the wire format is built by
//     RpcMessageSerializer / RpcOutboundMessage / RpcInboundMessage.  In .NET the
//     envelope carries MethodRef, RelatedId, Headers, CallTypeId, and serialized
//     argument data as binary or text segments.  In TS we use a simpler JSON
//     envelope + delimiter-separated args.
//
// Omitted from .NET:
//   - RpcCallTypeId Reliable (stage-based reconnection protocol) — the TS client
//     is browser-only; reconnection replays entire calls rather than resuming at
//     a stage, so Reliable call type adds no value.
//   - RpcHeader / RpcHeaders — .NET passes typed header arrays on every message
//     (Hash, ActivityContext, etc.).  TS has no RPC caching, OpenTelemetry
//     propagation, or middleware pipeline, so headers aren't needed yet.  The
//     Headers field on RpcMessage is reserved for future use.
//   - $sys.M (Match) / $sys.NotFound / $sys.Disconnect / Stream calls
//     (Ack, AckEnd, I, B, End) — Match is for hash-based response caching
//     (bandwidth optimisation); NotFound is a server-side "service not registered"
//     response; Disconnect is for RpcSharedObject lifetime management; the stream
//     calls are for RpcStream (server→client IAsyncEnumerable).  None of these
//     are needed in the TS client today because:
//       * No response caching → no Match.
//       * Service-not-found is sent as $sys.Error.
//       * No shared-object tracking (server-side concept) → no Disconnect.
//       * No RpcStream yet → no Ack/AckEnd/I/B/End.
//     They can be added when streaming or caching is ported.
//   - Binary serialization variants (RpcByteMessageSerializer) — TS uses
//     text (JSON) exclusively; binary MessagePack/MemoryPack require native
//     codecs that aren't available in browsers.

/** Identifies the type of an RPC call on the wire. */
export const enum RpcCallTypeId {
  Regular = 0,
  Reliable = 1,
}

/** Identifies system call methods — names include :argCount suffix for .NET interop. */
export const RpcSystemCalls = {
  handshake: "$sys.Handshake:1",
  ok: "$sys.Ok:1",
  error: "$sys.Error:1",
  cancel: "$sys.Cancel:0",
  keepAlive: "$sys.KeepAlive:1",
  reconnect: "$sys.Reconnect:3",
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
