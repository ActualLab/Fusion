using ActualLab.Generators;

namespace ActualLab.Versioning.Providers;

public sealed class LTagVersionGenerator : VersionGenerator<LTag>
{
    public static VersionGenerator<LTag> Default { get; set; } = new LTagVersionGenerator(ConcurrentLTagGenerator.Default);

    private readonly ConcurrentGenerator<LTag> _generator;

    public LTagVersionGenerator(ConcurrentGenerator<LTag> generator) => _generator = generator;

    public override LTag NextVersion(LTag currentVersion = default)
    {
        while (true) {
            var nextVersion = _generator.Next();
            if (nextVersion != currentVersion)
                return nextVersion;
        }
    }
}
