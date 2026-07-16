namespace ActualLab.Reflection;

/// <summary>
/// Provides typed invocation-list helpers for delegates.
/// </summary>
public static class DelegateExt
{
    public static TDelegate[] GetInvocationList<TDelegate>(TDelegate? @delegate)
        where TDelegate : Delegate
    {
        if (@delegate is null)
            return [];

        var invocationList = @delegate.GetInvocationList();
        var result = new TDelegate[invocationList.Length];
        for (var i = 0; i < result.Length; i++)
            result[i] = (TDelegate)invocationList[i];
        return result;
    }
}
