import { defineRpcService } from "@actuallab/rpc";

// ISimpleService wire contract — maps to .NET's ISimpleService : IRpcService.
// Wire format uses camelCase (.NET's JsonNamingPolicy.CamelCase).
//
// Not using ctOffset here because Ping has no CancellationToken parameter.
// Methods that have CT on the .NET side include a null placeholder arg instead.

export interface Table<T> {
  title: string;
  rows: AsyncIterable<Row<T>>;  // resolved from stream ref string
}

export interface Row<T> {
  index: number;
  items: AsyncIterable<T>;  // resolved from stream ref string
}

export interface ISimpleService {
  Greet(name: string): Promise<string>;
  GetTable(title: string): Promise<Table<number>>;
  Ping(message: string): void;  // noWait
}

export const SimpleServiceDef = defineRpcService("ISimpleService", {
  Greet: { args: ["", null] },          // (name, CT) → wire :2
  GetTable: { args: ["", null] },       // (title, CT) → wire :2
  Ping: { args: [""], noWait: true },   // (message)   → wire :1  — no CT
});

// ISimpleClientSideService — client-side service that receives Pong calls from the server.
// Pong has no CancellationToken → wire :1
export const SimpleClientSideServiceDef = defineRpcService("ISimpleClientSideService", {
  Pong: { args: [""], noWait: true },   // (message) → wire :1
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
