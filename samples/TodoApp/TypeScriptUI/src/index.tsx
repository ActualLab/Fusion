import React from "react";
import { createRoot, type Root } from "react-dom/client";
import { FusionHub } from "@actuallab/fusion-rpc";
import { RpcClientPeer, RpcPeerStateMonitor } from "@actuallab/rpc";
import { TodoApiDef } from "./todo-api.js";
import type { ITodoApi } from "./todo-api.js";
import { SimpleServiceDef, SimpleClientSideServiceDef, createPongService } from "./simple-api.js";
import type { ISimpleService } from "./simple-api.js";
import { Todos } from "./todos.js";
import { TodoApp } from "./TodoApp.js";
import { RpcExampleApp } from "./RpcExampleApp.js";

// --- Module-level singletons (created once per page load) ---

function getWsUrl(): string {
  const loc = window.location;
  const protocol = loc.protocol === "https:" ? "wss:" : "ws:";
  return `${protocol}//${loc.host}/rpc/ws`;
}

const hub = new FusionHub();
const peer = new RpcClientPeer(hub, getWsUrl());
hub.addPeer(peer);

const api = hub.addClient<ITodoApi>(peer, TodoApiDef);
const simpleApi = hub.addClient<ISimpleService>(peer, SimpleServiceDef);
const todos = new Todos(api);
const monitor = new RpcPeerStateMonitor(peer);

// Register client-side service for Pong callbacks
const pongListeners = new Set<(message: string) => void>();
hub.addService(SimpleClientSideServiceDef, createPongService((msg) => {
  for (const listener of pongListeners) listener(msg);
}));

// Start the peer connection once
void peer.run();

// --- React mount/unmount (called by Blazor interop) ---

let root: Root | null = null;

const todoReactApp = {
  mount(elementId: string) {
    const container = document.getElementById(elementId);
    if (!container) {
      console.error(`TodoReactApp: element #${elementId} not found`);
      return;
    }

    root = createRoot(container);
    root.render(<TodoApp todos={todos} api={api} monitor={monitor} />);
  },

  unmount() {
    root?.unmount();
    root = null;
  },

  cancelReconnectDelays() {
    peer.reconnectDelayer.cancelDelays();
  },
};

// --- RPC Example React app mount/unmount (called by Blazor interop) ---

let rpcExampleRoot: Root | null = null;

const rpcExampleReactApp = {
  mount(elementId: string) {
    const container = document.getElementById(elementId);
    if (!container) {
      console.error(`RpcExampleReactApp: element #${elementId} not found`);
      return;
    }

    rpcExampleRoot = createRoot(container);
    rpcExampleRoot.render(
      <RpcExampleApp api={simpleApi} monitor={monitor} pongListeners={pongListeners} />
    );
  },

  unmount() {
    rpcExampleRoot?.unmount();
    rpcExampleRoot = null;
  },
};

// Expose to global scope for Blazor interop
(window as any).TodoReactApp = todoReactApp;
(window as any).RpcExampleReactApp = rpcExampleReactApp;
