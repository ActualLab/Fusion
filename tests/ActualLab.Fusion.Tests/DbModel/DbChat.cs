using System.ComponentModel.DataAnnotations;
using MessagePack;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.Tests.DbModel;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
#if NET8_0_OR_GREATER
[MessagePackObject(true, SuppressSourceGeneration = true)]
#else
[MessagePackObject(true)]
#endif
[Index(nameof(Title))]
public partial record DbChat : DbEntityWithInt64Key
{
    [Required, MaxLength(120)]
    [DataMember, MemoryPackOrder(1)]
    public string Title { get; init; } = "";

    [Required]
    [DataMember, MemoryPackOrder(2)]
    public DbUser Author { get; init; } = null!;
}
