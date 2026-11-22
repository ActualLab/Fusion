// Ref composition helpers for RPC peers.
//
// `RpcClientPeer`'s ref is its full WebSocket URL. The wire format is
// encoded as the `f=` query parameter (see RpcClientPeer ctor).
// `RpcServerPeer`'s ref is `server://{id}`.
//
// Typical use:
//     rpcHub.defaultPeerUrl = RpcRefBuilder.forClient('wss://host/rpc/ws', 'msgpack6');
//     const serverRef = RpcRefBuilder.forServer(crypto.randomUUID());

export class RpcRefBuilder {
    /** Build a client peer ref from a WebSocket URL, optionally baking in the
     *  wire format as a `f=` query parameter.
     *  @param url Base WebSocket URL, e.g. `wss://host/rpc/ws`.
     *  @param wireFormat Optional format key, e.g. `msgpack6`. When
     *         provided, replaces any existing `f=` in the URL. */
    static forClient(url: string, wireFormat?: string): string {
        if (wireFormat === undefined) return url;
        const u = new URL(url);
        u.searchParams.set('f', wireFormat);
        return u.toString();
    }

    /** Build a server peer ref from a peer id — returns `server://{id}`. */
    static forServer(id: string): string {
        return `server://${id}`;
    }
}
