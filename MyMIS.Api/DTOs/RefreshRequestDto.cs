using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

public class RefreshRequestDto
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}