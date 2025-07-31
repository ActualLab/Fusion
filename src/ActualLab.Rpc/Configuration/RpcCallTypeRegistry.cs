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
    private static volatile (Type? InboundCallType, Type? OutboundCallType)[] _callTypes;

    static RpcCallTypeRegistry()
    {
        _callTypes = new (Type?, Type?)[256];
        _callTypes[RpcCallTypes.Regular] = (typeof(RpcInboundCall<>), typeof(RpcOutboundCall<>));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (Type InboundCallType, Type OutboundCallType) Resolve(byte callTypeId)
    {
        var item = _callTypes[callTypeId];
        if (item.InboundCallType is null)
            throw Errors.UnknownCallType(callTypeId);
        return item!;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (Type? InboundCallType, Type? OutboundCallType) Get(byte callTypeId)
        => _callTypes[callTypeId];

    public static void Register(byte callTypeId, Type inboundCallType, Type outboundCallType)
    {
        if (!typeof(RpcInboundCall).IsAssignableFrom(inboundCallType))
            throw ActualLab.Internal.Errors.MustBeAssignableTo<RpcInboundCall>(inboundCallType, nameof(inboundCallType));
        if (!typeof(RpcOutboundCall).IsAssignableFrom(outboundCallType))
            throw ActualLab.Internal.Errors.MustBeAssignableTo<RpcOutboundCall>(outboundCallType, nameof(outboundCallType));

        if (!inboundCallType.IsGenericType || inboundCallType.GetGenericArguments().Length != 1)
            throw new ArgumentOutOfRangeException(nameof(inboundCallType));
        if (!outboundCallType.IsGenericType || outboundCallType.GetGenericArguments().Length != 1)
            throw new ArgumentOutOfRangeException(nameof(outboundCallType));

        var item = (inboundCallType, outboundCallType);
        if (Get(callTypeId) == item)
            return;

        lock (StaticLock) {
            var existingItem = Get(callTypeId);
            if (existingItem == item)
                return;

            if (existingItem.InboundCallType is not null)
                throw ActualLab.Internal.Errors.KeyAlreadyExists();

            _callTypes[callTypeId] = item;
        }
    }
}
