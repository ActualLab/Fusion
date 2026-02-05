namespace ActualLab.Fusion.EntityFramework;

/// <summary>
/// Helper for composing composite primary key values for Entity Framework lookups.
/// </summary>
public static class DbKey
{
    public static object[] Compose(params object[] components)
        => components;
}
