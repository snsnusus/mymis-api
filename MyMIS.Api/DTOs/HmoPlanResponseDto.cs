using System.Text.Json.Serialization;
using MyMIS.Api.Models;

namespace MyMIS.Api.DTOs;

public class HmoPlanResponseDto
{
  public int Id { get; set; }

  public int HmoProviderId { get; set; }

  public string HmoProviderName { get; set; } = string.Empty;

  public string Name { get; set; } = string.Empty;

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public HmoPlanTier Tier { get; set; }

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public HmoRoomType RoomType { get; set; }

  public decimal MaximumBenefitLimit { get; set; }

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public HmoPremiumFrequency PremiumFrequency { get; set; }

  public decimal PremiumCost { get; set; }

  public decimal EmployerSubsidyPercentage { get; set; }

  public bool PecCovered { get; set; }

  public decimal? PecLimit { get; set; }

  public bool AllowDependents { get; set; }

  public decimal? DependentPremiumCost { get; set; }

  public decimal? DependentSubsidyPercentage { get; set; }

  public bool IsActive { get; set; }

  public DateTime CreatedAt { get; set; }

  public DateTime UpdatedAt { get; set; }

  public List<HmoPlanCoverageGroupDto> CoverageGroups { get; set; } = [];
}