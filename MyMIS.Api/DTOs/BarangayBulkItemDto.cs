using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

public class BarangayBulkItemDto
{
  [Required, MaxLength(150)]
  public string? Name { get; set; }

  [MaxLength(20)]
  public string? PsgcCode { get; set; }

  [MaxLength(10)]
  public string? ZipCode { get; set; }
}