using System.Text.RegularExpressions;
using CommunityToolkit.HighPerformance;

namespace ActualLab.IO;

public readonly partial struct FilePath
{
#if NET7_0_OR_GREATER
    [GeneratedRegex("[^A-Za-z0-9_]+")]
    private static partial Regex NonAlphaOrNumberReFactory();
    [GeneratedRegex("^_+")]
    private static partial Regex LeadingUnderscoresReFactory();
    [GeneratedRegex("_+$")]
    private static partial Regex TrailingUnderscoresReFactory();

    private static readonly Regex NonAlphaOrNumberRe = NonAlphaOrNumberReFactory();
    private static readonly Regex LeadingUnderscoresRe = LeadingUnderscoresReFactory();
    private static readonly Regex TrailingUnderscoresRe = TrailingUnderscoresReFactory();
#else
    private static readonly Regex NonAlphaOrNumberRe = new("[^A-Za-z0-9_]+", RegexOptions.Compiled);
    private static readonly Regex LeadingUnderscoresRe = new("^_+", RegexOptions.Compiled);
    private static readonly Regex TrailingUnderscoresRe = new("_+$", RegexOptions.Compiled);
#endif

    public static string GetHashedName(
        string key, string? prefix = null,
        int maxLength = 40, bool alwaysHash = false)
    {
        if (maxLength is < 8 or > 128)
            throw new ArgumentOutOfRangeException(nameof(maxLength));

        var result = prefix ?? key;
        result = NonAlphaOrNumberRe.Replace(result, "_");
        result = TrailingUnderscoresRe.Replace(result, "");

        var mustAddHash = alwaysHash || !string.Equals(result, key, StringComparison.Ordinal);
        if (mustAddHash || result.Length > maxLength) {
            var hash = Convert.ToBase64String(BitConverter.GetBytes(key.GetDjb2HashCode()));
            hash = NonAlphaOrNumberRe.Replace(hash, "_");
            hash = LeadingUnderscoresRe.Replace(hash, "");
            hash = TrailingUnderscoresRe.Replace(hash, "");
            var prefixLength = Math.Min(result.Length, maxLength - hash.Length - 1);
            result = $"{result.Substring(0, prefixLength)}_{hash}";
        }
        return result;
    }

    public static FilePath GetTempPath()
        => Path.GetTempPath();

    public static FilePath GetApplicationDirectory()
    {
        var assembly = Assembly.GetEntryAssembly();
        if (assembly?.GetName()?.Name?.StartsWith("testhost", StringComparison.Ordinal) ?? false) // Unit tests
            assembly = Assembly.GetExecutingAssembly();
        return Path.GetDirectoryName(assembly?.Location) ?? Environment.CurrentDirectory;
    }

    public static FilePath GetApplicationTempDirectory(string appId = "")
    {
        // The application-specific directory is guaranteed to exist when this method succeeds.
        if (appId.IsNullOrEmpty())
            appId = Assembly.GetEntryAssembly()?.GetName()?.Name ?? "unknown";
        var subdirectory = GetHashedName($"{appId}_{GetApplicationDirectory()}");
        var path = GetTempPath() & subdirectory;
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        return path;
    }

    public static FilePath GetApplicationCacheDirectory(string appId = "")
    {
        // Unlike GetApplicationTempDirectory, the result is rooted in a per-user location, so
        // another local user can neither pre-create nor write to it. The application-specific
        // directory is guaranteed to exist when this method succeeds.
        if (appId.IsNullOrEmpty())
            appId = Assembly.GetEntryAssembly()?.GetName()?.Name ?? "unknown";
        var subdirectory = GetHashedName($"{appId}_{GetApplicationDirectory()}");
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var path = root.IsNullOrEmpty()
            ? GetTempPath() & subdirectory
            : new FilePath(root) & "ActualLab" & subdirectory;
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
        return path;
    }

    public static bool IsWritableByOtherUsers(FilePath path)
    {
        // Permission bits exist only on Unix and only since .NET 7; everywhere else the answer is
        // "unknown", reported as false - callers use this to reject an obviously unsafe location,
        // not to prove a safe one.
#if NET7_0_OR_GREATER
        if (OperatingSystem.IsWindows())
            return false;

        try {
            var directory = new DirectoryInfo(path.Value);
            return directory.Exists
                && (directory.UnixFileMode & (UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) != 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or PlatformNotSupportedException) {
            return false;
        }
#else
        return false;
#endif
    }
}
