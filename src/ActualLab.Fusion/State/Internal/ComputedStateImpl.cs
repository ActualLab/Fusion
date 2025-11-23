using ActualLab.Caching;

namespace ActualLab.Fusion.Internal;

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
