using ActualLab.IO;
using ActualLab.OS;
using ActualLab.Testing.Output;
using CommunityToolkit.HighPerformance;

namespace ActualLab.Tests;

public abstract class TestBase(ITestOutputHelper @out) : IAsyncLifetime
{
    protected static readonly string DotNetVersion = RuntimeInfo.DotNet.VersionString ?? "";
    protected static readonly string DotNetVersionHash = Convert.ToBase64String(BitConverter.GetBytes(DotNetVersion.GetDjb2HashCode()))[..4];
    protected static readonly FilePath TempDir = TestRunnerInfo.IsGitHubAction()
        ? (FilePath)Environment.GetEnvironmentVariable("RUNNER_TEMP")
        : FilePath.GetApplicationTempDirectory("", true);

    [field: AllowNull, MaybeNull]
    protected string LongTestNameHash {
        get {
            if (field is not null)
                return field;

            var testType = GetType();
            field = FilePath.GetHashedName($"{testType.Name}_{testType.Namespace}_{DotNetVersionHash}");
            return field;
        }
    }

    [field: AllowNull, MaybeNull]
    protected string ShortTestNameHash {
        get {
            if (field is not null)
                return field;

            var testType = GetType();
            field = FilePath.GetHashedName($"{testType.Name}_{testType.Namespace}_{DotNetVersionHash}", maxLength: 20);
            return field;
        }
    }

    public ITestOutputHelper Out { get; set; } = @out;

    public virtual Task InitializeAsync() => Task.CompletedTask;
    public virtual Task DisposeAsync() => Task.CompletedTask;

    // Protected methods

    protected string GetTestRedisKeyPrefix(string? basePrefix = null, string? suffix = null)
    {
        suffix ??= LongTestNameHash;
        return $"{basePrefix ?? "fusion_tests"}_{suffix}";
    }

    protected string GetTestDbConnectionString(string connectionString, string dbName, string? suffix = null)
    {
        suffix ??= LongTestNameHash;
        return connectionString.Replace(dbName, $"{dbName}_{suffix}");
    }

    protected FilePath GetTestSqliteFilePath(string? baseName = null, string? suffix = null)
    {
        suffix ??= LongTestNameHash;
        return TempDir & $"{baseName ?? "fusion_tests"}_{suffix}.db";
    }

    protected Disposable<TestOutputCapture> CaptureOutput()
    {
        var testOutputCapture = new TestOutputCapture(Out);
        var oldOut = Out;
        Out = testOutputCapture;
        return new Disposable<TestOutputCapture>(
            testOutputCapture,
            _ => Out = oldOut);
    }
}
