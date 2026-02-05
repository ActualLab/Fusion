using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ActualLab.Fusion.Extensions.Services;

/// <summary>
/// Entity Framework entity representing a key-value pair with optional expiration.
/// </summary>
[Table("_KeyValues")]
[Index(nameof(ExpiresAt))]
public class DbKeyValue
{
    [Key] public string Key { get; set; } = "";
    public string Value { get; set; } = "";

    public DateTime? ExpiresAt {
        get => field.DefaultKind(DateTimeKind.Utc);
        set => field = value.DefaultKind(DateTimeKind.Utc);
    }
}
