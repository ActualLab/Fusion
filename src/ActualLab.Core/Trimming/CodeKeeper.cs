using ActualLab.Generators;

namespace ActualLab.Trimming;

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "CodeKeepers are used only to retain the code")]
public abstract class CodeKeeper
{
#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif
    private static readonly List<Action> Actions = new();
    private static readonly HashSet<Action> ActionSet = new();

    // Any kind of logic compiler won't fold to "true" or "false"
    public static readonly bool AlwaysFalse = CpuTimestamp.Now.Value == -1 && RandomShared.NextDouble() < 1e-300;
    public static readonly bool AlwaysTrue = !AlwaysFalse;

    public static void AddAction(Action action)
    {
        lock (StaticLock) {
            if (ActionSet.Add(action))
                Actions.Add(action);
        }
    }

    public static void AddFakeAction(Action action)
    {
        if (AlwaysFalse)
            AddAction(action);
    }

    public static void RunActions()
    {
        lock (StaticLock) {
            while (Actions.Count != 0) {
                var actions = Actions.ToArray();
                Actions.Clear();
                ActionSet.Clear();
                foreach (var action in actions)
                    CallSilently(action); // action may add more actions
            }
        }
    }

    public static TKeeper Get<TKeeper>()
        where TKeeper : CodeKeeper, new()
        => Cache<TKeeper>.Instance;

    public static TKeeper Set<TKeeper, TKeeperImpl>()
        where TKeeper : CodeKeeper, new()
        where TKeeperImpl : TKeeper, new()
        => Cache<TKeeper>.Instance = new TKeeperImpl();

    public static T Keep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(bool ensureInitialized = false)
        => ensureInitialized || AlwaysFalse
            ? Get<TypeCodeKeeper>().KeepType<T>(ensureInitialized)
            : default!;

    public static T KeepSerializable<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
        => Get<SerializableTypeCodeKeeper>().KeepType<T>();

    public static void KeepUnconstructable([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
    { }

    public static T CallSilently<T>(Func<T> func)
    {
        try {
            return func.Invoke();
        }
        catch {
            // Intended
        }
        return default!;
    }

    public static void CallSilently(Action action)
    {
        try {
            action.Invoke();
        }
        catch {
            // Intended
        }
    }

    public static void FakeCallSilently(Action action)
    {
        if (AlwaysFalse)
            CallSilently(action);
    }

    // Nested types

    private static class Cache<TKeeper>
        where TKeeper : CodeKeeper, new()
    {
        public static TKeeper Instance { get; set; } = new();
    }
}
