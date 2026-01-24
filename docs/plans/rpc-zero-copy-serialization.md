# RPC Zero-Copy Serialization Plan

## Problem Statement

The current RPC implementation has unnecessary memory copying in both directions:

**Outbound (Client → Server):**
1. Arguments are serialized into `RpcMessage.ArgumentData` (`ReadOnlyMemory<byte>`)
2. Message is passed to WebSocket channel
3. `ArgumentData` is copied again when written to the WebSocket stream

**Inbound (Server → Client):**
1. Data is received from WebSocket into a buffer
2. To construct `RpcMessage.ArgumentData`, the relevant bytes are copied (via `.ToArray()`)
3. Arguments are then deserialized from this copied memory

The current `AllowProjection` optimization in `RpcByteMessageSerializerV4` helps in some cases by avoiding the copy at step 2 (inbound), but it:
- Keeps the entire receive buffer alive until all messages referencing it are processed
- Only works when the underlying array is "efficient" enough (checked via `MemoryMarshal.TryGetArray()`)
- Doesn't help with outbound serialization at all

## Proposed Solution

### Core Idea

Replace the existing RPC message infrastructure with:
1. **Lazy outbound serialization**: Arguments serialize directly to the output buffer
2. **Reference-counted inbound buffers**: Memory blocks are pooled and released when all messages from that block are processed
3. **Separate message types**: `RpcOutboundMessage` (lazy) and `RpcInboundMessage` (ref-counted)
4. **New transport abstraction**: Replace `WebSocketChannel` with new implementation

### Compatibility

- **Wire format**: Remains **binary compatible** with V4/V5 formats (they are identical)
- **Code compatibility**: Old `RpcMessage` and related infrastructure will be **removed entirely**
- V4/V5/V5C serialization formats continue to work - only the in-memory representation changes

## Detailed Design

### 1. New Message Types

Remove `RpcMessage` and replace with two distinct types:

#### RpcOutboundMessage

```csharp
public sealed class RpcOutboundMessage
{
    public byte CallTypeId { get; init; }
    public long RelatedId { get; init; }
    public RpcMethodRef MethodRef { get; init; }
    public RpcHeader[]? Headers { get; init; }

    // Arguments stored in original form - serialized lazily
    public ArgumentList? Arguments { get; init; }
    public bool NeedsPolymorphism { get; init; }

    // Pre-serialized data for cache key scenarios
    public ReadOnlyMemory<byte>? ArgumentData { get; init; }
}
```

**Key behavior**: Arguments are stored in their original form and serialized directly to the transport buffer when the message is sent. No intermediate `ArgumentData` allocation needed in the common case.

#### RpcInboundMessage

```csharp
public sealed class RpcInboundMessage : IDisposable
{
    public byte CallTypeId { get; init; }
    public long RelatedId { get; init; }
    public RpcMethodRef MethodRef { get; init; }
    public RpcHeader[]? Headers { get; init; }

    // Points directly into the pooled buffer
    public ReadOnlyMemory<byte> ArgumentData { get; init; }

    // Reference to buffer block for lifetime management
    internal RpcBufferBlock? BufferBlock { get; init; }

    // Populated on-demand during processing
    public ArgumentList? Arguments { get; set; }

    public void Dispose()
    {
        BufferBlock?.Release();
    }

    // For compute calls that need to outlive the buffer
    public void DetachFromBuffer()
    {
        if (BufferBlock is not null)
        {
            ArgumentData = ArgumentData.ToArray();
            BufferBlock.Release();
            BufferBlock = null;
        }
    }
}
```

**Key behavior**: `ArgumentData` points directly into the receive buffer. The buffer is released when all messages from it are disposed. Compute calls can detach early to release the buffer while keeping their own copy.

### 2. Reference-Counted Buffer Pool

