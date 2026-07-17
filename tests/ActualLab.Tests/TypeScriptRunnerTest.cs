namespace ActualLab.Tests;

public class TypeScriptRunnerTest : IDisposable
{
    private static readonly string[] RequiredTsupPackages = [
        "core", "rpc", "fusion", "fusion-rpc",
    ];

    private readonly string _tsDir = Path.Combine(
        Path.GetTempPath(), $"fusion-ts-runner-{Guid.NewGuid():N}");
    private readonly DateTime _builtAt = DateTime.UtcNow.AddMinutes(-5);

    public TypeScriptRunnerTest()
    {
        Directory.CreateDirectory(_tsDir);
        foreach (var relativePath in new[] {
                     "package.json", "package-lock.json", "tsconfig.json", "tsup.config.ts",
                 })
            WriteBuildInput(relativePath);

        foreach (var package in RequiredTsupPackages) {
            WriteBuildInput(Path.Combine("packages", package, "package.json"));
            WriteBuildInput(Path.Combine("packages", package, "tsconfig.json"));
            WriteBuildInput(Path.Combine("packages", package, "src", "index.ts"));

            var distDir = Path.Combine(_tsDir, "packages", package, "dist");
            Directory.CreateDirectory(distDir);
            File.WriteAllText(Path.Combine(distDir, "index.cjs"), "built");
            File.WriteAllBytes(Path.Combine(distDir, "index.js"), new byte[20_000]);
        }

        var buildMarker = Path.Combine(_tsDir, ".ts-built");
        File.WriteAllText(buildMarker, "");
        File.SetLastWriteTimeUtc(buildMarker, _builtAt);
    }

    [Fact]
    public void ShouldDetectStaleTypeScriptBuildInputs()
    {
        TypeScriptRunner.IsTsBuilt(_tsDir).Should().BeTrue();

        var sourceFile = Path.Combine(_tsDir, "packages", "rpc", "src", "index.ts");
        File.SetLastWriteTimeUtc(sourceFile, _builtAt.AddMinutes(1));
        TypeScriptRunner.IsTsBuilt(_tsDir).Should().BeFalse();

        File.SetLastWriteTimeUtc(sourceFile, _builtAt.AddMinutes(-1));
        TypeScriptRunner.IsTsBuilt(_tsDir).Should().BeTrue();

        var buildConfig = Path.Combine(_tsDir, "tsup.config.ts");
        File.SetLastWriteTimeUtc(buildConfig, _builtAt.AddMinutes(1));
        TypeScriptRunner.IsTsBuilt(_tsDir).Should().BeFalse();
    }

    public void Dispose()
        => Directory.Delete(_tsDir, recursive: true);

    private void WriteBuildInput(string relativePath)
    {
        var path = Path.Combine(_tsDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "input");
        File.SetLastWriteTimeUtc(path, _builtAt.AddMinutes(-1));
    }
}
