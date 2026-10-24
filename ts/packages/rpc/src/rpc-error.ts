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

// .NET exception types a peer may answer a call with that mean "cancelled" (all derive from
// OperationCanceledException). Such an answer never carries a cacheable value.
const CANCELLATION_TYPE_NAMES = new Set([
    'System.OperationCanceledException',
    'System.Threading.Tasks.TaskCanceledException',
    'ActualLab.Rpc.RpcRerouteException',
]);

export function isCancellationTypeName(typeName: string | undefined): boolean {
    return typeName !== undefined && CANCELLATION_TYPE_NAMES.has(typeName);
}

// Assembly-qualified TypeRef of ActualLab's RemoteException — the type every
// ActualLab peer can reconstruct from a single (string message) ctor. TS uses
// it whenever it reports a JS error to a .NET peer ($sys.Error, stream $sys.End),
// folding the JS error name into the message for provenance (see decision D3).
export const REMOTE_EXCEPTION_TYPE_REF = 'ActualLab.Serialization.RemoteException, ActualLab.Core';

/** Wire shape of .NET `ExceptionInfo` for a JS error sent to a .NET peer. */
export interface RpcExceptionInfo {
    TypeRef: string;
    Message: string;
}

/** Reshapes what a remote peer gets to see of a local error. */
export type RpcErrorFilter = (error: unknown, info: RpcExceptionInfo) => RpcExceptionInfo;

/** Opt-in {@link RpcErrorFilter} that hides the original message — Node error
 *  text routinely embeds absolute paths, connection strings and SQL. */
export const genericErrorFilter: RpcErrorFilter = () => ({
    TypeRef: REMOTE_EXCEPTION_TYPE_REF,
    Message: 'Error: An error occurred while processing the request.',
});

export function toExceptionInfo(error: unknown, filter?: RpcErrorFilter): RpcExceptionInfo {
    const name = error instanceof Error ? error.name : 'Error';
    const message = error instanceof Error ? error.message : String(error);
    const info: RpcExceptionInfo = {
        TypeRef: REMOTE_EXCEPTION_TYPE_REF,
        Message: `${name}: ${message}`,
    };
    return filter === undefined ? info : filter(error, info);
}
