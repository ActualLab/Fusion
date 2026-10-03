using ActualLab.CommandR.Internal;
using ActualLab.Interception;

namespace ActualLab.CommandR.Operations;

/// <summary>
/// Executes the <see cref="ServiceCall"/>s an <see cref="Operation"/> carries, on whichever host
/// applies it - locally when the operation completes here, and on every other host once the
/// operation log delivers it.
/// </summary>
/// <remarks>
/// This one can't apply anything by itself: only Fusion knows what invalidating a computed value
/// means, and <c>AddFusion()</c> replaces it with a handler that does. Subclass it to run whatever
/// else an operation's completion should trigger - <see cref="ApplyInvalidation"/> is the machinery for
/// turning a <see cref="ServiceCall"/> back into a call.
/// </remarks>
public class OperationCompletionHandler(IServiceProvider services)
    : IOperationCompletionListener, ICommandHandler<OperationCompletion>
{
    private static readonly ConcurrentDictionary<MethodInfo, Func<object?, ArgumentList, object?>> Invokers = new();
    private static readonly ConcurrentDictionary<string, bool> ReportedDrops = new(StringComparer.Ordinal);

    protected IServiceProvider Services { get; } = services;
    protected ServiceTypeResolver ServiceTypeResolver
        => field ??= Services.GetRequiredService<ServiceTypeResolver>();
    protected ILogger Log => field ??= Services.LogFor(GetType());

    public virtual Task OnOperationCompleted(Operation operation, CommandContext? commandContext)
    {
        if (operation.InvalidationCalls.IsEmpty)
            return Task.CompletedTask; // Nothing to do - the fast path
        if (commandContext is not null)
            return Task.CompletedTask; // A local operation applies its calls right after the commit

        return ApplyInvalidations(operation.InvalidationCalls, handleLocally: true);
    }

    [CommandHandler]
    public virtual Task OnCommand(
        OperationCompletion command,
        CommandContext context,
        CancellationToken cancellationToken)
        => command.InvalidationCalls.Length == 0
            ? Task.CompletedTask
            : ApplyInvalidations(command.InvalidationCalls, handleLocally: false, cancellationToken);

    // handleLocally: apply the calls to this host's own state. The alternative is to route each
    // one to the host that owns its value, which has no second chance - nobody else will apply
    // those later, so dropping one silently would lose it.
    public virtual Task ApplyInvalidations(
        IReadOnlyList<ServiceCall> invalidationCalls,
        bool handleLocally,
        CancellationToken cancellationToken = default)
        => invalidationCalls.Count == 0
            ? Task.CompletedTask
            : throw Errors.InvalidationCallsRequireFusion(GetType());

    // Protected methods

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume called service code is preserved")]
    protected virtual async Task ApplyInvalidation(
        ServiceCall call,
        bool mustResolve = false,
        CancellationToken cancellationToken = default)
    {
        if (call.ServiceType.TryResolve() is not { } type) {
            DropInvalidation(call, "unknown service type", mustResolve);
            return;
        }

        // A call naming a protected method names the implementation type - that's the only one of
        // the two that declares the method - so map it back to what the container knows
        var serviceType = ServiceTypeResolver.TryResolveServiceType(type) ?? type;
        var service = Services.GetService(serviceType);
        if (service is null) {
            DropInvalidation(call, "no local service", mustResolve);
            return;
        }

        // The method is looked up on the implementation, not on the type the container knows:
        // a method worth calling here can be protected, and then it isn't on the interface
        var method = MethodInfoExt.TryGetByRpcStyleName(service.GetType(), call.MethodName);
        if (method is null) {
            DropInvalidation(call, "unknown or ambiguous method", mustResolve);
            return;
        }

        switch (GetInvoker(method).Invoke(service, call.Arguments)) {
        case Task task:
            await task.ConfigureAwait(false);
            break;
        case ValueTask valueTask:
            await valueTask.ConfigureAwait(false);
            break;
        }
    }

    protected virtual void DropInvalidation(ServiceCall call, string reason, bool mustResolve = false)
    {
        if (mustResolve)
            throw Errors.ServiceCallCannotBeApplied(call.ToString(), reason);

        // MethodName is RPC-style, so it already carries the argument count
        var key = $"{call.ServiceType.TypeName}.{call.MethodName}";
        if (ReportedDrops.TryAdd(key, true))
            Log.LogWarning("Post-completion call dropped ({Reason}): {Call}", reason, call);
    }

    // Private methods

    private static Func<object?, ArgumentList, object?> GetInvoker(MethodInfo method)
        => Invokers.GetOrAdd(method,
#pragma warning disable IL2026
            static m => ArgumentListType.Get(m).Factory.Invoke().GetInvoker(m));
#pragma warning restore IL2026
}
