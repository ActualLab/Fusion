using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using ActualLab.Versioning;

namespace ActualLab.Fusion.Authentication.Services;

[Table("_Sessions")]
[Index(nameof(CreatedAt), nameof(IsSignOutForced))]
[Index(nameof(LastSeenAt), nameof(IsSignOutForced))]
[Index(nameof(UserId), nameof(IsSignOutForced))]
[Index(nameof(IPAddress), nameof(IsSignOutForced))]
public class DbSessionInfo<TDbUserId> : IHasId<string>, IHasVersion<long>
{
    private NewtonsoftJsonSerialized<ImmutableOptionSet> _options = ImmutableOptionSet.Empty;
    private DateTime _createdAt;
    private DateTime _lastSeenAt;

    [Key, StringLength(256)]
    public string Id { get; set; } = "";

    [ConcurrencyCheck]
    public long Version { get; set; }

    public DateTime CreatedAt {
        get => _createdAt.DefaultKind(DateTimeKind.Utc);
        set => _createdAt = value.DefaultKind(DateTimeKind.Utc);
    }
    public DateTime LastSeenAt {
        get => _lastSeenAt.DefaultKind(DateTimeKind.Utc);
        set => _lastSeenAt = value.DefaultKind(DateTimeKind.Utc);
    }

    public string IPAddress { get; set; } = "";
    public string UserAgent { get; set; } = "";

    // Authentication
    public string AuthenticatedIdentity { get; set; } = "";
    public TDbUserId? UserId { get; set; } = default;
    public bool IsSignOutForced { get; set; }

    // Options
    public string OptionsJson {
        get => _options.Data;
        set => _options = value;
    }

    [NotMapped]
    public ImmutableOptionSet Options {
#pragma warning disable IL2026
        get => _options.Value;
#pragma warning restore IL2026
        set => _options = value;
    }
}
