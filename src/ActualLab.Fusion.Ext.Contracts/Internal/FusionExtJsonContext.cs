#if NET8_0_OR_GREATER // Source-generated System.Text.Json metadata matters for NativeAOT only
using System.Text.Json.Serialization;
using ActualLab.Fusion.Authentication;

namespace ActualLab.Fusion.Extensions.Internal;

/// <summary>
/// Source-generated System.Text.Json metadata for the types the services of ActualLab.Fusion.Ext.Contracts carry;
/// it's added to <see cref="SystemJsonSerializer.TypeInfoResolver"/> on startup.
/// </summary>
[JsonSerializable(typeof(Session))]
[JsonSerializable(typeof(User))]
[JsonSerializable(typeof(SessionInfo))]
[JsonSerializable(typeof(SessionAuthInfo))]
[JsonSerializable(typeof(ImmutableArray<SessionInfo>))]
[JsonSerializable(typeof(Auth_EditUser))]
[JsonSerializable(typeof(Auth_SignOut))]
[JsonSerializable(typeof(PageRef<string>))]
[JsonSerializable(typeof(SortDirection))]
internal sealed partial class FusionExtJsonContext : JsonSerializerContext;
#endif
