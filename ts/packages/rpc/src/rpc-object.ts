export interface RpcObjectId {
  readonly hostId: string;
  readonly localId: number;
}

export const RpcObjectKind = { Local: 0, Remote: 1 } as const;
export type RpcObjectKind = (typeof RpcObjectKind)[keyof typeof RpcObjectKind];

export interface IRpcObject {
  readonly id: RpcObjectId;
  readonly kind: RpcObjectKind;
  reconnect(): void;
  disconnect(): void;
}
