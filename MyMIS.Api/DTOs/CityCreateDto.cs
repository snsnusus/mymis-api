using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

public class CityCreateDto
{
  [Required]
  [MaxLength(150)]
  public string Name { get; set; } = string.Empty;

  [MaxLength(20)]
  public string? PsgcCode { get; set; }

  public int RegionId { get; set; }
}