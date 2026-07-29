import React from "react";
import { toRpcStream } from "@actuallab/rpc";
import type { RpcPeer, RpcPeerStateMonitor } from "@actuallab/rpc";
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
  peer: RpcPeer;  // toRpcStream needs it to bind a nested stream reference
  monitor: RpcPeerStateMonitor;
  pongListeners: Set<(message: string) => void>;
}

export function RpcExampleApp({ api, peer, monitor, pongListeners }: Props) {
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

        await readTable(tableResult, peer, model, signal, triggerRender);
      } catch (err) {
        if (!signal.aborted) console.error("RPC Example error:", err);
      }
    })();

    return () => abortController.abort();
  }, [api, peer, triggerRender]);

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
  peer: RpcPeer,
  model: TableModel,
  signal: AbortSignal,
  triggerRender: () => void,
): Promise<void> {
  // Since v14.2 a stream nested in an ordinary result is no longer auto-resolved:
  // only the caller knows which fields carry streams, so it converts them itself
  const rows = toRpcStream<Row<number>>(table.rows, peer);
  if (rows === null) {
    console.error("GetTable returned no stream reference for rows:", table.rows);
    model.isCompleted = true;
    triggerRender();
    return;
  }

  let rowCount = 0;
  for await (const row of rows) {
    if (signal.aborted) break;
    const rowModel: RowModel = { index: row.index, items: [], isCompleted: false };
    model.rows.push(rowModel);
    triggerRender();

    // Read items for this row concurrently
    void readRow(row, peer, rowModel, signal, triggerRender);

    rowCount++;
    if (rowCount >= ROW_LIMIT) break;
  }
  model.isCompleted = true;
  triggerRender();
}

async function readRow(
  row: Row<number>,
  peer: RpcPeer,
  model: RowModel,
  signal: AbortSignal,
  triggerRender: () => void,
): Promise<void> {
  const items = toRpcStream<number>(row.items, peer);
  if (items === null) {
    console.error(`Row ${model.index} carried no stream reference:`, row.items);
    model.isCompleted = true;
    triggerRender();
    return;
  }

  try {
    let itemCount = 0;
    for await (const item of items) {
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
