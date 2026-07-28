using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using ActualLab.Versioning;

namespace ActualLab.Fusion.Authentication.Services;

/// <summary>
/// Entity Framework entity representing a session record in the database,
/// including authentication state and metadata.
/// </summary>
[Table("_Sessions")]
[Index(nameof(CreatedAt), nameof(IsSignOutForced))]
[Index(nameof(LastSeenAt), nameof(IsSignOutForced))]
[Index(nameof(UserId), nameof(IsSignOutForced))]
[Index(nameof(IPAddress), nameof(IsSignOutForced))]
public class DbSessionInfo<TDbUserId> : IHasId<string>, IHasVersion<long>
{
    private NewtonsoftJsonSerialized<PropertyBag> _options = PropertyBag.Empty;

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
    public TDbUserId? UserId { get; set; } = default;
    public bool IsSignOutForced { get; set; }

    // Options
    public string OptionsJson {
        get => _options.Data;
        set {
            // Rows written before the ImmutableOptionSet -> PropertyBag migration may not parse anymore.
            // Session options are small, session-scoped and rebuildable, so such a row loses its options
            // instead of becoming unreadable. The session Id is intentionally not logged - it's a credential.
            try {
                _options = value;
            }
            catch (Exception e) {
                StaticLog.For<DbSessionInfo<TDbUserId>>()
                    .LogWarning(e, "Can't deserialize session options, resetting them to empty");
                _options = PropertyBag.Empty;
            }
        }
    }

    [NotMapped]
    public PropertyBag Options {
        get => _options.Value;
        set => _options = value;
    }
}
