using System.ComponentModel.DataAnnotations;
using MessagePack;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.Tests.DbModel;

[Index(nameof(Name))]
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
#if NET8_0_OR_GREATER
[MessagePackObject(true, SuppressSourceGeneration = true)]
#else
[MessagePackObject(true)]
#endif
public partial record DbUser : DbEntityWithInt64Key
{
    [Required, MaxLength(120)]
    [DataMember, MemoryPackOrder(1)]
    public string Name { get; init; } = "";

    [Required, MaxLength(250)]
    [DataMember, MemoryPackOrder(2)]
    public string Email { get; init; } = "";
}
