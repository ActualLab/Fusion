#if NET8_0_OR_GREATER // Source-generated System.Text.Json metadata matters for NativeAOT only
using System.Text.Json.Serialization;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Serialization.Internal;

/// <summary>
/// Source-generated System.Text.Json metadata for the types RPC system calls carry;
/// it's added to <see cref="SystemJsonSerializer.TypeInfoResolver"/> on startup.
/// </summary>
[JsonSerializable(typeof(RpcHandshake))]
[JsonSerializable(typeof(RpcNoWait))]
[JsonSerializable(typeof(Dictionary<int, byte[]>))]
internal sealed partial class RpcJsonContext : JsonSerializerContext;
#endif
