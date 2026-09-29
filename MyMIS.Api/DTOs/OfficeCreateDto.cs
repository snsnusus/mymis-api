using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

public class OfficeCreateDto
{
  [Required]
  [MaxLength(150)]
  public string Name { get; set; } = string.Empty;

  [Required]
  [MaxLength(150)]
  public string City { get; set; } = string.Empty;

  [Required]
  [RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "CountryCode must be a two-letter ISO code, e.g. PH.")]
  public string CountryCode { get; set; } = string.Empty;

  [MaxLength(300)]
  public string? Address { get; set; }
}