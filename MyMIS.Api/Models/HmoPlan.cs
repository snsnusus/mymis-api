using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.Models;

public class HmoPlan
{
  public int Id { get; set; }

  public int HmoProviderId { get; set; }

  public HmoProvider HmoProvider { get; set; } = null!;

  [Required, MaxLength(150)]
  public string Name { get; set; } = string.Empty;

  // Trimmed, uppercased copy of Name (e.g. "GOLD PLUS").
  // Used only for case-insensitive uniqueness per provider; never shown to users.
  [Required, MaxLength(150)]
  public string NormalizedName { get; set; } = string.Empty;

  public HmoPlanTier Tier { get; set; }

  public HmoRoomType RoomType { get; set; }

  public decimal MaximumBenefitLimit { get; set; }

  public HmoPremiumFrequency PremiumFrequency { get; set; }

  public decimal PremiumCost { get; set; }

  public decimal EmployerSubsidyPercentage { get; set; }

  public bool PecCovered { get; set; }

  public decimal? PecLimit { get; set; }

  public bool AllowDependents { get; set; }

  public decimal? DependentPremiumCost { get; set; }

  public decimal? DependentSubsidyPercentage { get; set; }

  public bool IsActive { get; set; } = true;

  public DateTime CreatedAt { get; set; }

  public DateTime UpdatedAt { get; set; }

  public List<HmoPlanCoverage> Coverages { get; set; } = [];
}