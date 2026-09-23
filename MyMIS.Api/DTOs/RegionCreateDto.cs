using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

public class RegionCreateDto
{
  [Required]
  [MaxLength(150)]
  public string Name { get; set; } = string.Empty;

  [MaxLength(20)]
  public string? PsgcCode { get; set; }
}