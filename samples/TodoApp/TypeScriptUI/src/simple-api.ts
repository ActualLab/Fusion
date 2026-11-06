// ISimpleService, Table, Row and the service definitions are generated from the .NET contracts
// into contracts.generated.ts. Nested streams (Table.rows, Row.items) arrive as raw stream
// references - convert them with toRpcStream(value, peer); see RpcExampleApp.readTable/readRow.

/** Callback handler for Pong messages from the server. */
export type PongHandler = (message: string) => void;

/** Creates a service implementation that forwards Pong calls to the handler. */
export function createPongService(onPong: PongHandler) {
  return {
    Pong(message: unknown) {
      onPong(message as string);
    },
  };
}
