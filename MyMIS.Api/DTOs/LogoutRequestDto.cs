using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

public class LogoutRequestDto
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}