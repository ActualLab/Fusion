using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Samples.TodoApp.Services.Db;

/// <summary>
/// Entity Framework entity representing a user identity record in the database.
/// </summary>
[Table("UserIdentities")]
[Index(nameof(Id))]
public class DbUserIdentity : IHasId<string>
{
    [Key]
    public string Id { get; set; } = "";
    [Column("UserId")]
    public string DbUserId { get; set; } = "";
    public string Secret { get; set; } = "";
}
