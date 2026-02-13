import React from "react";
import { useComputedState, createTrackedDelayer } from "@actuallab/fusion-react";
import { AsyncContext } from "@actuallab/core";
import type { Todos } from "./todos.js";
import type { UIActionTracker } from "@actuallab/fusion-react";

interface Props {
  todos: Todos;
  tracker: UIActionTracker;
}

export function TodoSummaryBadge({ todos, tracker }: Props) {
  const delayer = React.useMemo(
    () => createTrackedDelayer(tracker, 1000),
    [tracker],
  );
  const { value: summary, isInitial } = useComputedState(
    () => {
      const ctx = AsyncContext.current;
      return todos.getSummary(ctx);
    },
    [todos],
    { updateDelayer: delayer },
  );

  if (isInitial || !summary) {
    return <span className="badge bg-secondary">Loading...</span>;
  }

  return (
    <>
      <span className="badge bg-success"><b>{summary.doneCount}</b> done</span>{" "}
      <span className="badge bg-primary"><b>{summary.count}</b> total</span>
    </>
  );
}
