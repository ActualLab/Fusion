namespace ActualLab.Generators;

/// <summary>
/// A <see cref="UuidGenerator"/> that produces <see cref="Guid"/>-based UUID strings.
/// </summary>
public class GuidUuidGenerator : UuidGenerator
{
    public static readonly GuidUuidGenerator Instance = new();

    public override string Next() => Guid.NewGuid().ToString("d", provider: null);
}
