using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace Samples.MeshRpc;

public class MeshOutboundCallHandler : RpcOutboundCallHandler
{
    public MeshOutboundCallHandler(RpcMethodDef methodDef) : base(methodDef)
    {
        Timeouts = new(null, 60);
        Router = RouterImpl;
    }

    private RpcPeerRef RouterImpl(ArgumentList args)
    {
        if (MethodDef.Kind is RpcMethodKind.Command && Invalidation.IsActive)
            return RpcPeerRef.Local; // Commands in invalidation mode must always execute locally

        // Actual routing logic. We don't want too many conditions here: the routing runs per every RPC service call.
        if (args.Length == 0)
            return RpcPeerRef.Local;

        var arg0Type = args.GetType(0);
        if (arg0Type == typeof(HostRef))
            return RpcHostPeerRef.Get(args.Get<HostRef>(0));
        if (typeof(IHasHostRef).IsAssignableFrom(arg0Type))
            return RpcHostPeerRef.Get(args.Get<IHasHostRef>(0).HostRef);

        if (arg0Type == typeof(ShardRef))
            return RpcShardPeerRef.Get(args.Get<ShardRef>(0));
        if (typeof(IHasShardRef).IsAssignableFrom(arg0Type))
            return RpcShardPeerRef.Get(args.Get<IHasShardRef>(0).ShardRef);

        if (arg0Type == typeof(int))
            return RpcShardPeerRef.Get(ShardRef.New(args.Get<int>(0)));

        return RpcShardPeerRef.Get(ShardRef.New(args.GetUntyped(0)));
    }
}
