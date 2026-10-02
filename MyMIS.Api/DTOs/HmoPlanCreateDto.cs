using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MyMIS.Api.Models;

namespace MyMIS.Api.DTOs;

public class HmoPlanCreateDto : IValidatableObject
{
  [Required]
  public int? HmoProviderId { get; set; }

  [Required, MaxLength(150)]
  public string Name { get; set; } = string.Empty;

  [Required, EnumDataType(typeof(HmoPlanTier))]
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public HmoPlanTier? Tier { get; set; }

  [Required, EnumDataType(typeof(HmoRoomType))]
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public HmoRoomType? RoomType { get; set; }

  [Required]
  [Range(typeof(decimal), "0.01", "9999999999.99", ParseLimitsInInvariantCulture = true)]
  public decimal? MaximumBenefitLimit { get; set; }

  [Required, EnumDataType(typeof(HmoPremiumFrequency))]
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public HmoPremiumFrequency? PremiumFrequency { get; set; }

  [Required]
  [Range(typeof(decimal), "0.01", "9999999999.99", ParseLimitsInInvariantCulture = true)]
  public decimal? PremiumCost { get; set; }

  [Required]
  [Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true)]
  public decimal? EmployerSubsidyPercentage { get; set; }

  public bool PecCovered { get; set; }

  [Range(typeof(decimal), "0.01", "9999999999.99", ParseLimitsInInvariantCulture = true)]
  public decimal? PecLimit { get; set; }

  public bool AllowDependents { get; set; }

  [Range(typeof(decimal), "0.01", "9999999999.99", ParseLimitsInInvariantCulture = true)]
  public decimal? DependentPremiumCost { get; set; }

  [Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true)]
  public decimal? DependentSubsidyPercentage { get; set; }

  public bool IsActive { get; set; } = true;

  public List<HmoPlanCoverageGroupDto> CoverageGroups { get; set; } = [];

  public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
  {
    if (PecCovered && PecLimit is null)
    {
      yield return new ValidationResult(
          "PEC limit is required when PEC is covered.",
          [nameof(PecLimit)]);
    }

    if (PecCovered && PecLimit > MaximumBenefitLimit)
    {
      yield return new ValidationResult(
          "PEC limit cannot exceed the maximum benefit limit.",
          [nameof(PecLimit)]);
    }

    if (AllowDependents && DependentPremiumCost is null)
    {
      yield return new ValidationResult(
          "Dependent premium cost is required when dependents are allowed.",
          [nameof(DependentPremiumCost)]);
    }

    if (AllowDependents && DependentSubsidyPercentage is null)
    {
      yield return new ValidationResult(
          "Dependent subsidy percentage is required when dependents are allowed.",
          [nameof(DependentSubsidyPercentage)]);
    }

    var duplicateCategory = (CoverageGroups ?? [])
        .GroupBy(g => g.Category.Trim(), StringComparer.OrdinalIgnoreCase)
        .FirstOrDefault(group => group.Count() > 1);

    if (duplicateCategory is not null)
    {
      yield return new ValidationResult(
          $"Coverage category \"{duplicateCategory.Key}\" appears more than once.",
          [nameof(CoverageGroups)]);
    }
  }
}