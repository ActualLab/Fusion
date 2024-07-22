namespace ActualLab;

// This type is used as an extra parameter of constructors to indicate no validation is required
public readonly record struct ParseOrNone
{
    public static readonly ParseOrNone Option = default!;
}
