/** Error thrown for a failed remote RPC call — carries the remote exception's type name when available. */
export class RpcError extends Error {
    readonly typeName?: string;

    constructor(message: string, typeName?: string) {
        super(message);
        this.name = 'RpcError';
        if (typeName !== undefined)
            this.typeName = typeName;
    }
}
