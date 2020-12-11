using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Stl.Fusion.Internal
{
    public static class ComputedEx
    {
        internal static bool TryUseExisting<T>(this IComputed<T>? existing, ComputeContext context, IComputed? usedBy)
        {
            var callOptions = context.CallOptions;
            var useExisting = (callOptions & CallOptions.TryGetExisting) != 0;

            if (existing == null)
                return useExisting;
            if (!(useExisting || existing.IsConsistent()))
                return false;

            context.TryCapture(existing);
            var invalidate = (callOptions & CallOptions.Invalidate) == CallOptions.Invalidate;
            if (invalidate) {
                existing.Invalidate();
                return true;
            }
            if (!useExisting)
                ((IComputedImpl?) usedBy)?.AddUsed((IComputedImpl) existing!);
            ((IComputedImpl?) existing)?.RenewTimeouts();
            return true;
        }

        internal static async ValueTask<ResultBox<T>?> TryUseExistingAsync<T>(
            this IAsyncComputed<T>? existing, ComputeContext context, IComputed? usedBy,
            CancellationToken cancellationToken)
        {
            var callOptions = context.CallOptions;
            var useExisting = (callOptions & CallOptions.TryGetExisting) != 0;

            if (existing == null)
                return useExisting ? ResultBox<T>.Default : null;
            if (!(useExisting || existing.IsConsistent()))
                return null;

            var invalidate = (callOptions & CallOptions.Invalidate) == CallOptions.Invalidate;
            if (invalidate) {
                existing.Invalidate();
                context.TryCapture(existing);
                return existing.MaybeOutput ?? ResultBox<T>.Default;
            }

            var result = existing.MaybeOutput;
            if (result == null) {
                result = await existing.GetOutputAsync(cancellationToken).ConfigureAwait(false);
                if (result == null)
                    return null;
            }

            if (!useExisting)
                ((IComputedImpl?) usedBy)?.AddUsed((IComputedImpl) existing!);
            context.TryCapture(existing);
            ((IComputedImpl?) existing)?.RenewTimeouts();
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UseNew<T>(this IComputed<T> computed, ComputeContext context, IComputed? usedBy)
        {
            ((IComputedImpl?) usedBy)?.AddUsed((IComputedImpl) computed);
            ((IComputedImpl?) computed)?.RenewTimeouts();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T Strip<T>(this IComputed<T>? computed)
            => computed != null ? computed.Value : default!;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Task<T> StripToTask<T>(this IComputed<T>? computed)
            => computed?.Output.AsTask() ?? Task.FromResult(default(T)!);
    }
}
