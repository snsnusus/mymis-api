using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.Models;

public class HmoPlanCoverage
{
  public int Id { get; set; }

  public int HmoPlanId { get; set; }

  public HmoPlan HmoPlan { get; set; } = null!;

  [Required, MaxLength(100)]
  public string Category { get; set; } = string.Empty;

  [Required, MaxLength(300)]
  public string Name { get; set; } = string.Empty;

  public decimal? LimitAmount { get; set; }

  [MaxLength(500)]
  public string? Notes { get; set; }

  public int SortOrder { get; set; }
}