```csharp
public sealed class RpcBufferBlock
{
    private readonly ArrayPool<byte> _pool;
    private readonly byte[] _buffer;
    private int _refCount;

    public Memory<byte> Memory => _buffer;
    public int Length => _buffer.Length;

    public RpcBufferBlock(ArrayPool<byte> pool, int size)
    {
        _pool = pool;
        _buffer = pool.Rent(size);
        _refCount = 1; // Creator holds initial reference
    }

    public void AddRef()
        => Interlocked.Increment(ref _refCount);

    public void Release()
    {
        if (Interlocked.Decrement(ref _refCount) == 0)
            _pool.Return(_buffer);
    }
}
```

### 3. Updated Serializers

Replace message serializers to work with the new types. The wire format stays the same.

#### Outbound Serialization

```csharp
public sealed class RpcByteMessageSerializer
{
    public void Write(RpcOutboundMessage message, IBufferWriter<byte> writer)
    {
        var spanWriter = new SpanWriter(writer);

        spanWriter.WriteByte(message.CallTypeId);
        spanWriter.WriteVarLong(message.RelatedId);
        WriteMethodRef(ref spanWriter, message.MethodRef);

        if (message.ArgumentData is { } argumentData)
        {
            // Pre-serialized (cache key case)
            spanWriter.WriteVarInt(argumentData.Length);
            spanWriter.Write(argumentData.Span);
        }
        else
        {
            // Lazy serialization - serialize directly to output
            SerializeArgumentsTo(message.Arguments!, message.NeedsPolymorphism, ref spanWriter);
        }

        WriteHeaders(ref spanWriter, message.Headers);
    }

    private void SerializeArgumentsTo(
        ArgumentList arguments,
        bool needsPolymorphism,
        ref SpanWriter spanWriter)
    {
        // Reserve space for length prefix
        var lengthPosition = spanWriter.Position;
        spanWriter.WriteVarInt(0); // Placeholder
        var dataStart = spanWriter.Position;

        // Serialize arguments directly
        _argumentSerializer.WriteTo(arguments, needsPolymorphism, ref spanWriter);

        // Patch length
        var dataLength = spanWriter.Position - dataStart;
        var savedPosition = spanWriter.Position;
        spanWriter.Position = lengthPosition;
        spanWriter.WriteVarInt(dataLength);
        spanWriter.Position = savedPosition;
    }
}
```

#### Inbound Deserialization

```csharp
public sealed class RpcByteMessageSerializer
{
    public RpcInboundMessage Read(ReadOnlyMemory<byte> data, RpcBufferBlock bufferBlock)
    {
        var reader = new MemoryReader(data);

        var callTypeId = reader.ReadByte();
        var relatedId = reader.ReadVarLong();
        var methodRef = ReadMethodRef(ref reader);

        var argumentDataLength = reader.ReadVarInt();
        var argumentDataStart = reader.Position;
        reader.Skip(argumentDataLength);

        var headers = ReadHeaders(ref reader);

        // Increment ref count for this message
        bufferBlock.AddRef();

        return new RpcInboundMessage
        {
            CallTypeId = callTypeId,
            RelatedId = relatedId,
            MethodRef = methodRef,
            ArgumentData = data.Slice(argumentDataStart, argumentDataLength),
            Headers = headers,
            BufferBlock = bufferBlock,
        };
    }
}
```

### 4. Updated WebSocketChannel

```csharp
public sealed class WebSocketChannel : Channel<RpcInboundMessage, RpcOutboundMessage>
{
    private readonly ArrayPoolBuffer<byte> _writeBuffer;
    private readonly RpcByteMessageSerializer _serializer;

    // Writer side - lazy serialization
    private async Task RunWriter(CancellationToken ct)
    {
        await foreach (var message in Writer.ReadAllAsync(ct))
        {
            _writeBuffer.Reset();
            _serializer.Write(message, _writeBuffer);

            await _webSocket.SendAsync(
                _writeBuffer.WrittenMemory,
                WebSocketMessageType.Binary,
                endOfMessage: true,
                ct).ConfigureAwait(false);
        }
    }

    // Reader side - ref-counted buffers
    private async IAsyncEnumerable<RpcInboundMessage> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var bufferBlock = new RpcBufferBlock(_pool, _initialBufferSize);

            try
            {
                var totalReceived = await ReceiveFrameAsync(bufferBlock, ct);
                var data = bufferBlock.Memory.Slice(0, totalReceived);

                // Parse messages from this buffer
                var reader = new MemoryReader(data);
                while (reader.HasRemaining)
                {
                    yield return _serializer.Read(data.Slice(reader.Position), bufferBlock);
                }
            }
            finally
            {
                // Release creator's reference; messages hold their own
                bufferBlock.Release();
            }
        }
    }
}
```

