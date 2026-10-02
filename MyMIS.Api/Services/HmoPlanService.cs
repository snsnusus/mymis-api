using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;
using MyMIS.Api.Models;

namespace MyMIS.Api.Services;

public class HmoPlanService(AppDbContext context)
{
  private readonly AppDbContext _context = context;

  public async Task<List<HmoPlanSummaryDto>?> GetByProviderAsync(int providerId)
  {
    var providerExists = await _context.HmoProviders.AnyAsync(p => p.Id == providerId);
    if (!providerExists)
    {
      return null;
    }

    return await _context.HmoPlans
      .AsNoTracking()
      .Where(p => p.HmoProviderId == providerId)
      .OrderBy(p => p.Name)
      .Select(p => new HmoPlanSummaryDto
      {
        Id = p.Id,
        HmoProviderId = p.HmoProviderId,
        Name = p.Name,
        Tier = p.Tier,
        RoomType = p.RoomType,
        MaximumBenefitLimit = p.MaximumBenefitLimit,
        PremiumFrequency = p.PremiumFrequency,
        PremiumCost = p.PremiumCost,
        AllowDependents = p.AllowDependents,
        IsActive = p.IsActive
      })
      .ToListAsync();
  }

  public async Task<HmoPlanResponseDto?> GetByIdAsync(int id)
  {
    var plan = await _context.HmoPlans
      .AsNoTracking()
      .Include(p => p.HmoProvider)
      .Include(p => p.Coverages)
      .FirstOrDefaultAsync(p => p.Id == id);

    return plan is null ? null : ToResponse(plan);
  }

  public async Task<bool> ExistsAsync(int id)
  {
    return await _context.HmoPlans.AnyAsync(p => p.Id == id);
  }

  public async Task<bool> NameExistsAsync(int providerId, string name, int? excludeId = null)
  {
    var normalized = name.Trim().ToLowerInvariant();

    return await _context.HmoPlans.AnyAsync(p =>
      p.HmoProviderId == providerId &&
#pragma warning disable CA1862
      p.Name.ToLower() == normalized &&
#pragma warning restore CA1862
      (excludeId == null || p.Id != excludeId));
  }

  public async Task<ServiceResult<HmoPlanResponseDto>> CreateAsync(HmoPlanCreateDto dto)
  {
    var providerId = dto.HmoProviderId!.Value;

    var providerExists = await _context.HmoProviders.AnyAsync(p => p.Id == providerId);
    if (!providerExists)
    {
      return ServiceResult<HmoPlanResponseDto>.Invalid("HMO provider does not exist.");
    }

    var now = DateTime.UtcNow;
    var plan = new HmoPlan
    {
      HmoProviderId = providerId,
      CreatedAt = now,
      UpdatedAt = now
    };
    ApplyDto(plan, dto);
    ReplaceCoverages(plan, dto.CoverageGroups);

    _context.HmoPlans.Add(plan);
    await _context.SaveChangesAsync();

    var created = await GetByIdAsync(plan.Id)
      ?? throw new InvalidOperationException("Created HMO plan could not be reloaded.");

    return ServiceResult<HmoPlanResponseDto>.Success(created);
  }

  public async Task<ServiceResult<HmoPlanResponseDto>> UpdateAsync(int id, HmoPlanCreateDto dto)
  {
    var plan = await _context.HmoPlans
      .Include(p => p.Coverages)
      .FirstOrDefaultAsync(p => p.Id == id);

    if (plan is null)
    {
      return ServiceResult<HmoPlanResponseDto>.NotFound("HMO plan not found.");
    }

    if (plan.HmoProviderId != dto.HmoProviderId)
    {
      return ServiceResult<HmoPlanResponseDto>.Invalid(
          "A plan cannot be moved to a different HMO provider.");
    }

    ApplyDto(plan, dto);
    ReplaceCoverages(plan, dto.CoverageGroups);
    plan.UpdatedAt = DateTime.UtcNow;

    await _context.SaveChangesAsync();

    var updated = await GetByIdAsync(id)
        ?? throw new InvalidOperationException("Updated HMO plan could not be reloaded.");

    return ServiceResult<HmoPlanResponseDto>.Success(updated);
  }

  private static void ApplyDto(HmoPlan plan, HmoPlanCreateDto dto)
  {
    plan.Name = dto.Name.Trim();
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

  private static void ReplaceCoverages(HmoPlan plan, List<HmoPlanCoverageGroupDto>? groups)
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

  private static HmoPlanResponseDto ToResponse(HmoPlan plan) => new()
  {
    Id = plan.Id,
    HmoProviderId = plan.HmoProviderId,
    HmoProviderName = plan.HmoProvider.Name,
    Name = plan.Name,
    Tier = plan.Tier,
    RoomType = plan.RoomType,
    MaximumBenefitLimit = plan.MaximumBenefitLimit,
    PremiumFrequency = plan.PremiumFrequency,
    PremiumCost = plan.PremiumCost,
    EmployerSubsidyPercentage = plan.EmployerSubsidyPercentage,
    PecCovered = plan.PecCovered,
    PecLimit = plan.PecLimit,
    AllowDependents = plan.AllowDependents,
    DependentPremiumCost = plan.DependentPremiumCost,
    DependentSubsidyPercentage = plan.DependentSubsidyPercentage,
    IsActive = plan.IsActive,
    CreatedAt = plan.CreatedAt,
    UpdatedAt = plan.UpdatedAt,
    CoverageGroups = [.. plan.Coverages
      .OrderBy(c => c.SortOrder)
      .GroupBy(c => c.Category)
      .Select(g => new HmoPlanCoverageGroupDto
      {
        Category = g.Key,
        Items = [.. g.Select(c => new HmoPlanCoverageItemDto
        {
          Name = c.Name,
          LimitAmount = c.LimitAmount,
          Notes = c.Notes
        })]
      })]
  };

  private static string? NullIfBlank(string? value) =>
    string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}