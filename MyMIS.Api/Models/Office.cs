using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.Models;

public class Office
{
  public int Id { get; set; }

  [Required]
  [MaxLength(150)]
  public string Name { get; set; } = string.Empty;

  // Trimmed, uppercased copy of Name (e.g. "MANILA OFFICE").
  // Used only for case-insensitive uniqueness and lookups, and is never shown to users.
  [Required]
  [MaxLength(150)]
  public string NormalizedName { get; set; } = string.Empty;

  [Required]
  [MaxLength(150)]
  public string City { get; set; } = string.Empty;

  [Required]
  [MaxLength(2)]
  public string CountryCode { get; set; } = string.Empty;

  [MaxLength(300)]
  public string? Address { get; set; }
}