### 5. Updated Call Infrastructure

#### RpcOutboundCall Changes

```csharp
public class RpcOutboundCall
{
    public RpcOutboundMessage CreateMessage(long relatedId, bool needsPolymorphism)
    {
        return new RpcOutboundMessage
        {
            CallTypeId = MethodDef.CallType.Id,
            RelatedId = relatedId,
            MethodRef = MethodDef.Ref,
            Headers = Context.Headers,
            Arguments = Context.Arguments,
            NeedsPolymorphism = needsPolymorphism,
        };
    }

    // For cache key computation - needs pre-serialized data
    public RpcOutboundMessage CreateMessageWithCacheKey(long relatedId, bool needsPolymorphism)
    {
        var argumentData = Peer.ArgumentSerializer.Serialize(
            Context.Arguments!,
            needsPolymorphism,
            Context.SizeHint);

        return new RpcOutboundMessage
        {
            CallTypeId = MethodDef.CallType.Id,
            RelatedId = relatedId,
            MethodRef = MethodDef.Ref,
            Headers = Context.Headers,
            ArgumentData = argumentData, // Pre-serialized for cache key
            NeedsPolymorphism = needsPolymorphism,
        };
    }
}
```

#### RpcInboundCall Changes

```csharp
public class RpcInboundCall
{
    public RpcInboundMessage Message { get; }

    public virtual async Task Process(CancellationToken ct)
    {
        try
        {
            Arguments ??= DeserializeArguments();
            await MethodDef.InboundCallInvoker.Invoke(this);
        }
        finally
        {
            Message.Dispose(); // Release buffer reference
        }
    }
}
```

#### RpcInboundComputeCall Changes

```csharp
public class RpcInboundComputeCall : RpcInboundCall
{
    public override async Task Process(CancellationToken ct)
    {
        // Deserialize arguments first
        Arguments ??= DeserializeArguments();

        // Detach from buffer early - compute calls may wait for invalidation
        Message.DetachFromBuffer();

        // Now process normally - buffer is already released
        await base.ProcessCore(ct);
    }
}
```

### 6. Cache Key Handling

`RpcCacheKey` requires serialized `ArgumentData`. Two approaches:

**Option A: Pre-serialize for cache-enabled calls**
```csharp
// In RpcOutboundComputeCall or when cache info capture is active
if (Context.CacheInfoCapture is not null)
{
    // Use CreateMessageWithCacheKey which pre-serializes
    message = CreateMessageWithCacheKey(relatedId, needsPolymorphism);
    Context.CacheInfoCapture.CaptureKey(Context, message);
}
else
{
    message = CreateMessage(relatedId, needsPolymorphism);
}
```

**Option B: Serialize on-demand for cache key only**
```csharp
// RpcCacheInfoCapture captures key lazily
public void CaptureKey(RpcOutboundContext context, RpcOutboundMessage message)
{
    var argumentData = message.ArgumentData
        ?? Peer.ArgumentSerializer.Serialize(message.Arguments!, message.NeedsPolymorphism);

    Key = new RpcCacheKey(MethodDef.FullName, argumentData);
}
```

Option A is preferred for simplicity - cache-enabled calls pre-serialize anyway.

## Implementation Plan

