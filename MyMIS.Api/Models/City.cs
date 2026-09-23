using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.Models;

public class City
{
  public int Id { get; set; }

  [Required]
  [MaxLength(150)]
  public string Name { get; set; } = string.Empty;

  [MaxLength(20)]
  public string? PsgcCode { get; set; }

  public int RegionId { get; set; }

  public Region Region { get; set; } = null!;
}