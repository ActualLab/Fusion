import React from "react";
import { useComputedState } from "@actuallab/fusion-react";
import { uiActions } from "@actuallab/fusion";
import { AsyncContext } from "@actuallab/core";
import type { RpcPeerStateMonitor } from "@actuallab/rpc";
import type { Todos } from "./todos.js";
import type { ITodoApi } from "./todo-api.js";
import { DEFAULT_SESSION, ULID_EMPTY } from "./todo-api.js";
import { TodoItemView } from "./TodoItemView.js";
import { TodoSummaryBadge } from "./TodoSummaryBadge.js";
import { ConnectionStatusBanner } from "./ConnectionStatusBanner.js";

interface Props {
  todos: Todos;
  api: ITodoApi;
  monitor: RpcPeerStateMonitor;
}

export function TodoApp({ todos, api, monitor }: Props) {
  const [loadedCount, setLoadedCount] = React.useState(5);
  const [newTitle, setNewTitle] = React.useState("");
  const [, forceRender] = React.useReducer(c => c + 1, 0);

  React.useEffect(() => {
    const handler = () => forceRender();
    uiActions.changed.add(handler);
    return () => uiActions.changed.remove(handler);
  }, []);

  const { value, isInitial } = useComputedState(
    () => {
      const ctx = AsyncContext.current;
      return todos.list(loadedCount, ctx);
    },
    [todos, loadedCount],
  );

  const items = value?.items ?? [];
  const hasMore = value?.hasMore ?? false;

  const handleCreate = (e: React.FormEvent) => {
    e.preventDefault();
    const title = newTitle.trim();
    setNewTitle("");
    uiActions.run(async () => {
      await api.AddOrUpdate({
        session: DEFAULT_SESSION,
        item: { id: ULID_EMPTY, title: title, isDone: false },
      });
    });
  };

  return (
    <>
      <h1>Todos - React + TypeScript Fusion</h1>
      <ConnectionStatusBanner monitor={monitor} />

      {uiActions.errors.map((err, i) => (
        <div key={i} className="alert alert-danger d-flex justify-content-between align-items-center">
          <span>{err instanceof Error ? err.message : String(err)}</span>
          <button type="button" className="btn-close" onClick={() => uiActions.dismissError(i)} />
        </div>
      ))}

      <div className="row">
        <div className="col-12 col-md-6">
          <p>
            <TodoSummaryBadge todos={todos} />
          </p>

          {isInitial && <p className="text-muted">Loading...</p>}

          {items.map((item) => (
            <TodoItemView key={item.id} item={item} api={api} />
          ))}

          {hasMore && (
            <button
              className="btn btn-primary my-3"
              onClick={() => setLoadedCount((c) => c * 2)}
            >
              Load {loadedCount} more <i className="fa fa-angle-double-down" />
            </button>
          )}

          <form onSubmit={handleCreate} className="my-3">
            <div className="input-group">
              <button type="submit" className="btn btn-primary">
                <i className="fa fa-plus-square" />
              </button>
              <input
                type="text"
                className="form-control"
                value={newTitle}
                onChange={(e) => setNewTitle(e.target.value)}
                placeholder="What needs to be done?"
              />
            </div>
          </form>
        </div>
      </div>
    </>
  );
}
