using System.Diagnostics;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace ActualLab.Fusion.Server.Internal;

// Blazor Circuit "inherits" Activity.Current from an HTTP request associated with its
// WebSocket or HTTP connection, and this long-living activity becomes parent of
// any other activity triggered inside the circuit, which is typically undesirable.
// See:
// - https://github.com/dotnet/aspnetcore/issues/29846
//
// This circuit handler resets it for any inbound activity.

/// <summary>
/// A Blazor <see cref="CircuitHandler"/> that resets <see cref="Activity.Current"/>
/// to prevent long-lived connection activities from parenting unrelated spans.
/// </summary>
public class BlazorCircuitActivitySuppressor : CircuitHandler
{
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        TrySuppress();
        return Task.CompletedTask;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        TrySuppress();
        return Task.CompletedTask;
    }

#if NET8_0_OR_GREATER
    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(Func<CircuitInboundActivityContext, Task> next)
        => async context => {
            TrySuppress();
            await next.Invoke(context).ConfigureAwait(false);
        };
#endif

    // Protected methods

    protected virtual void TrySuppress()
    {
        if (Activity.Current is { } activity && MustSuppress(activity))
            Activity.Current = null;
    }

    protected virtual bool MustSuppress(Activity activity)
    {
        // Suppresses connection-scoped infrastructure activities (hosting request, SignalR
        // connection), but preserves deliberate ambient ones - e.g. Blazor circuit spans
        // from the .NET 10 "Microsoft.AspNetCore.Components" source.
        var sourceName = activity.Source.Name;
        return sourceName.Length == 0 // Legacy (DiagnosticSource-based) activities, e.g. hosting's HttpRequestIn
            || string.Equals(sourceName, "Microsoft.AspNetCore", StringComparison.Ordinal)
            || string.Equals(sourceName, "Microsoft.AspNetCore.Http.Connections", StringComparison.Ordinal);
    }
}
