namespace ActualLab;

// This type is used as an extra parameter of constructors to indicate no validation is required
public readonly record struct AssumeValid
{
    public static readonly AssumeValid Option = default!;
}
