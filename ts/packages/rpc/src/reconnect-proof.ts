// Client half of the reconnect proof-of-possession protocol. The server mints a
// per-peer secret and delivers it inside its `$sys.Handshake`; every subsequent
// connect attempt carries a counter (`c`) plus an HMAC over it (`p`), so a leaked
// `clientId` alone can no longer evict or hijack a live peer.
//
// .NET counterpart: ActualLab.Rpc.Internal.RpcReconnectProof. The two MUST agree
// byte for byte, so both sides are pinned by the same test vector:
//   key     = UTF8(secret)                                -- the secret is an opaque
//                                                            token, NOT base64url-decoded
//   message = UTF8(clientId + "\n" + counterText)         -- "\n" is a single 0x0A
//   proof   = Base64Url_NoPad(HMAC_SHA256(key, message))  -- 32 bytes -> 43 chars

import { base64UrlEncode } from './base64.js';

const textEncoder = new TextEncoder();

/** The `c` / `p` query parameter pair for one connect attempt. */
export interface RpcReconnectProofParameters {
    counter: string;
    proof: string;
}

// `crypto.subtle` is undefined in an insecure browser context (plain http on a
// non-localhost host). A client there must connect without a proof rather than
// fail outright, so callers gate on this instead of catching a TypeError.
export function isReconnectProofSupported(): boolean {
    return typeof crypto !== 'undefined' && (crypto.subtle as SubtleCrypto | undefined) !== undefined;
}

// `counterText` must be the decimal counter exactly as it will appear in the URL:
// the server HMACs the `c` value string as it arrived, without reparsing it.
export async function computeReconnectProof(
    secret: string, clientId: string, counterText: string
): Promise<string> {
    const key = await crypto.subtle.importKey(
        'raw', textEncoder.encode(secret), { name: 'HMAC', hash: 'SHA-256' }, false, ['sign']);
    const signature = await crypto.subtle.sign(
        'HMAC', key, textEncoder.encode(`${clientId}\n${counterText}`));
    return base64UrlEncode(new Uint8Array(signature));
}
