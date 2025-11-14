using System.Net.WebSockets;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Internal;

public static class Errors
{
    public static Exception UnknownCallType(byte callTypeId)
        => new KeyNotFoundException($"Unknown CallTypeId: {callTypeId}.");

    public static Exception UnspecifiedServiceMode(Type serviceType, RpcServiceMode mode)
        => new InvalidOperationException(
            $"Service '{serviceType.GetName()}' got {nameof(RpcServiceMode)}.{mode:G}, "
            + "although it must be configured for a specific service mode at this point.");

    public static Exception DistributedServicesMustNotHaveImplementationResolver()
        => new InvalidOperationException("Distributed services must not have an ImplementationResolver.");

    public static Exception ServiceTypeConflict(Type serviceType)
        => new InvalidOperationException($"Service '{serviceType.GetName()}' is already registered.");
    public static Exception ServiceNameConflict(Type serviceType1, Type serviceType2, string serviceName)
        => new InvalidOperationException($"Services '{serviceType1.GetName()}' and '{serviceType2.GetName()}' have the same name '{serviceName}'.");
    public static Exception MethodNameConflict(RpcMethodDef methodDef)
        => new InvalidOperationException($"Service '{methodDef.Service.Type.GetName()}' has 2 or more methods named '{methodDef.Name}'.");

    public static Exception NoService(Type serviceType)
        => new KeyNotFoundException($"Can't resolve service by type: '{serviceType.GetName()}'.");
    public static Exception NoService(string serviceName)
        => new KeyNotFoundException($"Can't resolve service by name: '{serviceName}'.");
    public static Exception NoClient(RpcServiceDef service)
        => new KeyNotFoundException($"RPC service has no client: {service}.");
    public static Exception NoServer(RpcServiceDef service)
        => new KeyNotFoundException($"RPC service has no server: {service}.");

    public static Exception NoMethod(Type serviceType, MethodInfo method)
        => new KeyNotFoundException($"Can't resolve method '{method.Name}' (by MethodInfo) of '{serviceType.GetName()}'.");
    public static Exception NoMethod(Type serviceType, string methodName)
        => new KeyNotFoundException($"Can't resolve method '{methodName}' (by name) of '{serviceType.GetName()}'.");

    public static Exception HandshakeFailed()
        => new RpcException("Handshake failed.");
    public static Exception PeerChanged()
        => new RpcException("Remote peer has been changed.");
    public static Exception EndpointNotFound(string serviceName, string methodName)
        => new RpcException($"Endpoint not found: '{serviceName}.{methodName}'.");
    public static Exception InvalidCallTypeId(string serviceName, string methodName, string expected, string actual)
        => new RpcException($"Invalid CallTypeId: '{serviceName}.{methodName}' expected '{expected}', but got '{actual}'.");
    public static Exception MatchButNoCachedEntry()
        => new RpcException("The remote server responded with 'Match', but the outbound call has no cached entry.");
    public static Exception TooLateToReconnect()
        => new RpcException("Peer is already changed, too late for this reconnect round.");

    public static Exception NoCurrentRpcInboundContext()
        => new InvalidOperationException($"{nameof(RpcInboundContext)}.{nameof(RpcInboundContext.Current)} is unavailable.");
    public static Exception NoCurrentRpcOutboundContext()
        => new InvalidOperationException($"{nameof(RpcOutboundContext)}.{nameof(RpcOutboundContext.Current)} is unavailable.");
    public static Exception RpcOutboundContextChanged()
        => new InvalidOperationException(
            $"The scope returned from {nameof(RpcOutboundContext)}.{nameof(RpcOutboundContext.Activate)} " +
            $"detected context change on its disposal. " +
            $"Most likely the scope was disposed in async continuation / another thread, which should never happen - " +
            $"this scope should be used only in synchronous part of your code that happens " +
            $"right before the async method triggering the outgoing RPC call is invoked.");
    public static Exception RpcOutboundCallSetupChanged()
        => new InvalidOperationException(
            $"The scope returned from {nameof(RpcOutboundCallSetup)}.{nameof(RpcOutboundCallSetup.Activate)} " +
            $"detected Peer change on its disposal. " +
            $"Most likely the scope was disposed in async continuation / another thread, which should never happen - " +
            $"this scope should be used only in synchronous part of your code that happens " +
            $"right before the async method triggering the outgoing RPC call is invoked.");

