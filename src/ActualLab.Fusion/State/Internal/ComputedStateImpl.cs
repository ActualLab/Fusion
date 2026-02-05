using ActualLab.Caching;

namespace ActualLab.Fusion.Internal;

/// <summary>
/// Internal helpers for <see cref="ComputedState"/>, including dispose-aware compute task creation.
/// </summary>
public static class ComputedStateImpl
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task? GetComputeTaskIfDisposed(ComputedState state)
    {
#pragma warning disable MA0022, RCS1210
        if (!state.IsDisposed)
            return null;

        return GenericInstanceCache
            .GetUnsafe<Func<CancellationToken, Task>>(typeof(GetComputeTaskIfDisposedFactory<>), state.OutputType)
            .Invoke(state.DisposeToken);
#pragma warning restore MA0022, RCS1210
    }

    // Nested types

    /// <summary>
    /// A generic factory that creates a cancellation-aware compute task
    /// for a disposed <see cref="ComputedState"/>.
    /// </summary>
    public sealed class GetComputeTaskIfDisposedFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override object Generate()
        {
            if (typeof(T) == typeof(VoidSurrogate))
                throw ActualLab.Internal.Errors.InternalError("Void-typed parameter cannot be used here.");

            return static (CancellationToken disposeToken)
                => disposeToken.IsCancellationRequested
                    ? (Task)Task.FromCanceled<T>(disposeToken)
                    : TaskExt.NewNeverEndingUnreferenced<T>().WaitAsync(disposeToken);
        }
    }
}
