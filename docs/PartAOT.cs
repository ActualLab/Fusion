using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR.Trimming;
using ActualLab.Fusion.Trimming;
using ActualLab.Interception;
using ActualLab.Interception.Trimming;
using ActualLab.Rpc;
using ActualLab.Rpc.Trimming;
using ActualLab.Trimming;
using static System.Console;
// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable InconsistentNaming

// ReSharper disable once CheckNamespace
namespace Docs.PartAOT;

// Fake types for snippet compilation
public interface ITestService : IComputeService
{
    Task<DateTime> GetTime();
}

public class TestService : ITestService
{
    [ComputeMethod]
    public virtual Task<DateTime> GetTime() => Task.FromResult(DateTime.UtcNow);
}

public class MyResult { }
public class MyCommand { }
public class MyDto { }
public interface IMyService : IRequiresAsyncProxy { }
public class MyServiceProxy : IProxy
{
    public Interceptor Interceptor { get; set; } = null!;
}
public class MyType { }

// ============================================================================
// RuntimeCodegen Modes
// ============================================================================

public static class RuntimeCodegenModes
{
    public static void Example()
    {
        #region PartAOT_CheckMode
        // Check the current mode
        Console.WriteLine($"RuntimeCodegen.Mode: {RuntimeCodegen.Mode}");

        // In Native AOT:
        // - NativeMode will be InterpretedExpressions
        // - DynamicMethods is not available
        #endregion
    }
}

// ============================================================================
// CodeKeeper Infrastructure - showing the conceptual hierarchy
// ============================================================================

// The actual implementation is in ActualLab.Trimming, ActualLab.Interception.Trimming, etc.
// Below shows the conceptual API:
/*
#region PartAOT_CodeKeeperBase
public abstract class CodeKeeper
{
    // Never evaluates to true at runtime, but compiler can't prove it
    public static readonly bool AlwaysFalse;
    public static readonly bool AlwaysTrue;

    // Register a type to prevent trimming
    public static T Keep<T>(bool ensureInitialized = false);

    // Register serializable types
    public static T KeepSerializable<T>();

    // Run all registered actions (called at startup)
    public static void RunActions();
}
#endregion
*/

// ============================================================================
// Basic Setup
// ============================================================================

public static class BasicSetup
{
    public static void Example()
    {
        #region PartAOT_BasicSetup
        // Set the code keeper to use (FusionProxyCodeKeeper includes all subsystems)
        CodeKeeper.Set<ProxyCodeKeeper, FusionProxyCodeKeeper>();

        // Run the code keeper actions to register types
        if (RuntimeCodegen.NativeMode != RuntimeCodegenMode.DynamicMethods)
            CodeKeeper.RunActions();
        #endregion
    }
}

// ============================================================================
// Complete Example - conceptual (would have entry point conflict)
// ============================================================================

/*
#region PartAOT_CompleteExample
#pragma warning disable IL3050

public static async Task Main()
{
    // Configure code keeper before anything else
    CodeKeeper.Set<ProxyCodeKeeper, FusionProxyCodeKeeper>();
    if (RuntimeCodegen.NativeMode != RuntimeCodegenMode.DynamicMethods)
        CodeKeeper.RunActions();

    // Now configure services as usual
    var services = new ServiceCollection()
        .AddLogging(l => l.AddSimpleConsole())
        .AddFusion(fusion => {
            fusion.Rpc.AddWebSocketClient();
            fusion.AddServerAndClient<ITestService, TestService>();
        })
        .AddSingleton(_ => RpcOutboundCallOptions.Default with {
            RouterFactory = methodDef => args => RpcPeerRef.Loopback,
        })
        .BuildServiceProvider();

    // Use services
    var client = services.RpcHub().GetClient<ITestService>();
    var now = await client.GetTime();
}

#pragma warning restore IL3050
#endregion
*/

// ============================================================================
// Custom CodeKeeper for Service Methods
// ============================================================================

