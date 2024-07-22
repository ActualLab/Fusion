namespace ActualLab.Generators;

public abstract class UuidGenerator : Generator<string>;

public class UlidUuidGenerator : UuidGenerator
{
    public override string Next() => Ulid.NewUlid().ToString();
}

public class GuidUuidGenerator : UuidGenerator
{
    public override string Next() => Guid.NewGuid().ToString("d", null);
}
