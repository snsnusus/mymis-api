using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

public class CityBulkItemDto
{
  [Required, MaxLength(150)]
  public string? Name { get; set; }

  [MaxLength(20)]
  public string? PsgcCode { get; set; }
}