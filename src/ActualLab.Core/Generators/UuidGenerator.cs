namespace ActualLab.Generators;

public abstract class UuidGenerator : Generator<string>;

public class UlidUuidGenerator : UuidGenerator
{
    public static readonly UlidUuidGenerator Instance = new();

#pragma warning disable MA0011
    public override string Next() => Ulid.NewUlid().ToString();
#pragma warning restore MA0011
}

public class GuidUuidGenerator : UuidGenerator
{
    public static readonly GuidUuidGenerator Instance = new();

    public override string Next() => Guid.NewGuid().ToString("d", provider: null);
}
