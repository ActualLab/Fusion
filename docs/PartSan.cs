using System.Runtime.Serialization;
using ActualLab.Compliance;
using MemoryPack;
using MessagePack;
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
    public override string Apply(string value)
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

        StartSnippetOutput("Suspension");
        #region PartSan_Suspend
        WriteLine(credentials.ApiKey.ToString());
        using (Sanitization.Begin())
            WriteLine(credentials.ApiKey.ToString());
        WriteLine(credentials.ApiKey.ToString());
        #endregion

        StartSnippetOutput("MaybeSanitize");
        #region PartSan_MaybeSanitize_Use
        var request = new ConnectRequest("/rpc/ws", "?s=Ab3fSomeLongSessionId&f=mempack6");
        WriteLine(request.LogTitle);
        using (Sanitization.Suspend())
            WriteLine(request.LogTitle); // MaybeSanitize honors the scope, Sanitize wouldn't
        #endregion

        StartSnippetOutput("Custom sanitizers");
        #region PartSan_CustomSanitizer_Use
        WriteLine(Sanitizer.Sanitize<EmailDomain>("alice@example.com"));
        WriteLine(new SanitizedString<EmailDomain>("alice@example.com").ToString());
        WriteLine(Sanitizer.Sanitize<DownloadQuery>("?id=17&token=Zm9vYmFy"));
        #endregion

        StartSnippetOutput("Equality");
        #region PartSan_Equality
        var a = new SanitizedString<Sanitizers.LengthHint>("hunter2-hunter2");
        var b = new SanitizedString<Sanitizers.LengthHint>("abcdefghijklmno"); // Same length, same mask
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
