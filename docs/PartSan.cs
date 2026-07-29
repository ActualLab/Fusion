using System.Runtime.Serialization;
using ActualLab.Compliance;
using MemoryPack;
using MessagePack;
using Microsoft.Extensions.Hosting;
using static System.Console;
// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable InconsistentNaming

// ReSharper disable once CheckNamespace
namespace Docs.PartSan;

#region PartSan_Declare
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
public sealed partial record ApiCredentials(
    [property: DataMember(Order = 0), MemoryPackOrder(0), Key(0)] string UserId,
    [property: DataMember(Order = 1), MemoryPackOrder(1), Key(1)]
    [property: MemoryPackAllowSerialize] // MemoryPack-only: it can't see the registered formatter
    SanitizedString<Sanitizers.PrefixAndLengthHint> ApiKey
);
#endregion

#region PartSan_DeclareBefore
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
public sealed partial record PlainCredentials(
    [property: DataMember(Order = 0), MemoryPackOrder(0), Key(0)] string UserId,
    [property: DataMember(Order = 1), MemoryPackOrder(1), Key(1)] string ApiKey
);
#endregion

#region PartSan_DontMaskInGetter
[DataContract]
public sealed record BadCredentials
{
    private readonly string _apiKey = "";

    // The masked value is what gets serialized, stored, and sent - the secret is simply lost
    [DataMember(Order = 0)]
    public string ApiKey {
        get => Sanitizer.Sanitize<Sanitizers.Hidden>(_apiKey);
        init => _apiKey = value;
    }
}
#endregion

#region PartSan_MaybeSanitize
public sealed class ConnectRequest(string path, string query)
{
    public string Path { get; } = path;
    public string Query { get; } = query;
    // Computed, never serialized - so masking right in the getter is safe
    public string LogTitle => Path + Sanitizer.MaybeSanitize<Sanitizers.RpcRequestQuery>(Query);
}
#endregion

#region PartSan_CustomSanitizer
public sealed class EmailDomain : Sanitizer
{
    // Null never reaches here - Sanitizer.Apply passes it through
    protected override string Sanitize(string value)
    {
        var atIndex = value.IndexOf('@', StringComparison.Ordinal);
        return atIndex < 0
            ? Sanitize<Sanitizers.Hidden>(value)
            : Sanitizers.HiddenValue + value[atIndex..];
    }
}
#endregion

#region PartSan_UriQueryPolicy
public sealed class DownloadQuery() : Sanitizers.UriQuery(SelectSanitizer)
{
    private static Sanitizer? SelectSanitizer(string name)
        => string.Equals(name, "token", StringComparison.OrdinalIgnoreCase)
            ? Get<Sanitizers.Hidden>()
            : null;
}
#endregion

public class PartSan : DocPart
{
    private const string ApiKey = "sk-7f3a91b8c2e4d6f0";

