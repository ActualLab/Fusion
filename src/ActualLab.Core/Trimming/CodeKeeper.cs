using System.Buffers;
using ActualLab.Generators;
// ReSharper disable ReturnValueOfPureMethodIsNotUsed

namespace ActualLab.Trimming;

/// <summary>
/// Static utility that prevents .NET trimming / NativeAOT from removing
/// types and code that are only used via reflection or dynamic dispatch.
/// Uses the dual-mechanism approach: <c>[DynamicallyAccessedMembers(All)]</c>
/// preserves metadata, while <c>typeof(T).GetMembers()</c> in a dead branch
/// forces ILC to generate native code (critical for struct generics).
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL2057", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "CodeKeepers are used only to retain the code")]
public static class CodeKeeper
{
    // Any kind of logic compiler won't fold to "true" or "false"
    public static readonly bool AlwaysFalse = RandomShared.NextDouble() > 2;
    public static readonly bool AlwaysTrue = !AlwaysFalse;

    public static IExtension? Extension { get; set; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Keep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
    {
        if (AlwaysTrue)
            return;

        var t = typeof(T);
        t.GetConstructors();
        t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance);
        t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        // _ = (AlwaysTrue ? new object() : default(T)) is T; // Force ILC to generate native code for casting
        Extension?.Keep<T>();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Keep([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
    {
        if (AlwaysTrue)
            return;

        type.GetConstructors();
        type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance);
        type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Extension?.Keep(type);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Keep(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] string assemblyQualifiedTypeName)
    {
        if (AlwaysTrue)
            return;

        var t = Type.GetType(assemblyQualifiedTypeName);
        t!.GetConstructors();
        t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance);
        t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Extension?.Keep(assemblyQualifiedTypeName);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void KeepSerializable<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
    {
        if (AlwaysTrue)
            return;

        Keep<T>();
        Keep<UniSerialized<T>>();
        Keep<MemoryPackSerialized<T>>();
        Keep<MemoryPackByteSerializer<T>>();
#if !NETSTANDARD2_0
        MemoryPackSerializer.Deserialize<T>(ReadOnlySpan<byte>.Empty);
        MemoryPackSerializer.Deserialize<T>(ReadOnlySequence<byte>.Empty);
        MemoryPackSerializer.Serialize<T>(default);
#endif
        Extension?.KeepSerializable<T>();
    }

    // Nested types

    public interface IExtension
    {
        public void Keep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>();
        public void Keep([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type);
        public void Keep([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] string assemblyQualifiedTypeName);
        public void KeepSerializable<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>();
    }
}
