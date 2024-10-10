using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MessagePack;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.Tests.Model;

[Table("TestUsers")]
[Index(nameof(Name))]
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
public partial record User : LongKeyedEntity
{
    [Required, MaxLength(120)]
    [DataMember, MemoryPackOrder(1), MessagePack.Key(1)]
    public string Name { get; init; } = "";

    [Required, MaxLength(250)]
    [DataMember, MemoryPackOrder(2), MessagePack.Key(2)]
    public string Email { get; init; } = "";
}
