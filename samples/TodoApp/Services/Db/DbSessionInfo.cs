using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using ActualLab.Versioning;
using Samples.TodoApp.Abstractions;

namespace Samples.TodoApp.Services.Db;

/// <summary>
/// Entity Framework entity representing a session record in the database.
/// </summary>
[Table("_Sessions")]
[Index(nameof(CreatedAt), nameof(IsSignOutForced))]
[Index(nameof(LastSeenAt), nameof(IsSignOutForced))]
[Index(nameof(UserId), nameof(IsSignOutForced))]
[Index(nameof(IPAddress), nameof(IsSignOutForced))]
public class DbSessionInfo : IHasId<string>, IHasVersion<long>
{
    private NewtonsoftJsonSerialized<ImmutableOptionSet> _options = ImmutableOptionSet.Empty;

    [Key, StringLength(256)]
    public string Id { get; set; } = "";

    [ConcurrencyCheck]
    public long Version { get; set; }

    public DateTime CreatedAt {
        get => field.DefaultKind(DateTimeKind.Utc);
        set => field = value.DefaultKind(DateTimeKind.Utc);
    }
    public DateTime LastSeenAt {
        get => field.DefaultKind(DateTimeKind.Utc);
        set => field = value.DefaultKind(DateTimeKind.Utc);
    }

    public string IPAddress { get; set; } = "";
    public string UserAgent { get; set; } = "";

    // Authentication
    public string AuthenticatedIdentity { get; set; } = "";
    public string? UserId { get; set; }
    public bool IsSignOutForced { get; set; }

    // Options
    public string OptionsJson {
        get => _options.Data;
        set => _options = value;
    }

    [NotMapped]
    public ImmutableOptionSet Options {
        get => _options.Value;
        set => _options = value;
    }

    public SessionInfo ToModel()
        => IsSignOutForced
            ? new(new Moment(CreatedAt)) {
                SessionHash = new Session(Id).Hash,
                IsSignOutForced = true,
                Version = Version,
                CreatedAt = CreatedAt,
                LastSeenAt = LastSeenAt,
            }
            : new() {
                SessionHash = new Session(Id).Hash,
                Version = Version,
                CreatedAt = CreatedAt,
                LastSeenAt = LastSeenAt,
                IPAddress = IPAddress,
                UserAgent = UserAgent,
                Options = Options,
                AuthenticatedIdentity = AuthenticatedIdentity,
                UserId = UserId ?? "",
                IsSignOutForced = IsSignOutForced,
            };

    public void UpdateFrom(SessionInfo source, VersionGenerator<long> versionGenerator)
    {
        Version = versionGenerator.NextVersion(Version);
        LastSeenAt = source.LastSeenAt;
        IPAddress = source.IPAddress;
        UserAgent = source.UserAgent;
        Options = source.Options;
        AuthenticatedIdentity = source.AuthenticatedIdentity;
        UserId = source.UserId.IsNullOrEmpty() ? null : source.UserId;
        IsSignOutForced = source.IsSignOutForced;
    }
}
