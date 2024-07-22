using System.Data;

namespace ActualLab.Fusion.EntityFramework;

public static class IsolationLevelExt
{
    public static IsolationLevel Or(this IsolationLevel first, IsolationLevel second)
        => first == IsolationLevel.Unspecified ? second : first;

    public static IsolationLevel Or<TState>(this IsolationLevel first, TState state, Func<TState, IsolationLevel> second)
        => first == IsolationLevel.Unspecified ? second.Invoke(state) : first;

    public static IsolationLevel Max(this IsolationLevel first, IsolationLevel second)
    {
        // If one is unspecified, we return the other one
        if (second == IsolationLevel.Unspecified)
            return first;
        if (first == IsolationLevel.Unspecified)
            return second;

        // Serializable is < Snapshot somehow in these enums
        if (first == IsolationLevel.Serializable)
            return first;
        if (second == IsolationLevel.Serializable)
            return second;

        // Otherwise we return max. of two
        return (IsolationLevel)MathExt.Max((int)first, (int)second);
    }
}
