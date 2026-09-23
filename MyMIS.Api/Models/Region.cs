using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.Models;

public class Region
{
  public int Id { get; set; }

  [Required]
  [MaxLength(150)]
  public string Name { get; set; } = string.Empty;

  [MaxLength(20)]
  public string? PsgcCode { get; set; }
}