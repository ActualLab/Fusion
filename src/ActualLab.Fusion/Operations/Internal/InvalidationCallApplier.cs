using System.ComponentModel;
using System.Globalization;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.Diagnostics;
using ActualLab.Fusion.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Operations.Internal;

/// <summary>
/// Applies the <see cref="InvalidationCall"/>s recorded by a
/// <see cref="InvalidationMode.Replicated"/> handler - locally right after its commit,
/// and on every other host once the operation log delivers the operation.
/// </summary>
public class InvalidationCallApplier(IServiceProvider services) : IOperationCompletionListener
{
    private static readonly ConcurrentDictionary<string, bool> ReportedDrops = new(StringComparer.Ordinal);

    protected IServiceProvider Services { get; } = services;
    protected ComputeServiceRegistry Registry
        => field ??= Services.GetRequiredService<ComputeServiceRegistry>();
    protected RpcHub RpcHub => field ??= Services.GetRequiredService<RpcHub>();
    protected ILogger Log => field ??= Services.LogFor(GetType());

    public Task OnOperationCompleted(Operation operation, CommandContext? commandContext)
        => commandContext is not null || operation.InvalidationCalls.IsEmpty
            // Locally originated operations apply their calls right after the commit - see
            // InMemoryOperationScopeProvider, which does it before the mutating call returns.
            ? Task.CompletedTask
            : Apply(operation.InvalidationCalls,
                new InvalidationSource($"Replicated invalidation of operation #{operation.Uuid}"));

    public async Task Apply(ImmutableList<InvalidationCall> calls, InvalidationSource source)
    {
        if (calls.IsEmpty)
            return;

        // Forces local execution of any distributed service method, exactly like the replay path does
        using var _1 = new RpcOutboundCallSetup(RpcHub.LocalPeer).Activate();
        using var _2 = Invalidation.Begin(source);
        foreach (var call in calls)
            await TryApply(call).ConfigureAwait(false);
    }

    // Protected methods

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume compute service code is preserved")]
    protected virtual async Task TryApply(InvalidationCall call)
    {
        try {
            if (call.ServiceType.TryResolve() is not { } implementationType) {
                Drop(call, "unknown service type");
                return;
            }

            var serviceType = Registry.GetServiceType(implementationType);
            var service = Services.GetService(serviceType) ?? Services.GetService(implementationType);
            if (service is null || !implementationType.IsInstanceOfType(service)) {
                // Includes the pure RPC client case: the host owning the service replicates
                // its own invalidations, so there is nothing to do here.
                Drop(call, "no local service");
                return;
            }

            var method = Registry.TryGetMethod(implementationType, call.MethodName, call.Arguments.Length);
            if (method is null) {
                Drop(call, "unknown or ambiguous method");
                return;
            }

            var parameters = method.GetParameters();
            var arguments = new object?[parameters.Length];
            for (var i = 0; i < call.Arguments.Length; i++)
                arguments[i] = Coerce(call.Arguments[i], parameters[i].ParameterType);
            if (parameters.Length > call.Arguments.Length)
                arguments[^1] = CancellationToken.None;

            switch (method.Invoke(service, arguments)) {
            case Task task:
                await task.ConfigureAwait(false);
                break;
            case ValueTask valueTask:
                await valueTask.ConfigureAwait(false);
                break;
            }
        }
        catch (Exception e) {
            FusionInstruments.DeferredInvalidationFailureCount.Add(1);
            Log.LogError(e, "Invalidation call failed: {Call}", call);
        }
    }

    // Private methods

    // Text serialization erases the exact numeric/enum type of a boxed argument, so the value
    // has to be brought back to the parameter's type before the method can be invoked.
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume compute service code is preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "We assume compute service code is preserved")]
    private static object? Coerce(object? value, Type type)
    {
        if (value is null || type.IsInstanceOfType(value))
            return value;

        var nonNullableType = Nullable.GetUnderlyingType(type) ?? type;
        if (nonNullableType.IsEnum)
            return value is string enumName
                ? Enum.Parse(nonNullableType, enumName, ignoreCase: true)
                : Enum.ToObject(nonNullableType, value);
        if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(nonNullableType))
            return Convert.ChangeType(value, nonNullableType, CultureInfo.InvariantCulture);

        var converter = TypeDescriptor.GetConverter(nonNullableType);
        return converter.CanConvertFrom(value.GetType())
            ? converter.ConvertFrom(null, CultureInfo.InvariantCulture, value)
            : value;
    }

    private void Drop(InvalidationCall call, string reason)
    {
        FusionInstruments.DeferredInvalidationDropCount.Add(1);
        var key = $"{call.ServiceType.TypeName}.{call.MethodName}/{call.Arguments.Length}";
        if (ReportedDrops.TryAdd(key, true))
            Log.LogWarning("Invalidation call dropped ({Reason}): {Call}", reason, call);
    }
}
