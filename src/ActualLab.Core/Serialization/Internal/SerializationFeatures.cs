namespace ActualLab.Serialization.Internal;

/// <summary>
/// Feature switches controlling which byte serializers <see cref="Trimming.CodeKeeper.KeepSerializable{T}"/>
/// preserves. On by default; disable the ones your app never uses to let trimming remove them.
/// </summary>
public static class SerializationFeatures
{
#if NET9_0_OR_GREATER
    [FeatureSwitchDefinition("MessagePackByteSerializer.IsEnabled")]
#endif
    public static bool IsMessagePackByteSerializerEnabled { get; }
        // ReSharper disable once SimplifyConditionalTernaryExpression
        = AppContext.TryGetSwitch("MessagePackByteSerializer.IsEnabled", out bool w) ? w : true;
}
