using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Stl.Fusion.Authentication.Services;

[Table("UserIdentities")]
[Index(nameof(Id))]
public class DbUserIdentity<TDbUserId> : IHasId<string>
    where TDbUserId : notnull
{
    [Key]
    public string Id { get; set; } = "";
    [Column("UserId")]
    public TDbUserId DbUserId { get; set; } = default!;
    public string Secret { get; set; } = "";
}
