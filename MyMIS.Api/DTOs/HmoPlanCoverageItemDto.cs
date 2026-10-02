using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

public class HmoPlanCoverageItemDto
{
  [Required, MaxLength(300)]
  public string Name { get; set; } = string.Empty;

  [Range(typeof(decimal), "0.01", "9999999999.99", ParseLimitsInInvariantCulture = true)]
  public decimal? LimitAmount { get; set; }

  [MaxLength(500)]
  public string? Notes { get; set; }
}