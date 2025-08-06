using System.Diagnostics;
using ActualLab.Rpc.Internal;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Rpc;

[DebuggerDisplay("{" + nameof(DebugValue) + "}")]
public abstract partial class RpcPeerRef(bool isServer)
{
    private string? _toStringCached;
    private string DebugValue => IsInitialized ? ToString() : "<uninitialized>";

    protected bool IsInitialized { get; set; }

    public bool IsServer { get; } = isServer;
    public bool IsBackend { get; init; }
    public RpcPeerConnectionKind ConnectionKind { get; init; } = RpcPeerConnectionKind.Remote;
    public string SerializationFormat { get; init; } = "";
    public string HostInfo { get; init; } = "";

    // Properties that require initialization
    public string Address {
        get { ThrowIfUninitialized(); return field; }
        set;
    } = "";

    public VersionSet Versions {
        get { ThrowIfUninitialized(); return field; }
        set;
    } = VersionSet.Empty;

    // Rerouting-related properties
    public virtual CancellationToken RerouteToken => default;
    public bool CanBeRerouted => RerouteToken.CanBeCanceled;
    public bool IsRerouted => RerouteToken.IsCancellationRequested;

    public override string ToString()
    {
        if (!IsRerouted)
            return Address;

        _toStringCached ??= "<*>" + Address;
        return _toStringCached;
    }

    /// <summary>
    /// This method must be called for any RpcPeerRef instance before using it.
    /// </summary>
    public RpcPeerRef Initialize()
    {
        IsInitialized = true; // Required to access Address and Versions properties
        try {
            if (Address.IsNullOrEmpty())
                Address = RpcPeerRefAddress.Format(this);
            if (Versions.IsEmpty)
                Versions = RpcDefaults.GetVersions(IsBackend);
        }
        catch {
            // If initialization fails, reset the state to uninitialized
            IsInitialized = false;
            throw;
        }
        return this;
    }

    // WhenXxx

    public async Task WhenRerouted()
        => await TaskExt.NewNeverEndingUnreferenced().WaitAsync(RerouteToken).SilentAwait(false);

    public Task WhenRerouted(CancellationToken cancellationToken)
    {
        return cancellationToken.CanBeCanceled
            ? WhenReroutedWithCancellationToken(cancellationToken)
            : WhenRerouted();

        async Task WhenReroutedWithCancellationToken(CancellationToken cancellationToken1) {
            using var tcs = RerouteToken.LinkWith(cancellationToken1);
            await TaskExt.NewNeverEndingUnreferenced().WaitAsync(tcs.Token).SilentAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    // Protected methods

    protected void ThrowIfUninitialized()
    {
        if (!IsInitialized)
            throw Errors.NotInitialized();
    }
}
