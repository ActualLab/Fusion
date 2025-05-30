using System.ComponentModel.DataAnnotations;
using MessagePack;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.Tests.Model;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
#if NET8_0_OR_GREATER
[MessagePackObject(true, SuppressSourceGeneration = true)]
#else
[MessagePackObject(true)]
#endif
[Index(nameof(Date))]
public partial record Message : LongKeyedEntity
{
    [DataMember, MemoryPackOrder(1), MessagePack.Key(1)]
    public DateTime Date { get; init; }

    [Required, MaxLength(1_000_000)]
    [DataMember, MemoryPackOrder(2), MessagePack.Key(2)]
    public string Text { get; init; } = "";

    [Required]
    [DataMember, MemoryPackOrder(3), MessagePack.Key(3)]
    public User Author { get; init; } = null!;

    [Required]
    [DataMember, MemoryPackOrder(4), MessagePack.Key(4)]
    public Chat Chat { get; init; } = null!;
}
