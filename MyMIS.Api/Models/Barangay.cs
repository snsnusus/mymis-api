using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.Models;

public class Barangay
{
  public int Id { get; set; }

  [Required]
  [MaxLength(150)]
  public string Name { get; set; } = string.Empty;

  [MaxLength(20)]
  public string? PsgcCode { get; set; }

  [MaxLength(10)]
  public string? ZipCode { get; set; }

  public int CityId { get; set; }

  public City City { get; set; } = null!;
}