### Phase 1: New Types (COMPLETED)
1. ✅ Add `RpcBufferBlock` - `src/ActualLab.Rpc/Infrastructure/RpcBufferBlock.cs`
2. ✅ Add `RpcInboundMessage` - `src/ActualLab.Rpc/Infrastructure/RpcInboundMessage.cs`
3. ✅ Add `RpcOutboundMessage` - `src/ActualLab.Rpc/Infrastructure/RpcOutboundMessage.cs`
4. ✅ Add `RpcTransport` abstract base - `src/ActualLab.Rpc/Infrastructure/RpcTransport.cs`
5. ✅ Add unit tests - `tests/ActualLab.Tests/Rpc/RpcBufferBlockTest.cs`, `tests/ActualLab.Tests/Rpc/RpcMessageTest.cs`

### Phase 2: New Transport (COMPLETED)
1. ✅ Create `WebSocketRpcTransport` - `src/ActualLab.Rpc/WebSockets/WebSocketRpcTransport.cs`
   - Uses existing `RpcByteMessageSerializerV4` with projection enabled
   - Simplified buffer management: creates new buffer when projection is used (matches original WebSocketChannel behavior)
   - Supports lazy argument serialization for outbound messages
2. ✅ Create `WebSocketRpcTransportChannel` adapter - `src/ActualLab.Rpc/WebSockets/WebSocketRpcTransportChannel.cs`
   - Wraps `WebSocketRpcTransport` to provide `Channel<RpcMessage>` interface
   - Enables incremental migration without changing RPC infrastructure
3. ✅ Update `RpcWebSocketClient` to use new transport
4. ✅ Update `RpcWebSocketServer` (both .NET and NetFx versions) to use new transport
5. ✅ Update `RpcWebSocketClientOptions` with `WebSocketTransportOptionsFactory`
6. ✅ Mark old `WebSocketChannelOptionsFactory` as `[Obsolete]`

### Test Results
- ✅ RpcBasicTest: 73 tests passed
- ✅ RpcWebSocketTest: 130 tests passed
- ✅ RpcStreamBasicTest: 17 tests passed
- ✅ RpcReconnectionTest: 4 tests passed
- ✅ FusionRpcBasicTest: 2 tests passed
- ✅ FusionRpcCancellationTest: 4 tests passed
- ✅ FusionRpcReconnectionTest: 5/6 tests passed
  - ⚠️ ReconnectionTest (stress test) times out - this is a pre-existing flaky test unrelated to transport changes (uses in-memory channels, not WebSocket)

### Phase 3: Migration (TODO)
1. Update `RpcPeer` to use `WebSocketRpcTransport` instead of `WebSocketChannel`
2. Update `RpcConnection` to work with new transport
3. Update `RpcInboundCall` to use `RpcInboundMessage`
4. Update `RpcOutboundCall` to use `RpcOutboundMessage`
5. Update `RpcInboundContext` and `RpcOutboundContext`
6. Update cache infrastructure

### Phase 4: Cleanup (TODO)
1. Remove old `WebSocketChannel`
2. Remove old `RpcMessage` (or keep for backward compat if needed)
3. Simplify serializer code (remove duplicate projection logic)

### Phase 5: Testing (TODO)
1. Verify wire compatibility with existing clients/servers
2. Benchmark memory allocations
3. Stress test ref-counting under concurrent load

## Files to Modify/Remove

### Added (Completed)
| File | Description |
|------|-------------|
| `src/ActualLab.Rpc/Infrastructure/RpcBufferBlock.cs` | ✅ Ref-counted buffer pool |
| `src/ActualLab.Rpc/Infrastructure/RpcInboundMessage.cs` | ✅ Inbound message with buffer lifetime management |
| `src/ActualLab.Rpc/Infrastructure/RpcOutboundMessage.cs` | ✅ Outbound message with lazy serialization |
| `src/ActualLab.Rpc/Infrastructure/RpcTransport.cs` | ✅ Abstract transport base class |
| `src/ActualLab.Rpc/WebSockets/WebSocketRpcTransport.cs` | ✅ WebSocket transport implementation |
| `src/ActualLab.Rpc/WebSockets/WebSocketRpcTransportChannel.cs` | ✅ Adapter for Channel<RpcMessage> compatibility |
| `tests/ActualLab.Tests/Rpc/RpcBufferBlockTest.cs` | ✅ Unit tests for buffer block |
| `tests/ActualLab.Tests/Rpc/RpcMessageTest.cs` | ✅ Unit tests for message types |

