import { defineRpcService, RpcType } from "@actuallab/rpc";

// ISimpleService wire contract — maps to .NET's ISimpleService : IRpcService.
// Wire format uses camelCase (.NET's JsonNamingPolicy.CamelCase).

export interface ISimpleService {
    Greet(name: string): Promise<string>;
    GetTable(title: string): Promise<Table<number>>;
    Ping(message: string): void;  // noWait
}

export interface Table<T> {
  title: string;
  rows: AsyncIterable<Row<T>>;  // resolved from stream ref string
}

export interface Row<T> {
  index: number;
  items: AsyncIterable<T>;  // resolved from stream ref string
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
