using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

public class RoleCloneDto
{
  [Required]
  [MaxLength(50)]
  public string Name { get; set; } = string.Empty;

  [MaxLength(200)]
  public string? Description { get; set; }
}