using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

public class HobbyCreateDto
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}