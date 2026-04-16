/**
 * Minimal base64 ↔ Uint8Array helpers used by the `$sys.Reconnect` protocol
 * (byte arrays are encoded as base64 strings on the JSON wire to match
 * .NET's default `System.Text.Json` serialization of `byte[]`).
 */

export function base64Encode(bytes: Uint8Array): string {
    // btoa works on latin1 strings; chunk to avoid "Maximum call stack size exceeded"
    // when building the intermediate string for large inputs.
    const chunkSize = 0x8000;
    let binary = '';
    for (let i = 0; i < bytes.length; i += chunkSize) {
        binary += String.fromCharCode.apply(
            null,
            bytes.subarray(i, Math.min(bytes.length, i + chunkSize)) as unknown as number[],
        );
    }
    return btoa(binary);
}

export function base64Decode(encoded: string): Uint8Array {
    const binary = atob(encoded);
    const out = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) out[i] = binary.charCodeAt(i);
    return out;
}
