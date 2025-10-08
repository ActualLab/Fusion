namespace ActualLab.Generators;

public abstract class UuidGenerator : Generator<string>;

public class UlidUuidGenerator : UuidGenerator
{
    public static readonly UlidUuidGenerator Instance = new();

    public override string Next() => Ulid.NewUlid().ToString();
}

public class GuidUuidGenerator : UuidGenerator
{
    public static readonly GuidUuidGenerator Instance = new();

    public override string Next() => Guid.NewGuid().ToString("d", null);
}
