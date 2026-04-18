// Ref composition helpers for RPC peers.
//
// `RpcClientPeer`'s ref is its full WebSocket URL. The serialization format is
// encoded as the `f=` query parameter (see RpcClientPeer ctor).
// `RpcServerPeer`'s ref is `server://{id}`.
//
// Typical use:
//     rpcHub.defaultPeerUrl = RpcPeerRefBuilder.forClient('wss://host/rpc/ws', 'msgpack6');
//     const serverRef = RpcPeerRefBuilder.forServer(crypto.randomUUID());

export class RpcPeerRefBuilder {
    /** Build a client peer ref from a WebSocket URL, optionally baking in the
     *  serialization format as a `f=` query parameter.
     *  @param url Base WebSocket URL, e.g. `wss://host/rpc/ws`.
     *  @param serializationFormat Optional format key, e.g. `msgpack6`. When
     *         provided, replaces any existing `f=` in the URL. */
    static forClient(url: string, serializationFormat?: string): string {
        if (serializationFormat === undefined) return url;
        const u = new URL(url);
        u.searchParams.set('f', serializationFormat);
        return u.toString();
    }

    /** Build a server peer ref from a peer id — returns `server://{id}`. */
    static forServer(id: string): string {
        return `server://${id}`;
    }
}
