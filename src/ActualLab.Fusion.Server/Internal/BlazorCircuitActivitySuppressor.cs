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
public class BlazorCircuitActivitySuppressor : CircuitHandler
{
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        Activity.Current = null;
        return Task.CompletedTask;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        Activity.Current = null;
        return Task.CompletedTask;
    }

    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(Func<CircuitInboundActivityContext, Task> next)
        => async context => {
            Activity.Current = null;
            await next.Invoke(context).ConfigureAwait(false);
        };
}
