namespace ActualLab.Interception.Internal;

public static class InterceptionFeatures
{
#if NET9_0_OR_GREATER
    [FeatureSwitchDefinition("ArgumentList.AllowGenerics")]
#endif
    public static bool ArgumentListAllowGenerics { get; }
        // ReSharper disable once SimplifyConditionalTernaryExpression
        = AppContext.TryGetSwitch("ArgumentList.AllowGenerics", out bool v) ? v : true;
}
