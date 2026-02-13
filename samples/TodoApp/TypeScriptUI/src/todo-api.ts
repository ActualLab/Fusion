import { defineComputeService } from "@actuallab/fusion-rpc";
import { AsyncContext } from "@actuallab/core";

// Optional trailing AsyncContext enables explicit context propagation across awaits.
export interface ITodoApi {
    Get(session: string, id: string, ctx?: AsyncContext): Promise<TodoItem | null>;
    ListIds(session: string, count: number, ctx?: AsyncContext): Promise<string[]>;
    GetSummary(session: string, ctx?: AsyncContext): Promise<TodoSummary>;
    AddOrUpdate(command: Todos_AddOrUpdate): Promise<TodoItem>;
    Remove(command: Todos_Remove): Promise<void>;
}

export interface TodoItem {
  id: string; // Ulid as string
  title: string;
  isDone: boolean;
}

export interface TodoSummary {
  count: number;
  doneCount: number;
}

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

// Service definition — compute methods default to FUSION_CALL_TYPE_ID,
// commands override with callTypeId: 0
export const TodoApiDef = defineComputeService("ITodoApi", {
  Get: { args: ["", ""] },          // (session, id) → TodoItem?
  ListIds: { args: ["", 0] },       // (session, count) → string[]
  GetSummary: { args: [""] },       // (session) → TodoSummary
  AddOrUpdate: { args: [{}], callTypeId: 0 },  // (command) → TodoItem
  Remove: { args: [{}], callTypeId: 0 },       // (command) → void
});
