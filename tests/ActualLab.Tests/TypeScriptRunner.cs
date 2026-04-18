using System.ComponentModel;
using ActualLab.OS;

namespace ActualLab.Tests;

public class TypeScriptRunner(ITestOutputHelper @out)
{
    // One-shot per test process: the first test that needs TS triggers the
    // check + optional build; everyone else awaits the same build, then no-ops.
    private static readonly SemaphoreSlim EnsureBuiltLock = new(1, 1);
    private static bool _ensureBuiltDone;

    private static string? _tsDir;

    public ITestOutputHelper Out { get; } = @out;
    public static string TsDir => _tsDir ??= FindTypeScriptDirectory();

    public async Task RunScenario(
        string scriptRelativePath,
        string scenario,
        Dictionary<string, string>? environmentVariables = null,
        TimeSpan? timeout = null)
    {
        await EnsureTsBuilt();

        var scriptPath = Path.Combine(TsDir, scriptRelativePath);
        var tsxPath = FindLocalBin("tsx");

        var result = await RunProcess(TsDir, tsxPath, $"\"{scriptPath}\" {scenario}",
            environmentVariables, timeout ?? TimeSpan.FromSeconds(30));

        Out.WriteLine($"[TypeScript output]\n{result.Stdout}");
        if (result.Stderr.Length > 0)
            Out.WriteLine($"[TypeScript errors]\n{result.Stderr}");

        result.ExitCode.Should().Be(0, $"TypeScript scenario failed:\n{result.Stderr}");
        result.Stderr.Should().BeEmpty("TypeScript scenario produced unexpected stderr output");
    }

    /// <summary>
    /// Ensures <c>node_modules</c> and the TS workspace build exist. Runs at
    /// most once per test process — the first caller does the check and
    /// optional build, subsequent callers return instantly once it finishes.
    /// Safe to call concurrently.
    /// </summary>
    public static async Task EnsureTsBuilt()
    {
        if (Volatile.Read(ref _ensureBuiltDone))
            return;
        await EnsureBuiltLock.WaitAsync().ConfigureAwait(false);
        try {
            if (_ensureBuiltDone)
                return;
            await EnsureTsBuiltOnceAsync().ConfigureAwait(false);
            Volatile.Write(ref _ensureBuiltDone, true);
        }
        finally {
            EnsureBuiltLock.Release();
        }
    }

    // Private methods

    private static async Task EnsureTsBuiltOnceAsync()
    {
        var tsxPath = FindLocalBinOrNull("tsx");
        var distOk = IsTsBuilt();
        if (tsxPath != null && distOk)
            return;

        var npmPath = FindGlobalBin("npm");

        if (tsxPath == null) {
            // Delete corrupt/partial node_modules to prevent local npm shim
            // from shadowing the global npm binary
            var nodeModulesDir = Path.Combine(TsDir, "node_modules");
            if (Directory.Exists(nodeModulesDir)) {
                Console.WriteLine("node_modules exists but tsx is missing — deleting node_modules...");
                Directory.Delete(nodeModulesDir, recursive: true);
            }

            Console.WriteLine("Running npm install...");
            var installResult = await RunProcess(TsDir, npmPath, "install",
                timeout: TimeSpan.FromSeconds(120));

            if (installResult.ExitCode != 0)
                throw new InvalidOperationException(
                    $"npm install failed (exit code {installResult.ExitCode}):\n{installResult.Stderr}");

            Console.WriteLine("npm install completed.");
        }

        if (!distOk) {
            Console.WriteLine("Running npm run build...");
            var buildResult = await RunProcess(TsDir, npmPath, "run build",
                timeout: TimeSpan.FromSeconds(120));

            if (buildResult.ExitCode != 0)
                throw new InvalidOperationException(
                    $"npm run build failed (exit code {buildResult.ExitCode}):\n{buildResult.Stderr}");

            File.WriteAllText(Path.Combine(TsDir, ".ts-built"), "");
            Console.WriteLine("npm run build completed.");
        }
    }

    private static async Task<ProcessResult> RunProcess(
        string workingDirectory,
        string executable,
        string arguments,
        Dictionary<string, string>? environmentVariables = null,
        TimeSpan? timeout = null)
    {
        // On Windows, .cmd shims can't be started directly with UseShellExecute=false
        // (CreateProcess doesn't handle .cmd files). Route through cmd.exe.
        string fileName, finalArguments;
        if (OSInfo.IsWindows && executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)) {
            fileName = "cmd.exe";
            finalArguments = $"/c \"\"{executable}\" {arguments}\"";
        }
        else {
            fileName = executable;
            finalArguments = arguments;
        }