#region PartAOT_CustomCodeKeeperMethods
public class MyAppCodeKeeper : FusionProxyCodeKeeper
{
    public MyAppCodeKeeper()
    {
        if (AlwaysTrue)
            return;

        // Keep types used as method results
        KeepAsyncMethod<MyResult>();                    // Task<MyResult>
        KeepAsyncMethod<MyResult, string>();            // Task<MyResult> Method(string arg)
        KeepAsyncMethod<MyResult, string, int>();       // Task<MyResult> Method(string, int)

        // Keep types used as method arguments
        KeepMethodArgument<MyCommand>();

        // Keep serializable types
        KeepSerializable<MyDto>();
    }
}
#endregion

// ============================================================================
// Custom CodeKeeper for Proxy Types
// ============================================================================

#region PartAOT_CustomCodeKeeperProxy
public class MyAppProxyCodeKeeper : FusionProxyCodeKeeper
{
    public MyAppProxyCodeKeeper()
    {
        if (AlwaysTrue)
            return;

        // Keep service interface and its generated proxy
        KeepProxy<IMyService, MyServiceProxy>();
    }
}

// Use your custom code keeper
// CodeKeeper.Set<ProxyCodeKeeper, MyAppProxyCodeKeeper>();
#endregion

// ============================================================================
// How CodeKeeper Works
// ============================================================================

#region PartAOT_HowCodeKeeperWorks
public class MyCodeKeeper : CodeKeeper
{
    public void KeepMyType()
    {
        // This condition is always false at runtime, but the compiler can't prove it
        if (AlwaysTrue)
            return;

        // This code is never executed, but the trimmer sees the reference
        // and preserves the type
        Keep<MyType>();
    }
}
#endregion

// ============================================================================
// AlwaysFalse Implementation - conceptual
// ============================================================================

// The actual implementation uses runtime values that can't be evaluated at compile time
/*
#region PartAOT_AlwaysFalseImplementation
public static readonly bool AlwaysFalse =
    CpuTimestamp.Now.Value == -1 && RandomShared.NextDouble() < 1e-300;
#endregion
*/

// ============================================================================
// Suppress Trimming Warnings
// ============================================================================

public static class SuppressTrimmingWarnings
{
    #region PartAOT_SuppressWarnings
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Types are preserved by CodeKeeper")]
    [UnconditionalSuppressMessage("Trimming", "IL3050",
        Justification = "Types are preserved by CodeKeeper")]
    public static void MyMethod() { }
    #endregion
}

// ============================================================================
// DynamicallyAccessedMembers
// ============================================================================

public static class DynamicallyAccessedMembersExample
{
    #region PartAOT_DynamicallyAccessedMembers
    public static void ProcessType<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
    {
        // T's members are preserved by the trimmer
    }
    #endregion
}

// ============================================================================
// ArgumentList Support
// ============================================================================

public static class ArgumentListSupport
{
    public static void Example()
    {
        #region PartAOT_ArgumentListSupport
        // These types are auto-generated and need preservation
        var l0 = ArgumentList.New();                    // ArgumentList0
        var l2 = ArgumentList.New(1, "s");              // ArgumentList2<int, string>
        var l10 = ArgumentList.New(0, 1, 2, 3, 4, 5, 6, 7, 8, 9);  // ArgumentList10<...>
        #endregion
    }
}

// ============================================================================
// DocPart class
// ============================================================================

public class PartAOT : DocPart
{
    public override async Task Run()
    {
        StartSnippetOutput("Reference verification");

        // Core trimming infrastructure
        _ = typeof(CodeKeeper);
        _ = typeof(ProxyCodeKeeper);
        _ = typeof(CommanderProxyCodeKeeper);
        _ = typeof(RpcProxyCodeKeeper);
        _ = typeof(FusionProxyCodeKeeper);

        // RuntimeCodegen
        _ = typeof(RuntimeCodegen);
        _ = typeof(RuntimeCodegenMode);
        _ = RuntimeCodegen.Mode;
        _ = RuntimeCodegen.NativeMode;
        _ = RuntimeCodegenMode.DynamicMethods;
        _ = RuntimeCodegenMode.InterpretedExpressions;

        // ArgumentList
        _ = typeof(ArgumentList);
        _ = ArgumentList.New();
        _ = ArgumentList.New(1, "test");

        // AlwaysFalse/AlwaysTrue
        _ = CodeKeeper.AlwaysFalse;
        _ = CodeKeeper.AlwaysTrue;

        WriteLine("All Native AOT references verified successfully!");
        WriteLine();

        await Task.CompletedTask;
    }
}
