namespace ActualLab;

// This type is used as an extra parameter of constructors to indicate newly generated Id required
public readonly record struct Generate
{
    public static readonly Generate Option = default!;
}
