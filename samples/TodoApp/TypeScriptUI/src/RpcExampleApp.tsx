import React from "react";
import type { RpcPeerStateMonitor } from "@actuallab/rpc";
import type { ISimpleService, Table, Row } from "./simple-api.js";
import { ConnectionStatusBanner } from "./ConnectionStatusBanner.js";

const ROW_LIMIT = 16;
const ITEM_LIMIT = 16;
const PING_INTERVAL_MS = 500;

interface RowModel {
  index: number;
  items: number[];
  isCompleted: boolean;
}

interface TableModel {
  title: string;
  rows: RowModel[];
  isCompleted: boolean;
}

interface Props {
  api: ISimpleService;
  monitor: RpcPeerStateMonitor;
  pongListeners: Set<(message: string) => void>;
}

export function RpcExampleApp({ api, monitor, pongListeners }: Props) {
  const [greeting, setGreeting] = React.useState("");
  const [table, setTable] = React.useState<TableModel | null>(null);
  const [lastPing, setLastPing] = React.useState("");
  const [lastPong, setLastPong] = React.useState("");

  const [, forceRender] = React.useReducer(c => c + 1, 0);

  const triggerRender = React.useCallback(() => forceRender(), []);

  // Ping-pong loop
  React.useEffect(() => {
    let cancelled = false;
    let pingIndex = 0;

    // Listen for Pong callbacks from the server
    const onPong = (message: string) => {
      if (!cancelled) setLastPong(message);
    };
    pongListeners.add(onPong);

    void (async () => {
      while (!cancelled) {
        pingIndex++;
        const message = `Ping ${pingIndex}`;
        setLastPing(message);
        setLastPong("");
        try {
          api.Ping(message);
        } catch {
          // NoWait calls may fail silently if disconnected
        }
        await delay(PING_INTERVAL_MS);
      }
    })();

    return () => {
      cancelled = true;
      pongListeners.delete(onPong);
    };
  }, [api, pongListeners]);

  // Greet + GetTable
  React.useEffect(() => {
    const abortController = new AbortController();
    const signal = abortController.signal;

    void (async () => {
      try {
        const [greetResult, tableResult] = await Promise.all([
          api.Greet("Fusion explorer"),
          api.GetTable("Streamed table"),
        ]);

        if (signal.aborted) return;
        setGreeting(greetResult);

        const model: TableModel = {
          title: tableResult.title,
          rows: [],
          isCompleted: false,
        };
        setTable(model);

        await readTable(tableResult, model, signal, triggerRender);
      } catch (err) {
        if (!signal.aborted) console.error("RPC Example error:", err);
      }
    })();

    return () => abortController.abort();
  }, [api, triggerRender]);

  return (
    <>
      <h1>RPC Example - React</h1>
      <ConnectionStatusBanner monitor={monitor} />

      <div className="my-1">
        Greet: <b>{greeting || "..."}</b>
      </div>
      <div className="my-1">
        Ping-pong: <b>{lastPing}</b> - <b>{lastPong}</b>
      </div>

      {table && (
        <>
          <div className="my-1">
            GetTable: <b>{table.title}</b>
            {table.isCompleted && (
              <span className="text-secondary"> - completed</span>
            )}
          </div>
          {table.rows.map((row) => (
            <div key={row.index} className="mx-2">
              <span>
                Row <b>{row.index}</b>:
              </span>{" "}
              <span>{row.items.join(", ")}</span>
              {row.isCompleted && (
                <span className="text-secondary"> - completed</span>
              )}
            </div>
          ))}
        </>
      )}
    </>
  );
}

function delay(ms: number): Promise<void> {
  return new Promise(r => setTimeout(r, ms));
}

async function readTable(
  table: Table<number>,
  model: TableModel,
  signal: AbortSignal,
  triggerRender: () => void,
): Promise<void> {
  let rowCount = 0;
  for await (const row of table.rows) {
    if (signal.aborted) break;
    const rowModel: RowModel = { index: row.index, items: [], isCompleted: false };
    model.rows.push(rowModel);
    triggerRender();

    // Read items for this row concurrently
    void readRow(row, rowModel, signal, triggerRender);

    rowCount++;
    if (rowCount >= ROW_LIMIT) break;
  }
  model.isCompleted = true;
  triggerRender();
}

async function readRow(
  row: Row<number>,
  model: RowModel,
  signal: AbortSignal,
  triggerRender: () => void,
): Promise<void> {
  try {
    let itemCount = 0;
    for await (const item of row.items) {
      if (signal.aborted) break;
      model.items.push(item);
      triggerRender();
      itemCount++;
      if (itemCount >= ITEM_LIMIT) break;
    }
  } catch (err) {
    if (!signal.aborted) console.error(`Row ${model.index} stream error:`, err);
  }
  model.isCompleted = true;
  triggerRender();
}
