import { defineComputeService } from "@actuallab/fusion-rpc";
import { defineRpcService } from "@actuallab/rpc";

// Data types matching .NET TodoItem and TodoSummary

export interface TodoItem {
  Id: string;       // Ulid as string
  Title: string;
  IsDone: boolean;
}

export interface TodoSummary {
  Count: number;
  DoneCount: number;
}

// Command types matching .NET Todos_AddOrUpdate and Todos_Remove

export interface Todos_AddOrUpdate {
  Session: string;
  Item: TodoItem;
}

export interface Todos_Remove {
  Session: string;
  Id: string;
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
export interface ITodoApiCompute {
  Get(session: string, id: string): Promise<TodoItem | null>;
  ListIds(session: string, count: number): Promise<string[]>;
  GetSummary(session: string): Promise<TodoSummary>;
}

export interface ITodoApiCommand {
  AddOrUpdate(command: Todos_AddOrUpdate): Promise<TodoItem>;
  Remove(command: Todos_Remove): Promise<void>;
}
