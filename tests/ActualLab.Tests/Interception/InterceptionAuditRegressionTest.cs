using System.Diagnostics;
using ActualLab.Interception;

namespace ActualLab.Tests.Interception;

public class InterceptionAuditRegressionTest
{
    private const string ValueTypeTargetCase = "ACTUALLAB_ARGUMENT_LIST_VALUE_TYPE_TARGET_CASE";

    [Fact]
    public void ProxyGeneratorMustSupportNestedTypes()
        => Proxies.TryGetProxyType(typeof(INestedProxy)).Should().NotBeNull();

    [Fact]
    public async Task ArgumentListInvokerMustSupportValueTypeTargets()
    {
        var subprocessCase = Environment.GetEnvironmentVariable(ValueTypeTargetCase);
        if (!string.IsNullOrEmpty(subprocessCase)) {
            RunValueTypeTargetCase(subprocessCase);
            return;
        }

        var failures = new List<string>();
        for (var itemCount = 0; itemCount <= ArgumentList.MaxItemCount; itemCount++) {
            var firstUseGenerics = itemCount == 0 ? 0 : 1;
            for (var useGenerics = firstUseGenerics; useGenerics >= 0; useGenerics--) {
                var caseId = $"{itemCount}:{useGenerics != 0}";
                var (exitCode, output) = await RunValueTypeTargetSubprocess(caseId);
                if (exitCode != 0) {
                    var failure = output.Contains("0xC0000005") ? "0xC0000005" : $"exit code {exitCode}";
                    failures.Add($"{caseId}: {failure}");
                }
            }
        }

        failures.Should().BeEmpty();
    }

    // Private methods

    private static void RunValueTypeTargetCase(string caseId)
    {
        var parts = caseId.Split(':');
        var itemCount = int.Parse(parts[0]);
        var useGenerics = bool.Parse(parts[1]);
        var itemTypes = Enumerable.Repeat(typeof(int), itemCount).ToArray();
        var list = ArgumentListType.Get(useGenerics, itemTypes).Factory.Invoke();
        for (var i = 0; i < itemCount; i++)
            list.Set(i, 1);

        var method = typeof(ValueTypeTarget).GetMethod($"Invoke{itemCount}")!;
        object target = new ValueTypeTarget(42);
        var result = list.GetInvoker(method).Invoke(target, list);
        var expected = 42 + Math.Max(itemCount, 1);

        result.Should().Be(expected);
        ((ValueTypeTarget)target).Value.Should().Be(expected);
    }

    private static async Task<(int ExitCode, string Output)> RunValueTypeTargetSubprocess(string caseId)
    {
        var testAssembly = typeof(InterceptionAuditRegressionTest).Assembly.Location;
        var testName = $"{typeof(InterceptionAuditRegressionTest).FullName}." +
            nameof(ArgumentListInvokerMustSupportValueTypeTargets);
        var startInfo = new ProcessStartInfo("dotnet", $"vstest \"{testAssembly}\" --Tests:{testName}") {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.EnvironmentVariables[ValueTypeTargetCase] = caseId;
        using var process = Process.Start(startInfo)!;
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000)) {
            process.Kill();
            return (-1, $"Subprocess timed out for {caseId}.");
        }
        var output = await standardOutputTask;
        var error = await standardErrorTask;
        return (process.ExitCode, output + error);
    }

    // Nested types

    private struct ValueTypeTarget(int value)
    {
        public int Value { get; private set; } = value;

        public int Invoke0() => ++Value;
        public int Invoke1(int a0) => Value += a0;
        public int Invoke2(int a0, int a1) => Value += a0 + a1;
        public int Invoke3(int a0, int a1, int a2) => Value += a0 + a1 + a2;
        public int Invoke4(int a0, int a1, int a2, int a3) => Value += a0 + a1 + a2 + a3;
        public int Invoke5(
            int a0, int a1, int a2, int a3, int a4)
            => Value += a0 + a1 + a2 + a3 + a4;
        public int Invoke6(
            int a0, int a1, int a2, int a3, int a4, int a5)
            => Value += a0 + a1 + a2 + a3 + a4 + a5;
        public int Invoke7(
            int a0, int a1, int a2, int a3, int a4, int a5, int a6)
            => Value += a0 + a1 + a2 + a3 + a4 + a5 + a6;
        public int Invoke8(
            int a0, int a1, int a2, int a3, int a4, int a5, int a6, int a7)
            => Value += a0 + a1 + a2 + a3 + a4 + a5 + a6 + a7;
        public int Invoke9(
            int a0, int a1, int a2, int a3, int a4, int a5, int a6, int a7, int a8)
            => Value += a0 + a1 + a2 + a3 + a4 + a5 + a6 + a7 + a8;
        public int Invoke10(
            int a0, int a1, int a2, int a3, int a4, int a5, int a6, int a7, int a8, int a9)
            => Value += a0 + a1 + a2 + a3 + a4 + a5 + a6 + a7 + a8 + a9;
    }

    public interface INestedProxy : IRequiresAsyncProxy
    {
        Task Run();
    }
}
