namespace ActualLab.CommandR.Commands;

/// <summary>
/// A tagging interface indicating that a command can be handled only by a backend peer,
/// i.e., a peer <see cref="RpcPeer.Versions"/> defining a version
/// for <see cref="RpcDefaults.BackendScope"/>.
/// Otherwise, it will be rejected with an error.
/// </summary>
/// <remarks>
/// Enforced on the inbound RPC path by <c>RpcInboundContext</c>, which rejects any method whose
/// <c>RpcMethodDef.IsBackend</c> is true on a non-backend peer, and by <c>RpcInboundCommandHandler</c>,
/// which re-checks the deserialized command's runtime type.
/// </remarks>
public interface IBackendCommand : ICommand;

/// <summary>
/// A generic variant of <see cref="IBackendCommand"/> that produces a typed result.
/// </summary>
public interface IBackendCommand<TResult> : ICommand<TResult>, IBackendCommand;
