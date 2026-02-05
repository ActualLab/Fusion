using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc;

/// <summary>
/// Registry of <see cref="RpcCallType"/> instances, mapping call type IDs to their definitions.
/// </summary>
public static class RpcCallTypes
{
#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif
    private static readonly RpcCallType?[] Registry;

    public static readonly RpcCallType Regular = new(0);

    static RpcCallTypes()
    {
        Registry = new RpcCallType?[8];
        Registry[Regular.Id] = Regular;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RpcCallType Resolve(byte callTypeId)
        => Registry[callTypeId] ?? throw Errors.UnknownCallType(callTypeId);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RpcCallType? Get(byte callTypeId)
        => Registry[callTypeId];

    public static string GetDescription(byte callTypeId)
        => Get(callTypeId) is { } callType
            ? callType.ToString()
            : $"{callTypeId}: Unknown";

    public static bool Register(RpcCallType callType)
    {
        var ict = callType.InboundCallType;
        if (!typeof(RpcInboundCall).IsAssignableFrom(ict) || !ict.IsGenericType || ict.GetGenericArguments().Length != 1)
            throw ActualLab.Internal.Errors.InternalError("InboundCallType must be an open generic descendant of RpcInboundCall<>.");

        var oct = callType.OutboundCallType;
        if (!typeof(RpcOutboundCall).IsAssignableFrom(oct) || !oct.IsGenericType || oct.GetGenericArguments().Length != 1)
            throw ActualLab.Internal.Errors.InternalError("OutboundCallType must be an open generic descendant of RpcOutboundCall<>.");

        if (Get(callType.Id) is not null) return false;

        lock (StaticLock) {
            if (Get(callType.Id) is not null) return false;

            Registry[callType.Id] = callType;
            return true;
        }
    }
}
