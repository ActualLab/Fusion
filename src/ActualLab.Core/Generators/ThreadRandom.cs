namespace ActualLab.Generators;

public static class ThreadRandom
{
    private static readonly Random SharedInstance = new();
    [ThreadStatic] private static Random? _instance;

    public static Random Instance {
        get {
            if (_instance != null)
                return _instance;

            lock (SharedInstance)
                return _instance ??= new Random(SharedInstance.Next() + Environment.CurrentManagedThreadId);
        }
    }

    public static int Next() => Instance.Next();
    public static double NextDouble() => Instance.NextDouble();
}
