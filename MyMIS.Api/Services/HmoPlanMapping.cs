using MyMIS.Api.DTOs;
using MyMIS.Api.Models;

namespace MyMIS.Api.Services;

internal static class HmoPlanMapping
{
  public static string NormalizeName(string name) =>
    name.Trim().ToUpperInvariant();
  public static void ApplyDto(HmoPlan plan, HmoPlanFieldsDto dto)
  {
    plan.Name = dto.Name.Trim();
    plan.NormalizedName = NormalizeName(dto.Name);
    plan.Tier = dto.Tier!.Value;
    plan.RoomType = dto.RoomType!.Value;
    plan.MaximumBenefitLimit = dto.MaximumBenefitLimit!.Value;
    plan.PremiumFrequency = dto.PremiumFrequency!.Value;
    plan.PremiumCost = dto.PremiumCost!.Value;
    plan.EmployerSubsidyPercentage = dto.EmployerSubsidyPercentage!.Value;

    plan.PecCovered = dto.PecCovered;
    plan.PecLimit = dto.PecCovered ? dto.PecLimit : null;

    plan.AllowDependents = dto.AllowDependents;
    plan.DependentPremiumCost = dto.AllowDependents ? dto.DependentPremiumCost : null;
    plan.DependentSubsidyPercentage = dto.AllowDependents ? dto.DependentSubsidyPercentage : null;

    plan.IsActive = dto.IsActive;
  }

  public static void ReplaceCoverages(HmoPlan plan, List<HmoPlanCoverageGroupDto>? groups)
  {
    plan.Coverages.Clear();

    var sortOrder = 0;
    foreach (var group in groups ?? [])
    {
      var category = group.Category.Trim();

      foreach (var item in group.Items)
      {
        plan.Coverages.Add(new HmoPlanCoverage
        {
          Category = category,
          Name = item.Name.Trim(),
          LimitAmount = item.LimitAmount,
          Notes = NullIfBlank(item.Notes),
          SortOrder = sortOrder++
        });
      }
    }
  }

  private static string? NullIfBlank(string? value) =>
      string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}