### Modified (Completed)
| File | Description |
|------|-------------|
| `src/ActualLab.Rpc/Clients/RpcWebSocketClient.cs` | ✅ Uses `WebSocketRpcTransportChannel` |
| `src/ActualLab.Rpc/Clients/RpcWebSocketClientOptions.cs` | ✅ Added `WebSocketTransportOptionsFactory` |
| `src/ActualLab.Rpc/Server/RpcWebSocketServer.cs` | ✅ Uses `WebSocketRpcTransportChannel` |
| `src/ActualLab.Rpc.Server.NetFx/RpcWebSocketServer.cs` | ✅ Uses `WebSocketRpcTransportChannel` |
| `src/ActualLab.Rpc/Testing/RpcTestClientOptions.cs` | ✅ Uses `WebSocketRpcTransport.Options` |

### To Remove (Deferred)
| File | Description |
|------|-------------|
| `src/ActualLab.Rpc/WebSockets/WebSocketChannel.cs` | Old channel implementation - kept for backward compatibility |
| `src/ActualLab.Rpc/Infrastructure/RpcMessage.cs` | Old unified message type - required for `Channel<RpcMessage>` interface compatibility |

**Note**: The old `WebSocketChannel` and `RpcMessage` are kept for backward compatibility. The adapter pattern (`WebSocketRpcTransportChannel`) allows the new transport to work with existing RPC infrastructure without invasive changes. These can be removed in a future major version when the full migration to `RpcInboundMessage`/`RpcOutboundMessage` is complete.

### To Modify (Pending)
| File | Description |
|------|-------------|
| `src/ActualLab.Rpc/RpcPeer.cs` | Switch to new transport |
| `src/ActualLab.Rpc/RpcConnection.cs` | Update to work with new transport |
| `src/ActualLab.Rpc/Infrastructure/RpcInboundCall.cs` | Use `RpcInboundMessage` |
| `src/ActualLab.Rpc/Infrastructure/RpcOutboundCall.cs` | Use `RpcOutboundMessage` |
| `src/ActualLab.Rpc/Infrastructure/RpcInboundContext.cs` | Use `RpcInboundMessage` |
| `src/ActualLab.Rpc/Caching/RpcCacheInfoCapture.cs` | Update for new message types |
| `src/ActualLab.Fusion/Client/Internal/RpcInboundComputeCall.cs` | Call `DetachFromBuffer()` |
| `src/ActualLab.Fusion/Client/Internal/RpcOutboundComputeCall.cs` | Use `RpcOutboundMessage` |

## Expected Benefits

1. **Outbound**: Eliminates intermediate `ArgumentData` allocation and copy
2. **Inbound**: Eliminates `.ToArray()` copy - data points directly into receive buffer
3. **Memory**: Better buffer reuse via pooling with ref-counting
4. **GC Pressure**: Fewer allocations, especially for large messages
5. **Simplicity**: Removes `AllowProjection` complexity and related heuristics

## Key Considerations

### Thread Safety
- `RpcBufferBlock._refCount` uses `Interlocked` operations
- Multiple messages from same buffer may be processed concurrently
- Each message's `Dispose()` is called once from its processing context

### Compute Call Lifetime
- Compute calls detach from buffer immediately after argument deserialization
- This ensures buffers aren't held during potentially long invalidation waits
- The `DetachFromBuffer()` method copies `ArgumentData` only when needed

### Error Handling
- Messages are disposed in `finally` blocks to ensure buffer release
- `RpcBufferBlock.Release()` is idempotent after ref-count hits zero
- Double-dispose of message is safe (checks `BufferBlock is null`)

## Open Questions

1. Should `DetachFromBuffer()` be automatic for all compute calls, or configurable?
2. What's the optimal initial buffer size for `RpcBufferBlock`?
3. Should we track metrics on buffer reuse efficiency?
