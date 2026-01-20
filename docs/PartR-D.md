# ActualLab.Rpc: Diagrams

Diagrams for the RPC concepts introduced in [Part 2](PartR.md).


## RpcHub Architecture

```mermaid
flowchart TD
    subgraph RpcHub ["RpcHub (singleton)"]
        direction TB
        SR["ServiceRegistry"]
        SF["SerializationFormats"]
        Peers["Peers Collection"]
    end

    RpcHub --> DP["DefaultPeer<br/>(RpcClientPeer)"]
    RpcHub --> LP["LocalPeer<br/>(in-process)"]
    RpcHub --> LB["LoopbackPeer<br/>(tests only)"]

    Peers --> CP["RpcClientPeer<br/>(outbound)"]
    Peers --> SP["RpcServerPeer<br/>(inbound)"]
```

| Component | Description |
|-----------|-------------|
| `RpcHub` | Singleton orchestrator managing all peers and service proxies |
| `ServiceRegistry` | Registry of RPC service definitions |
| `DefaultPeer` | Default peer for outbound calls |
| `LocalPeer` | Peer for same-process calls |
| `LoopbackPeer` | In-process loopback for testing |


## RpcPeer Hierarchy

```mermaid
classDiagram
    direction LR
    RpcPeer <|-- RpcClientPeer
    RpcPeer <|-- RpcServerPeer

    class RpcPeer {
        <<abstract>>
        +Hub: RpcHub
        +Ref: RpcPeerRef
        +ConnectionState
        +InboundCalls
        +OutboundCalls
        +Send()
        +WhenConnected()
        +Disconnect()
    }
    class RpcClientPeer {
        +ClientId: string
        +ReconnectsAt: AsyncState
        Initiates connections
        Handles reconnection
    }
    class RpcServerPeer {
        +SetNextConnection()
        Waits for connections
        from server framework
    }
```

| Peer Type | Direction | Description |
|-----------|-----------|-------------|
| `RpcClientPeer` | Outbound | Initiates connections, handles reconnection with backoff |
| `RpcServerPeer` | Inbound | Receives connections from server framework (e.g., ASP.NET) |


## Connection State Machine

```mermaid
stateDiagram-v2
    direction LR
    [*] --> Disconnected
    Disconnected --> Connecting : GetConnection()
    Connecting --> Connected : Handshake OK
    Connecting --> Disconnected : Error
    Connected --> Disconnected : Connection lost
    Disconnected --> Connecting : Reconnect (backoff)
```

| State | Description |
|-------|-------------|
| Disconnected | No active connection |
| Connecting | Establishing connection and exchanging handshake |
| Connected | Active connection, can send/receive messages |


## RpcPeerRef Types

```mermaid
flowchart LR
    subgraph PeerRefs ["RpcPeerRef"]
        Default["RpcPeerRef.Default<br/>(remote)"]
        Local["RpcPeerRef.Local<br/>(same-process)"]
        Loopback["RpcPeerRef.Loopback<br/>(in-process test)"]
        Custom["RpcPeerRef.NewClient(host)<br/>(custom remote)"]
    end

    Default --> RCP["RpcClientPeer"]
    Local --> LocalPeer["LocalPeer"]
    Loopback --> LBPeer["LoopbackPeer"]
    Custom --> RCP
```

| PeerRef | ConnectionKind | Use Case |
|---------|----------------|----------|
| `Default` | Remote | Standard client-server communication |
| `Local` | Local | Same-process calls (no serialization) |
| `Loopback` | Loopback | Testing with serialization but no network |
| `NewClient(host)` | Remote | Connect to specific host |


## RPC Call Flow

```mermaid
sequenceDiagram
    participant Client
    participant ClientProxy as Client Proxy
    participant Transport as WebSocket
    participant ServerProxy as Server Proxy
    participant Service

    Client->>ClientProxy: service.Method(args)
    ClientProxy->>ClientProxy: Serialize args
    ClientProxy->>Transport: RpcMessage (request)
    Transport->>ServerProxy: RpcMessage
    ServerProxy->>ServerProxy: Deserialize args
    ServerProxy->>Service: Method(args)
    Service-->>ServerProxy: result
    ServerProxy->>ServerProxy: Serialize result
    ServerProxy->>Transport: RpcMessage (response)
    Transport->>ClientProxy: RpcMessage
    ClientProxy->>ClientProxy: Deserialize result
    ClientProxy-->>Client: result
```


## Compute Service Client Caching

```mermaid
flowchart TD
    Call["Client calls<br/>service.GetData()"] --> Check{"Cached<br/>Computed&lt;T&gt;?"}
    Check -->|"Yes & Consistent"| Return["Return cached value<br/>(no RPC)"]
    Check -->|"No or Inconsistent"| RPC["Make RPC call"]
    RPC --> Cache["Cache as<br/>Computed&lt;T&gt; replica"]
    Cache --> Return2["Return value"]

    subgraph Invalidation ["Server-Side Invalidation"]
        ServerInv["Server Computed&lt;T&gt;<br/>invalidated"] --> Notify["Send invalidation<br/>to client"]
        Notify --> ClientInv["Client replica<br/>marked Inconsistent"]
    end
```

| Scenario | Behavior |
|----------|----------|
| Cache hit (consistent) | Return immediately, no RPC |
| Cache miss | Make RPC, cache result |
| Server invalidation | Client replica invalidated, next call triggers RPC |


## Invalidation Propagation

```mermaid
sequenceDiagram
    participant Server as Server Computed
    participant Tracker as Call Tracker
    participant Transport as WebSocket
    participant Client as Client Replica

    Note over Server: Data changes
    Server->>Server: Invalidate()
    Server->>Tracker: Notify invalidation
    Tracker->>Transport: Invalidate(callId)
    Transport->>Client: Invalidate message
    Client->>Client: Mark Inconsistent
    Note over Client: Next call will fetch fresh data
```


## RpcStream Data Flow

```mermaid
flowchart LR
    subgraph Server
        AE["IAsyncEnumerable&lt;T&gt;"] --> Stream["RpcStream.New()"]
    end

    subgraph Transport
        Stream --> Chunks["Stream chunks<br/>via RpcMessage"]
        Acks["Ack messages"] --> Stream
    end

    subgraph Client
        Chunks --> Consume["await foreach<br/>(item in stream)"]
        Consume --> Acks
    end
```

| Direction | Description |
|-----------|-------------|
| Server → Client | Stream items sent in chunks |
| Client → Server | Acknowledgments for backpressure |


## RpcStream Backpressure

```mermaid
sequenceDiagram
    participant Producer as Server (Producer)
    participant Consumer as Client (Consumer)

    loop Until AckAdvance reached
        Producer->>Consumer: Item 1..N
    end
    Note over Producer: Pauses (AckAdvance=61)

    Consumer->>Producer: Ack (received 30)
    Note over Producer: Resumes sending

    loop Continue streaming
        Producer->>Consumer: More items
        Consumer->>Producer: Ack (every AckPeriod=30)
    end
```

| Property | Default | Description |
|----------|---------|-------------|
| `AckPeriod` | 30 | Consumer acks every N items |
| `AckAdvance` | 61 | Producer can send N items ahead before waiting |
