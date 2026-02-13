import React from "react";
import { useUICommander } from "@actuallab/fusion-react";
import type { TodoItem, ITodoApiCommand } from "./todo-api.js";
import { DEFAULT_SESSION } from "./todo-api.js";

interface Props {
  item: TodoItem;
  commandApi: ITodoApiCommand;
}

export function TodoItemView({ item, commandApi }: Props) {
  const commander = useUICommander();
  const [editTitle, setEditTitle] = React.useState(item.title);
  const debounceTimer = React.useRef<ReturnType<typeof setTimeout> | null>(null);

  // Sync editTitle when the item changes from server
  React.useEffect(() => {
    setEditTitle(item.title);
  }, [item.title]);

  const toggleDone = () => {
    void commander.run(async () => {
      await commandApi.AddOrUpdate({
        session: DEFAULT_SESSION,
        item: { ...item, isDone: !item.isDone },
      });
    });
  };

  const handleTitleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const newTitle = e.target.value;
    setEditTitle(newTitle);

    if (debounceTimer.current) clearTimeout(debounceTimer.current);
    debounceTimer.current = setTimeout(() => {
      const trimmed = newTitle.trim();
      if (trimmed && trimmed !== item.title) {
        void commander.run(async () => {
          await commandApi.AddOrUpdate({
            session: DEFAULT_SESSION,
            item: { ...item, title: trimmed },
          });
        });
      }
    }, 500);
  };

  const remove = () => {
    void commander.run(async () => {
      await commandApi.Remove({
        session: DEFAULT_SESSION,
        id: item.id,
      });
    });
  };

  return (
    <div className="input-group my-1">
      <span className="input-group-text" onClick={toggleDone} style={{ cursor: "pointer" }}>
        <i className={`fa fa-${item.isDone ? "check-square" : "square"}`} />
      </span>
      <input
        type="text"
        className="form-control"
        value={editTitle}
        onChange={handleTitleChange}
      />
      <button className="btn btn-warning" onClick={remove}>
        <i className="fa fa-minus" />
      </button>
    </div>
  );
}
