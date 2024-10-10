using System.ComponentModel.DataAnnotations;
using MessagePack;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.Tests.Model;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Index(nameof(Title))]
public partial record Chat : LongKeyedEntity
{
    [Required, MaxLength(120)]
    [DataMember, MemoryPackOrder(1), MessagePack.Key(1)]
    public string Title { get; init; } = "";

    [Required]
    [DataMember, MemoryPackOrder(2), MessagePack.Key(2)]
    public User Author { get; init; } = default!;
}
