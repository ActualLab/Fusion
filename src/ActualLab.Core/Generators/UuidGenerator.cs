namespace ActualLab.Generators;

/// <summary>
/// Abstract base for generators that produce UUID strings.
/// </summary>
public abstract class UuidGenerator : Generator<string>;

/// <summary>
/// A <see cref="UuidGenerator"/> that produces ULID-based UUID strings.
/// </summary>
public class UlidUuidGenerator : UuidGenerator
{
    public static readonly UlidUuidGenerator Instance = new();

#pragma warning disable MA0011
    public override string Next() => Ulid.NewUlid().ToString();
#pragma warning restore MA0011
}

/// <summary>
/// A <see cref="UuidGenerator"/> that produces <see cref="Guid"/>-based UUID strings.
/// </summary>
public class GuidUuidGenerator : UuidGenerator
{
    public static readonly GuidUuidGenerator Instance = new();

    public override string Next() => Guid.NewGuid().ToString("d", provider: null);
}
