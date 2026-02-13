using System.ComponentModel;

namespace ActualLab.Tests;

public class TypeScriptRunner(ITestOutputHelper @out)
{
    private static readonly SemaphoreSlim NpmInstallLock = new(1, 1);
    private static string? _tsDir;

    public ITestOutputHelper Out { get; } = @out;
    public string TsDir => _tsDir ??= FindTypeScriptDirectory();

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

    public async Task EnsureTsBuilt()
    {
        var tsxPath = FindLocalBinOrNull("tsx");
        var distExists = IsTsBuilt();
        if (tsxPath != null && distExists)
            return;

        await NpmInstallLock.WaitAsync();
        try {
            tsxPath = FindLocalBinOrNull("tsx");
            distExists = IsTsBuilt();
            if (tsxPath != null && distExists)
                return;

            var npmPath = FindGlobalBin("npm");

            if (tsxPath == null) {
                // Delete corrupt/partial node_modules to prevent local npm shim
                // from shadowing the global npm binary
                var nodeModulesDir = Path.Combine(TsDir, "node_modules");
                if (Directory.Exists(nodeModulesDir)) {
                    Out.WriteLine("node_modules exists but tsx is missing — deleting node_modules...");
                    Directory.Delete(nodeModulesDir, recursive: true);
                }

                Out.WriteLine("Running npm install...");
                var installResult = await RunProcess(TsDir, npmPath, "install",
                    timeout: TimeSpan.FromSeconds(120));

                if (installResult.ExitCode != 0)
                    throw new InvalidOperationException(
                        $"npm install failed (exit code {installResult.ExitCode}):\n{installResult.Stderr}");

                Out.WriteLine("npm install completed.");
            }

            if (!distExists) {
                Out.WriteLine("Running npm run build...");
                var buildResult = await RunProcess(TsDir, npmPath, "run build",
                    timeout: TimeSpan.FromSeconds(120));

                if (buildResult.ExitCode != 0)
                    throw new InvalidOperationException(
                        $"npm run build failed (exit code {buildResult.ExitCode}):\n{buildResult.Stderr}");

                File.WriteAllText(Path.Combine(TsDir, ".ts-built"), "");
                Out.WriteLine("npm run build completed.");
            }
        }
        finally {
            NpmInstallLock.Release();
        }
    }

    // Private methods

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
        if (OperatingSystem.IsWindows() && executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)) {
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

        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(cts.Token);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return new ProcessResult(process.ExitCode, stdout, stderr);
    }

    private bool IsTsBuilt()
        => File.Exists(Path.Combine(TsDir, ".ts-built"));

    private static string FindGlobalBin(string name)
    {
        var bin = OperatingSystem.IsWindows() ? $"{name}.cmd" : name;
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

    private string FindLocalBin(string name)
    {
        var path = FindLocalBinOrNull(name);
        if (path == null)
            throw new FileNotFoundException(
                $"'{name}' not found in '{TsDir}/node_modules/.bin'. Run 'npm install' in '{TsDir}'.");
        return path;
    }

    private string? FindLocalBinOrNull(string name)
    {
        var bin = OperatingSystem.IsWindows() ? $"{name}.cmd" : name;
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
