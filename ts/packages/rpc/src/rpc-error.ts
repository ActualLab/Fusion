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

// Assembly-qualified TypeRef of ActualLab's RemoteException — the type every
// ActualLab peer can reconstruct from a single (string message) ctor. TS uses
// it whenever it reports a JS error to a .NET peer ($sys.Error, stream $sys.End),
// folding the JS error name into the message for provenance (see decision D3).
export const REMOTE_EXCEPTION_TYPE_REF = 'ActualLab.Serialization.RemoteException, ActualLab.Core';

/** Wire shape of .NET `ExceptionInfo` for a JS error sent to a .NET peer. */
export function toExceptionInfo(error: unknown): { TypeRef: string; Message: string } {
    const name = error instanceof Error ? error.name : 'Error';
    const message = error instanceof Error ? error.message : String(error);
    return { TypeRef: REMOTE_EXCEPTION_TYPE_REF, Message: `${name}: ${message}` };
}
