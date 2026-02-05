namespace ActualLab.CommandR.Commands;
using Interception;
using Rpc;

/// <summary>
/// A tagging interface indicating that a command can be handled only by a backend peer,
/// i.e., a peer <see cref="RpcPeer.Versions"/> defining a version
/// for <see cref="RpcDefaults.BackendScope"/>.
/// Otherwise, it will be rejected with an error.
/// </summary>
/// <remarks>
/// <see cref="CommandServiceInterceptor"/> is responsible for checks associated with this interface.
/// </remarks>
public interface IBackendCommand : ICommand;

/// <summary>
/// A generic variant of <see cref="IBackendCommand"/> that produces a typed result.
/// </summary>
public interface IBackendCommand<TResult> : ICommand<TResult>, IBackendCommand;
