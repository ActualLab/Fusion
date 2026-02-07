using System.ComponentModel.DataAnnotations;
using MessagePack;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.Tests.DbModel;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[MessagePackObject(true)]
[Index(nameof(Date))]
public partial record DbMessage : DbEntityWithInt64Key
{
    [DataMember, MemoryPackOrder(1), MessagePack.Key(1)]
    public DateTime Date { get; init; }

    [Required, MaxLength(1_000_000)]
    [DataMember, MemoryPackOrder(2), MessagePack.Key(2)]
    public string Text { get; init; } = "";

    [Required]
    [DataMember, MemoryPackOrder(3), MessagePack.Key(3)]
    public DbUser Author { get; init; } = null!;

    [Required]
    [DataMember, MemoryPackOrder(4), MessagePack.Key(4)]
    public DbChat Chat { get; init; } = null!;
}
