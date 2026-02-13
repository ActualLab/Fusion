import React from "react";
import { useComputedState, useUICommander, type UIActionTracker } from "@actuallab/fusion-react";
import type { Todos } from "./todos.js";
import type { ITodoApiCommand } from "./todo-api.js";
import { DEFAULT_SESSION, ULID_EMPTY } from "./todo-api.js";
import { TodoItemView } from "./TodoItemView.js";
import { TodoSummaryBadge } from "./TodoSummaryBadge.js";

interface Props {
  todos: Todos;
  commandApi: ITodoApiCommand;
  tracker: UIActionTracker;
}

export function TodoApp({ todos, commandApi, tracker }: Props) {
  const [loadedCount, setLoadedCount] = React.useState(5);
  const [newTitle, setNewTitle] = React.useState("");
  const commander = useUICommander();

  const { value, isInitial } = useComputedState(
    () => todos.list(loadedCount),
    [todos, loadedCount],
  );

  const items = value?.items ?? [];
  const hasMore = value?.hasMore ?? false;

  const handleCreate = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newTitle.trim()) return;
    const title = newTitle.trim();
    setNewTitle("");
    void commander.run(async () => {
      await commandApi.AddOrUpdate({
        Session: DEFAULT_SESSION,
        Item: { Id: ULID_EMPTY, Title: title, IsDone: false },
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

        {isInitial && <p className="text-muted">Loading...</p>}

        {items.map((item) => (
          <TodoItemView key={item.Id} item={item} commandApi={commandApi} />
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