        var psi = new ProcessStartInfo(fileName) {
            Arguments = finalArguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (environmentVariables != null) {
            foreach (var (key, value) in environmentVariables)
                psi.Environment[key] = value;
        }

        Process process;
        try {
            process = Process.Start(psi)!;
        }
        catch (Win32Exception e) {
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            throw new InvalidOperationException(
                $"Failed to start process. " +
                $"Tried: '{psi.FileName} {psi.Arguments}'. WorkingDirectory: '{psi.WorkingDirectory}'. " +
                $"PATH='{path}'.",
                e);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        var processTimeout = (timeout ?? TimeSpan.FromSeconds(30)).Positive();
#if NET5_0_OR_GREATER
        using var cts = new CancellationTokenSource(processTimeout);
        await process.WaitForExitAsync(cts.Token);
#else
        process.WaitForExit((int)processTimeout.TotalMilliseconds);
#endif

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return new ProcessResult(process.ExitCode, stdout, stderr);
    }

    // Packages that E2E scripts import. Each must have tsup's .cjs output in
    // dist — tsc never produces .cjs, so its presence is a reliable signal that
    // `npm run build` completed and the dist hasn't been clobbered by an
    // incidental `tsc -b` (IDE / typecheck) since.
    private static readonly string[] RequiredTsupPackages = [
        "core", "rpc", "fusion", "fusion-rpc",
    ];

    private static bool IsTsBuilt()
    {
        if (!File.Exists(Path.Combine(TsDir, ".ts-built")))
            return false;
        foreach (var pkg in RequiredTsupPackages) {
            var distDir = Path.Combine(TsDir, "packages", pkg, "dist");
            // tsup's .cjs output is the unambiguous "tsup ran" marker (tsc
            // doesn't emit .cjs). Its absence means either no build ran or the
            // dist has been wiped since.
            if (!File.Exists(Path.Combine(distDir, "index.cjs")))
                return false;
            // index.js can be replaced by a stray `tsc -b` (IDE or manual) with
            // a tiny re-export skeleton like `export { getLogs } from
            // './logging.js';`. The skeleton's siblings may be missing, so
            // `tsx` fails at resolve time. tsup's bundled esm is always tens of
            // KB; treat anything under 20KB as broken and force a rebuild.
            var indexJs = Path.Combine(distDir, "index.js");
            if (!File.Exists(indexJs) || new FileInfo(indexJs).Length < 20_000)
                return false;
        }
        return true;
    }

    private static string FindGlobalBin(string name)
    {
        var bin = OSInfo.IsWindows ? $"{name}.cmd" : name;
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator)) {
            if (string.IsNullOrWhiteSpace(dir))
                continue;
            var candidate = Path.Combine(dir, bin);
            if (File.Exists(candidate))
                return candidate;
        }
        throw new FileNotFoundException(
            $"'{bin}' not found in PATH. Ensure Node.js is installed.");
    }

    private static string FindLocalBin(string name)
    {
        var path = FindLocalBinOrNull(name);
        if (path == null)
            throw new FileNotFoundException(
                $"'{name}' not found in '{TsDir}/node_modules/.bin'. Run 'npm install' in '{TsDir}'.");
        return path;
    }

    private static string? FindLocalBinOrNull(string name)
    {
        var bin = OSInfo.IsWindows ? $"{name}.cmd" : name;
        var path = Path.Combine(TsDir, "node_modules", ".bin", bin);
        return File.Exists(path) ? path : null;
    }

    private static string FindTypeScriptDirectory()
    {
        // Walk up from the test assembly's location to find the repo root,
        // then navigate to ts/
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null) {
            var tsPath = Path.Combine(dir, "ts");
            if (Directory.Exists(tsPath) && File.Exists(Path.Combine(tsPath, "package.json")))
                return tsPath;
            dir = Path.GetDirectoryName(dir);
        }

        throw new DirectoryNotFoundException(
            $"Could not find 'ts/' directory with package.json. Searched upward from '{AppDomain.CurrentDomain.BaseDirectory}'.");
    }

    private record ProcessResult(int ExitCode, string Stdout, string Stderr);
}
