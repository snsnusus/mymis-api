using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;

namespace MyMIS.Api.Services;

public class HmoProviderService(AppDbContext context)
{
  private readonly AppDbContext _context = context;

  private static string NormalizeName(string name) =>
    name.Trim().ToUpperInvariant();
  private static string NormalizeCode(string code) =>
    code.Trim().ToUpperInvariant();
  private static string? NullIfBlank(string? value) =>
    string.IsNullOrWhiteSpace(value) ? null : value.Trim();

  private static readonly Expression<Func<HmoProvider, HmoProviderResponseDto>> ToResponse =
    p => new HmoProviderResponseDto
    {
      Id = p.Id,
      Code = p.Code,
      Name = p.Name,
      AccountManagerName = p.AccountManagerName,
      Hotline = p.Hotline,
      SupportEmail = p.SupportEmail,
      WebsiteUrl = p.WebsiteUrl,
      ContractStartDate = p.ContractStartDate,
      ContractEndDate = p.ContractEndDate,
      IsActive = p.IsActive,
      CreatedAt = p.CreatedAt,
      UpdatedAt = p.UpdatedAt
    };

  private static void ApplyDto(HmoProvider provider, HmoProviderFieldsDto dto)
  {
    provider.Code = NormalizeCode(dto.Code);
    provider.Name = dto.Name.Trim();
    provider.NormalizedName = NormalizeName(dto.Name);
    provider.AccountManagerName = NullIfBlank(dto.AccountManagerName);
    provider.Hotline = NullIfBlank(dto.Hotline);
    provider.SupportEmail = NullIfBlank(dto.SupportEmail);
    provider.WebsiteUrl = NullIfBlank(dto.WebsiteUrl);
    provider.ContractStartDate = dto.ContractStartDate!.Value;
    provider.ContractEndDate = dto.ContractEndDate!.Value;
    provider.IsActive = dto.IsActive;
  }

  public async Task<List<HmoProviderResponseDto>> GetAllAsync()
  {
    return await _context.HmoProviders
      .AsNoTracking()
      .OrderBy(p => p.Name)
      .Select(ToResponse)
      .ToListAsync();
  }

  public async Task<HmoProviderResponseDto?> GetByIdAsync(int id)
  {
    return await _context.HmoProviders
      .AsNoTracking()
      .Where(p => p.Id == id)
      .Select(ToResponse)
      .FirstOrDefaultAsync();
  }

  public async Task<bool> CodeExistsAsync(string code, int? excludeId = null)
  {
    var normalized = NormalizeCode(code);

    return await _context.HmoProviders.AnyAsync(p =>
      p.Code == normalized &&
      (excludeId == null || p.Id != excludeId));
  }

  public async Task<bool> NameExistsAsync(string name, int? excludeId = null)
  {
    var normalized = NormalizeName(name);

    return await _context.HmoProviders.AnyAsync(p =>
      p.NormalizedName == normalized &&
      (excludeId == null || p.Id != excludeId));
  }

  public async Task<HmoProviderResponseDto> CreateAsync(HmoProviderCreateDto dto)
  {
    var now = DateTime.UtcNow;
    var provider = new HmoProvider { CreatedAt = now, UpdatedAt = now };
    ApplyDto(provider, dto);

    foreach (var planDto in dto.Plans ?? [])
    {
      var plan = new HmoPlan { CreatedAt = now, UpdatedAt = now };
      HmoPlanMapping.ApplyDto(plan, planDto);
      HmoPlanMapping.ReplaceCoverages(plan, planDto.CoverageGroups);
      provider.Plans.Add(plan);
    }

    _context.HmoProviders.Add(provider);
    await _context.SaveChangesAsync();

    return await GetByIdAsync(provider.Id)
      ?? throw new InvalidOperationException("Created HMO provider could not be reloaded.");
  }

  public async Task<HmoProviderResponseDto?> UpdateAsync(int id, HmoProviderUpdateDto dto)
  {
    var provider = await _context.HmoProviders.FindAsync(id);
    if (provider is null)
    {
      return null;
    }

    ApplyDto(provider, dto);
    provider.UpdatedAt = DateTime.UtcNow;

    await _context.SaveChangesAsync();

    return await GetByIdAsync(id);
  }


}