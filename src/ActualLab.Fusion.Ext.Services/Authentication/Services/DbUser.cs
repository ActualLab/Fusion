using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using ActualLab.Versioning;

namespace ActualLab.Fusion.Authentication.Services;

/// <summary>
/// Entity Framework entity representing a user record in the database,
/// with claims and identity associations.
/// </summary>
[Table("Users")]
[Index(nameof(Name))]
public class DbUser<TDbUserId> : IHasId<TDbUserId>, IHasVersion<long>
    where TDbUserId : notnull
{
    private NewtonsoftJsonSerialized<ImmutableDictionary<string, string>> _claims
        = ImmutableDictionary<string, string>.Empty;

    [Key] public TDbUserId Id { get; set; } = default!;

    [ConcurrencyCheck]
    public long Version { get; set; }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume server-side code is fully preserved")]
    [MinLength(3)]
    public string Name { get; set; } = "";

    public string ClaimsJson {
        get => _claims.Data;
        set => _claims = value;
    }

    [NotMapped]
    public ImmutableDictionary<string, string> Claims {
        get => _claims.Value;
        set => _claims = value;
    }

    public List<DbUserIdentity<TDbUserId>> Identities { get; } = new();
}
