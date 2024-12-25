using System.ComponentModel.DataAnnotations;
using MessagePack;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.Tests.Model;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true, SuppressSourceGeneration = true)]
[Index(nameof(Title))]
public partial record Chat : LongKeyedEntity
{
    [Required, MaxLength(120)]
    [DataMember, MemoryPackOrder(1)]
    public string Title { get; init; } = "";

    [Required]
    [DataMember, MemoryPackOrder(2)]
    public User Author { get; init; } = null!;
}