    public override async Task Run()
    {
        var credentials = new ApiCredentials("alice", ApiKey);

        StartSnippetOutput("Rendering");
        #region PartSan_Render
        using (Sanitization.Begin()) // What SanitizingLogger opens around every log call
            WriteLine($"Authenticating {credentials.UserId} with {credentials.ApiKey}");
        WriteLine(credentials.ApiKey.Value); // .Value is the raw one, and it's the only way to get it
        #endregion

        StartSnippetOutput("Serialization carries the raw value");
        #region PartSan_Wire
        WriteLine(SystemJsonSerializer.Default.Write(credentials));
        #endregion

        StartSnippetOutput("Wire compatibility");
        #region PartSan_WireCompatible
        var plain = new PlainCredentials("alice", ApiKey);
        foreach (var serializer in (IByteSerializer[])[MemoryPackByteSerializer.Default, MessagePackByteSerializer.Default]) {
            using var plainBytes = serializer.Write(plain);
            using var sanitizedBytes = serializer.Write(credentials);
            var isSame = plainBytes.WrittenSpan.SequenceEqual(sanitizedBytes.WrittenSpan);
            WriteLine($"{serializer.GetType().Name}: {isSame}");
        }
        #endregion

        StartSnippetOutput("Sanitizers");
        #region PartSan_Sanitizers
        WriteLine(Sanitizer.Sanitize<Sanitizers.Hidden>(ApiKey));
        WriteLine(Sanitizer.Sanitize<Sanitizers.LengthHint>(ApiKey));
        WriteLine(Sanitizer.Sanitize<Sanitizers.PrefixAndLengthHint>(ApiKey));
        WriteLine(Sanitizer.Sanitize<Sanitizers.Fingerprint>(ApiKey));
        WriteLine(Sanitizer.Sanitize<Sanitizers.SessionString>("Ab3fSomeLongSessionId"));
        WriteLine(Sanitizer.Sanitize<Sanitizers.RpcRequestQuery>("?s=Ab3fSomeLongSessionId&p=Zm9vYmFy&f=mempack6"));
        #endregion

        StartSnippetOutput("Fingerprint correlates");
        #region PartSan_Fingerprint
        WriteLine(Sanitizer.Sanitize<Sanitizers.Fingerprint>("alice@example.com"));
        WriteLine(Sanitizer.Sanitize<Sanitizers.Fingerprint>("alice@example.com")); // Same input, same output
        WriteLine(Sanitizer.Sanitize<Sanitizers.Fingerprint>("bob@example.com"));
        #endregion

        StartSnippetOutput("Null and empty pass through");
        #region PartSan_NullAndEmpty
        WriteLine(Sanitizer.Sanitize<Sanitizers.Hidden>(null) is null);
        WriteLine($"'{Sanitizer.Sanitize<Sanitizers.Hidden>("")}'");
        #endregion

        StartSnippetOutput("Turning it on");
        #region PartSan_Suspend
        WriteLine(credentials.ApiKey.ToString()); // Inactive by default
        using (Sanitization.Begin()) {
            WriteLine(credentials.ApiKey.ToString());
            using (Sanitization.Suspend())
                WriteLine(credentials.ApiKey.ToString());
        }
        #endregion

        #region PartSan_AddSanitizingLoggerFactory
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddSanitizingLoggerFactory(
            c => c.GetRequiredService<IHostEnvironment>().IsProduction()));

        // No ILoggingBuilder to hook? Wrap the factory itself
        StaticLog.Factory = StaticLog.Factory.Sanitizing();
        #endregion

        StartSnippetOutput("A log argument must defer its ToString()");
        #region PartSan_LogArgument
        var deferred = new SanitizedString<Sanitizers.RpcRequestQuery>("?s=Ab3fSomeLongSessionId");
        var eager = Sanitizer.MaybeSanitize<Sanitizers.RpcRequestQuery>("?s=Ab3fSomeLongSessionId");
        using (Sanitization.Begin()) { // What SanitizingLogger opens around Log(...)
            WriteLine(deferred); // ToString() runs inside the scope
            WriteLine(eager); // Already a plain string by the time the scope opens
        }
        #endregion

        StartSnippetOutput("MaybeSanitize");
        #region PartSan_MaybeSanitize_Use
        var request = new ConnectRequest("/rpc/ws", "?s=Ab3fSomeLongSessionId&f=mempack6");
        WriteLine(request.LogTitle);
        using (Sanitization.Begin())
            WriteLine(request.LogTitle); // MaybeSanitize honors the scope, Sanitize wouldn't
        #endregion

        StartSnippetOutput("Custom sanitizers");
        #region PartSan_CustomSanitizer_Use
        WriteLine(Sanitizer.Sanitize<EmailDomain>("alice@example.com"));
        WriteLine(Sanitizer.Sanitize<DownloadQuery>("?id=17&token=Zm9vYmFy"));
        using (Sanitization.Begin())
            WriteLine(new SanitizedString<EmailDomain>("alice@example.com"));
        #endregion

        StartSnippetOutput("Equality");
        #region PartSan_Equality
        var a = new SanitizedString<Sanitizers.LengthHint>("hunter2-hunter2");
        var b = new SanitizedString<Sanitizers.LengthHint>("abcdefghijklmno"); // Same length, same mask
        using (Sanitization.Begin())
            WriteLine($"{a} == {b}: {a == b}");
        #endregion

        StartSnippetOutput("The wrong way");
        #region PartSan_DontMaskInGetter_Use
        var bad = new BadCredentials { ApiKey = ApiKey };
        WriteLine(SystemJsonSerializer.Default.Write(bad));
        #endregion

        await Task.CompletedTask;
    }
}
