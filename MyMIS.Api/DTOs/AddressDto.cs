using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.Dtos;

public class AddressDto
{
  [Required, MaxLength(200)]
  public string AddressLine1 { get; set; } = string.Empty;

  [MaxLength(200)]
  public string? AddressLine2 { get; set; }

  [Required]
  public int? BarangayId { get; set; }

  [Required, MaxLength(10)]
  public string PostalCode { get; set; } = string.Empty;
}