using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.Models;

public class Address
{
  [Required, MaxLength(200)]
  public string AddressLine1 { get; set; } = string.Empty;

  [MaxLength(200)]
  public string? AddressLine2 { get; set; }

  // Only the most specific location is stored.
  // City and Region are reached via Barangay.City.Region.
  public int BarangayId { get; set; }
  public Barangay Barangay { get; set; } = null!;

  [MaxLength(10)]
  public string PostalCode { get; set; } = string.Empty;
}