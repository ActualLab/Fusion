// .NET counterparts:
//   RpcMessageSerializer (abstract) — serialises a complete outbound message (method
//     ref + headers + argument data) into a frame.  Has binary (V4/V5 compact) and
//     text (V3) implementations.
//   RpcArgumentSerializer (abstract) — serialises/deserialises individual argument
//     lists using MessagePack, MemoryPack, or System.Text.Json.
//   RpcTextMessageSerializerV3 — the text-based wire format: JSON envelope line,
//     delimiter, JSON-encoded argument segments.
//
// Omitted from .NET:
//   - Polymorphic argument handling — .NET's json5/njson5 formats inspect each
//     argument's runtime type and may wrap it with type info for polymorphic
//     deserialization.  TS uses plain JSON.stringify (no static type info),
//     which matches the json5np/njson5np "no polymorphism" formats.
//   - Binary serialization (RpcByteMessageSerializer, MessagePack, MemoryPack) —
//     not available in browsers; JSON is the only format TS supports.
//   - ArgumentData / ReadOnlyMemory<byte> lazy deserialization — .NET defers
//     deserializing argument bytes until the call handler runs (important for
//     zero-copy binary paths).  TS deserializes eagerly since JSON.parse is cheap
//     and there's no memory-ownership concern in JS.
//   - RpcSerializationFormat / RpcSerializationFormatResolver — the protocol
//     negotiation that picks byte-vs-text, V3-vs-V4-vs-V5.  TS speaks text-V3
//     only; no negotiation needed.
//   - Size limits (MaxArgumentDataSize) — .NET enforces 130 MB caps.  TS relies
//     on the WebSocket library's built-in frame limits; adding an explicit cap
//     would be straightforward but isn't necessary for the current client use case.

import type { RpcMessage } from "./rpc-message.js";
import {
  ENVELOPE_DELIMITER,
  ARG_DELIMITER,
  FRAME_DELIMITER,
} from "./rpc-message.js";

/** Serializes an RpcMessage + args into the json5 wire format. */
export function serializeMessage(message: RpcMessage, args?: unknown[]): string {
  const envelope = JSON.stringify(message);
  if (args === undefined || args.length === 0) {
    return envelope + ENVELOPE_DELIMITER;
  }
  const argsStr = args.map((a) => JSON.stringify(a)).join(ARG_DELIMITER);
  return envelope + ENVELOPE_DELIMITER + argsStr;
}

/** Serializes multiple messages into a single WebSocket frame. */
export function serializeFrame(messages: string[]): string {
  return messages.join(FRAME_DELIMITER);
}

/** Splits a WebSocket text frame into individual message strings. */
export function splitFrame(frame: string): string[] {
  return frame.split(FRAME_DELIMITER);
}

/** Deserializes a single message string into envelope + args. */
export function deserializeMessage(raw: string): { message: RpcMessage; args: unknown[] } {
  const nlIndex = raw.indexOf(ENVELOPE_DELIMITER);
  if (nlIndex === -1) {
    return { message: JSON.parse(raw) as RpcMessage, args: [] };
  }

  const envelopeStr = raw.substring(0, nlIndex);
  const argsStr = raw.substring(nlIndex + 1);
  const message = JSON.parse(envelopeStr) as RpcMessage;

  if (argsStr.length === 0) {
    return { message, args: [] };
  }

  const args = argsStr.split(ARG_DELIMITER).map((s) => {
    try {
      return JSON.parse(s) as unknown;
    } catch {
      return s; // Return raw string if not valid JSON
    }
  });

  return { message, args };
}
