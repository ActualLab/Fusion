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
