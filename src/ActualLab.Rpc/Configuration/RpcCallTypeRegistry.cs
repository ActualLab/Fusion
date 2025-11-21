using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc;

public static class RpcCallTypeRegistry
{
#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif
    private static volatile RpcCallTypeRegistration?[] _callTypes;

    static RpcCallTypeRegistry()
    {
        _callTypes = new RpcCallTypeRegistration?[8];
        _callTypes[RpcCallTypes.Regular] = new(RpcCallTypes.Regular) {
            InboundCallTypeOverridesInvokeServer = false,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RpcCallTypeRegistration Resolve(byte callTypeId)
        => _callTypes[callTypeId] ?? throw Errors.UnknownCallType(callTypeId);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RpcCallTypeRegistration? Get(byte callTypeId)
        => _callTypes[callTypeId];

    public static string GetDescription(byte callTypeId)
        => Get(callTypeId) is { } registration
            ? registration.ToString()
            : $"{callTypeId}: Unknown";

    public static bool Register(RpcCallTypeRegistration registration)
    {
        var ict = registration.InboundCallType;
        if (!typeof(RpcInboundCall).IsAssignableFrom(ict) || !ict.IsGenericType || ict.GetGenericArguments().Length != 1)
            throw ActualLab.Internal.Errors.InternalError("InboundCallType must be an open generic descendant of RpcInboundCall<>.");

        var oct = registration.OutboundCallType;
        if (!typeof(RpcOutboundCall).IsAssignableFrom(oct) || !oct.IsGenericType || oct.GetGenericArguments().Length != 1)
            throw ActualLab.Internal.Errors.InternalError("OutboundCallType must be an open generic descendant of RpcOutboundCall<>.");

        if (Get(registration.Id) is not null) return false;

        lock (StaticLock) {
            if (Get(registration.Id) is not null) return false;

            _callTypes[registration.Id] = registration;
            return true;
        }
    }
}
