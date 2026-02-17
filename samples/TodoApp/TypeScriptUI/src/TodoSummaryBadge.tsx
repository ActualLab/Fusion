import React from "react";
import { useComputedState, UIUpdateDelayer } from "@actuallab/fusion-react";
import { AsyncContext } from "@actuallab/core";
import type { Todos } from "./todos.js";

interface Props {
  todos: Todos;
}

const delayer = UIUpdateDelayer.get(1000);

export function TodoSummaryBadge({ todos }: Props) {
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
