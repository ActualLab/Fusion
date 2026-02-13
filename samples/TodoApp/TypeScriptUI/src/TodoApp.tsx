import React from "react";
import { useComputedState, useUICommander, type UIActionTracker } from "@actuallab/fusion-react";
import { AsyncContext } from "@actuallab/core";
import type { Todos } from "./todos.js";
import type { ITodoApi } from "./todo-api.js";
import { DEFAULT_SESSION, ULID_EMPTY } from "./todo-api.js";
import { TodoItemView } from "./TodoItemView.js";
import { TodoSummaryBadge } from "./TodoSummaryBadge.js";

interface Props {
  todos: Todos;
  api: ITodoApi;
  tracker: UIActionTracker;
}

export function TodoApp({ todos, api, tracker }: Props) {
  const [loadedCount, setLoadedCount] = React.useState(5);
  const [newTitle, setNewTitle] = React.useState("");
  const commander = useUICommander();
  const [, forceRender] = React.useReducer(c => c + 1, 0);

  React.useEffect(() => {
    const handler = () => forceRender();
    tracker.changed.add(handler);
    return () => tracker.changed.remove(handler);
  }, [tracker]);

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
    commander.run(async () => {
      await api.AddOrUpdate({
        session: DEFAULT_SESSION,
        item: { id: ULID_EMPTY, title: title, isDone: false },
      });
    });
  };

  return (
    <div className="row">
      <div className="col-12 col-md-6">
        <h1>Todos - React + TypeScript Fusion</h1>

        <p>
          <TodoSummaryBadge todos={todos} tracker={tracker} />
        </p>

        {tracker.errors.map((err, i) => (
          <div key={i} className="alert alert-danger d-flex justify-content-between align-items-center">
            <span>{err instanceof Error ? err.message : String(err)}</span>
            <button type="button" className="btn-close" onClick={() => tracker.dismissError(i)} />
          </div>
        ))}

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
  );
}
