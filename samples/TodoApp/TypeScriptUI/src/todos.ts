import { computeMethod } from "@actuallab/fusion";
import { AsyncContext } from "@actuallab/core";
import type { ITodoApi, TodoItem, TodoSummary } from "./todo-api.js";
import { DEFAULT_SESSION } from "./todo-api.js";

/**
 * Client-side compute service — mirrors UI/Services/Todos.cs.
 * Composes server API calls into higher-level computed methods
 * that participate in Fusion's dependency graph.
 */
export class Todos {
  private api: ITodoApi;

  constructor(api: ITodoApi) {
    this.api = api;
  }

  @computeMethod
  async list(count: number, ctx?: AsyncContext): Promise<{ items: TodoItem[]; hasMore: boolean }> {
    ctx ??= AsyncContext.current;
    const ids = await this.api.ListIds(DEFAULT_SESSION, count + 1, ctx);
    const hasMore = ids.length > count;
    const idsToFetch = hasMore ? ids.slice(0, count) : ids;

    const items: TodoItem[] = [];
    for (const id of idsToFetch) {
      const item = await this.api.Get(DEFAULT_SESSION, id, ctx);
      if (item !== null)
        items.push(item);
    }
    return { items, hasMore };
  }

  @computeMethod
  async getSummary(ctx?: AsyncContext): Promise<TodoSummary> {
    ctx ??= AsyncContext.current;
    return await this.api.GetSummary(DEFAULT_SESSION, ctx);
  }
}