    public static Exception InvalidItemSize()
        => new SerializationException("Invalid item size. The remainder of the message will be dropped.");
    public static Exception InvalidItemTypeFormat()
        => new SerializationException("Invalid item type format.");
    public static Exception CannotSerializeAbstractType(Type type)
        => new SerializationException($"Cannot serialize abstract type '{type.GetName()}'.");
    public static Exception CannotDeserializeInboundCallArguments(Exception innerException)
        => new SerializationException("Cannot deserialize inbound call arguments.", innerException);
    public static Exception CannotDeserializeUnexpectedArgumentType(Type expectedType, Type actualType)
        => new SerializationException($"Cannot deserialize unexpected argument type: " +
            $"expected '{expectedType.GetName()}' (exact match), got '{actualType.GetName()}'.");
    public static Exception CannotDeserializeUnexpectedPolymorphicArgumentType(Type expectedType, Type actualType)
        => new SerializationException($"Cannot deserialize polymorphic argument type: " +
            $"expected '{expectedType.GetName()}' or its descendant, got '{actualType.GetName()}'.");
    public static Exception InvalidResultType(Type expectedType, object? actualResult)
        => new SerializationException(
            $"Got invalid RPC call result type: " +
            $"expected '{expectedType.GetName()}', got '{actualResult?.GetType().GetName() ?? "null"}'.");

    public static Exception ConnectTimeout(RpcPeerRef peerRef, TimeSpan? timeout = null)
        => ConnectTimeout(peerRef.GetRemotePartyName());
    public static Exception ConnectTimeout(string remoteParty = "remote host", TimeSpan? timeout = null)
        => new TimeoutException(
            timeout is { } t
                ? $"Timeout while connecting to {remoteParty} ({t.ToShortString()})."
                : $"Timeout while connecting to {remoteParty}.");

    public static Exception CallTimeout(RpcPeerRef peerRef, TimeSpan? timeout = null)
        => CallTimeout(peerRef.GetRemotePartyName());

    public static Exception CallTimeout(string remoteParty = "remote host", TimeSpan? timeout = null)
        => new TimeoutException(
            timeout is { } t
                ? $"The {remoteParty} didn't respond in time ({t.ToShortString()})."
                : $"The {remoteParty} didn't respond in time.");

    public static Exception HandshakeTimeout()
        => new TimeoutException("Timeout while waiting for RPC handshake.");
    public static Exception KeepAliveTimeout()
        => new TimeoutException("Timeout while waiting for RPC keep-alive.");

    public static Exception ClientRpcPeerRefExpected(string argumentName)
        => new ArgumentOutOfRangeException(argumentName, "Client RpcPeerRef is expected.");
    public static Exception ServerRpcPeerRefExpected(string argumentName)
        => new ArgumentOutOfRangeException(argumentName, "Server RpcPeerRef is expected.");
    public static Exception BackendRpcPeerRefExpected(string argumentName)
        => new ArgumentOutOfRangeException(argumentName, "Backend RpcPeerRef is expected.");

    public static Exception InvalidRpcObjectKind(RpcObjectKind expectedKind)
        => new InvalidOperationException($"Invalid IRpcObject kind (expected: {expectedKind}).");
    public static Exception RpcObjectIsAlreadyUsed()
        => new InvalidOperationException("This IRpcObject is already used in some other call.");

    public static Exception RemoteRpcStreamCanBeEnumeratedJustOnce()
        => new InvalidOperationException("Remote RpcStream can be enumerated just once.");

    public static Exception RpcStreamNotFound()
        => new KeyNotFoundException("RpcStream with the specified Id is not found.");
    public static Exception RpcStreamInvalidPosition()
        => new InvalidOperationException("RpcStream position is invalid.");

    public static Exception InvalidWebSocketMessageType(WebSocketMessageType type, WebSocketMessageType expectedType)
        => new InvalidOperationException($"Invalid WebSocket message type: got {type:G}, but expected {expectedType:G}.");

    public static Exception SystemCallsMustBePrerouted()
        => new InvalidOperationException("System calls must be prerouted.");

    public static Exception GotRpcRerouteExceptionFromRemotePeer()
        => new InvalidOperationException(
            "Got RpcRerouteException from remote peer, which should never happen.");

    public static Exception InvalidRpcPeerRefAddress(string id)
        => new ArgumentException($"Invalid {nameof(RpcPeerRef)}.{nameof(RpcPeerRef.Address)}: \"{id}\".", id);
}
