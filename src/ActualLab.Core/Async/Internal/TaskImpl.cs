namespace ActualLab.Async.Internal;

/// <summary>
/// Provides low-level access to internal <see cref="Task"/> fields and methods.
/// </summary>
public static class TaskImpl
{
#if USE_UNSAFE_ACCESSORS
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "m_stateFlags")]
    public static extern ref int StateFlagsGetter(Task task);
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "AddTaskContinuation")]
    public static extern bool AddContinuation(Task task, object continuation, bool addBeforeOthers);
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RemoveContinuation")]
    public static extern void RemoveContinuation(Task task, object continuationObject);
#else
    public static readonly Action<Task, int> StateFlagsSetter;
    public static readonly Func<Task, object, bool, bool> AddContinuation;
    public static readonly Action<Task, object> RemoveContinuation;
#endif

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume Task class is fully preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "We assume Task class is fully preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume Task class is fully preserved")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(TaskExt))]
    static TaskImpl()
    {
#if !USE_UNSAFE_ACCESSORS
        StateFlagsSetter = typeof(Task)
            .GetField("m_stateFlags", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetSetter<Task, int>();
        AddContinuation = (Func<Task, object, bool, bool>)typeof(Task)
            .GetMethod("AddTaskContinuation", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate(typeof(Func<Task, object, bool, bool>));
        RemoveContinuation = (Action<Task, object>)typeof(Task)
            .GetMethod("RemoveContinuation", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate(typeof(Action<Task, object>));
#endif
    }
}
