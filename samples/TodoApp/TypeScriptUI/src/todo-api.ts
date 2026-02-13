import { defineComputeService } from "@actuallab/fusion-rpc";
import { defineRpcService } from "@actuallab/rpc";
import { AsyncContext } from "@actuallab/core";

// Data types matching .NET TodoItem and TodoSummary (camelCase — server uses JsonNamingPolicy.CamelCase)

export interface TodoItem {
  id: string;       // Ulid as string
  title: string;
  isDone: boolean;
}

export interface TodoSummary {
  count: number;
  doneCount: number;
}

// Command types matching .NET Todos_AddOrUpdate and Todos_Remove

export interface Todos_AddOrUpdate {
  session: string;
  item: TodoItem;
}

export interface Todos_Remove {
  session: string;
  id: string;
}

// Session.Default.Id = "~"
export const DEFAULT_SESSION = "~";

// Ulid.Empty as string (26 zeros)
export const ULID_EMPTY = "00000000000000000000000000";

// Compute service definition (queries) — ITodoApi compute methods
export const TodoApiComputeDef = defineComputeService("ITodoApi", {
  Get: { args: ["", ""] },          // (session, id) → TodoItem?
  ListIds: { args: ["", 0] },       // (session, count) → string[]
  GetSummary: { args: [""] },       // (session) → TodoSummary
});

// Command service definition (mutations) — ITodoApi command methods
// Commands use callTypeId: 0 (regular RPC, not compute)
export const TodoApiCommandDef = defineRpcService("ITodoApi", {
  AddOrUpdate: { args: [{}] },      // (Todos_AddOrUpdate) → TodoItem
  Remove: { args: [{}] },           // (Todos_Remove) → void
}, { ctOffset: 1 });

// Client interface for type safety
// Optional trailing AsyncContext enables explicit context propagation across awaits.
export interface ITodoApiCompute {
  Get(session: string, id: string, ctx?: AsyncContext): Promise<TodoItem | null>;
  ListIds(session: string, count: number, ctx?: AsyncContext): Promise<string[]>;
  GetSummary(session: string, ctx?: AsyncContext): Promise<TodoSummary>;
}

export interface ITodoApiCommand {
  AddOrUpdate(command: Todos_AddOrUpdate): Promise<TodoItem>;
  Remove(command: Todos_Remove): Promise<void>;
}
