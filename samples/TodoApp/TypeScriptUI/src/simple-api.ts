import { defineRpcService, RpcType } from "@actuallab/rpc";

// ISimpleService wire contract — maps to .NET's ISimpleService : IRpcService.
// Wire format uses camelCase (.NET's JsonNamingPolicy.CamelCase).

export interface ISimpleService {
    Greet(name: string): Promise<string>;
    GetTable(title: string): Promise<Table<number>>;
    Ping(message: string): void;  // noWait
}

// Nested streams arrive as the raw wire value - a stream reference, not a live stream.
// Convert them with toRpcStream(value, peer); see RpcExampleApp.readTable/readRow.
export interface Table<T> {
  title: string;
  rows: unknown;  // -> RpcStream<Row<T>>
}

export interface Row<T> {
  index: number;
  items: unknown;  // -> RpcStream<T>
}

export const SimpleServiceDef = defineRpcService("ISimpleService", {
  Greet: { args: [""] },
  GetTable: { args: [""] },
  Ping: { args: [""], returns: RpcType.noWait, wireArgCount: 1 },  // no CT
});

export const SimpleClientSideServiceDef = defineRpcService("ISimpleClientSideService", {
  Pong: { args: [""], returns: RpcType.noWait, wireArgCount: 1 },  // no CT
});

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
