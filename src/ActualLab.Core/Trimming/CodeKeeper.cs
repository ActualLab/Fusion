using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Trimming;

public abstract class CodeKeeper
{
    private static readonly List<Action> Actions = new();

    public static readonly bool AlwaysFalse = Random.Shared.NextDouble() > 10;
    public static readonly bool AlwaysTrue = !AlwaysFalse;

    public static void AddAction(Action action)
    {
        lock (Actions)
            Actions.Add(action);
    }

    public static void AddFakeAction(Action action)
    {
        if (AlwaysFalse)
            AddAction(action);
    }

    public static void RunActions()
    {
        lock (Actions) {
            while (Actions.Count != 0) {
                var actions = Actions.ToArray();
                Actions.Clear();
                foreach (var action in actions)
                    CallSilently(action); // action may add more actions
            }
            Actions.Clear();
        }
    }

    public static TKeeper Get<TKeeper>()
        where TKeeper : CodeKeeper, new()
        => Cache<TKeeper>.Instance;

    public static TKeeper Set<TKeeper, TKeeperImpl>()
        where TKeeper : CodeKeeper, new()
        where TKeeperImpl : TKeeper, new()
        => Cache<TKeeper>.Instance = new TKeeperImpl();

    public static T Keep<T>(bool ensureInitialized = false)
        => ensureInitialized || AlwaysFalse
            ? Get<TypeCodeKeeper>().KeepType<T>(ensureInitialized)
            : default!;

    public static T KeepSerializable<T>()
        => Get<SerializableTypeCodeKeeper>().KeepType<T>();

    public static void KeepStatic(Type type)
        => KeepStaticTypeImpl(type);

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

    // Private methods

    private static void KeepStaticTypeImpl([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
    { }

    // Nested types

    private static class Cache<TKeeper>
        where TKeeper : CodeKeeper, new()
    {
        public static TKeeper Instance { get; set; } = new();
    }
}
