using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Stl.Versioning;

namespace Stl.Fusion.Authentication.Services;

[Table("Users")]
[Index(nameof(Name))]
public class DbUser<TDbUserId> : IHasId<TDbUserId>, IHasVersion<long>
    where TDbUserId : notnull
{
    private readonly NewtonsoftJsonSerialized<ImmutableDictionary<string, string>> _claims =
        NewtonsoftJsonSerialized.New(ImmutableDictionary<string, string>.Empty);

    [Key] public TDbUserId Id { get; set; } = default!;
    [ConcurrencyCheck] public long Version { get; set; }

    [MinLength(3)]
    public string Name { get; set; } = "";

    public string ClaimsJson {
        get => _claims.Data;
        set => _claims.Data = value;
    }

    [NotMapped]
    public ImmutableDictionary<string, string> Claims {
        get => _claims.Value;
        set => _claims.Value = value;
    }

    public List<DbUserIdentity<TDbUserId>> Identities { get; } = new();
}
