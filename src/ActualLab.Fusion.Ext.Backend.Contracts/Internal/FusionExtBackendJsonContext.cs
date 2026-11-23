#if NET8_0_OR_GREATER // Source-generated System.Text.Json metadata matters for NativeAOT only
using System.Text.Json.Serialization;
using ActualLab.Fusion.Authentication;

namespace ActualLab.Fusion.Extensions.Internal;

/// <summary>
/// Source-generated System.Text.Json metadata for the types the services of ActualLab.Fusion.Ext.Backend.Contracts
/// carry; it's added to <see cref="SystemJsonSerializer.TypeInfoResolver"/> on startup.
/// </summary>
[JsonSerializable(typeof(AuthBackend_SetSessionOptions))]
[JsonSerializable(typeof(AuthBackend_SetupSession))]
[JsonSerializable(typeof(AuthBackend_SignIn))]
[JsonSerializable(typeof(KeyValueStore_Set))]
[JsonSerializable(typeof(KeyValueStore_Remove))]
internal sealed partial class FusionExtBackendJsonContext : JsonSerializerContext;
#endif
