using System.ComponentModel.DataAnnotations;

namespace ActualLab.Tests.CommandR.Services;

public class User
{
    [Key]
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}
