namespace ActualLab.Async;

public static partial class TaskExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFaultedOrCancelled(this Task task)
        => task.Status is TaskStatus.Faulted or TaskStatus.Canceled;

#if NETSTANDARD2_0

    extension(Task task)
    {
        /// <summary>
        /// Cross-platform version of <code>IsCompletedSuccessfully</code> from .NET Core.
        /// </summary>
        /// <value>The task.</value>
        /// <value>True if <paramref name="task"/> is completed successfully; otherwise, false.</value>
        public bool IsCompletedSuccessfully
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => task.Status == TaskStatus.RanToCompletion;
        }
    }

#endif

}
