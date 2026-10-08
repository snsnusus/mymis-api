using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;

namespace MyMIS.Api.Services;

public class OfficeService(AppDbContext context)
{
  private readonly AppDbContext _context = context;

  private static string NormalizeName(string name) =>
    name.Trim().ToUpperInvariant();

  private static void ApplyDto(Office office, OfficeCreateDto dto)
  {
    office.Name = dto.Name.Trim();
    office.NormalizedName = NormalizeName(dto.Name);
    office.City = dto.City.Trim();
    office.CountryCode = dto.CountryCode.Trim().ToUpperInvariant();
    office.Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim();
  }

  public async Task<bool> NameExistsAsync(string name, int? excludeId = null)
  {
    var normalized = NormalizeName(name);

    return await _context.Offices
      .AnyAsync(o =>
        o.NormalizedName == normalized &&
        (excludeId == null || o.Id != excludeId));
  }

  public async Task<List<OfficeResponseDto>> GetAllAsync()
  {
    return await _context.Offices
      .OrderBy(o => o.Name)
      .Select(o => new OfficeResponseDto
      {
        Id = o.Id,
        Name = o.Name,
        City = o.City,
        CountryCode = o.CountryCode,
        Address = o.Address
      })
      .ToListAsync();
  }

  public async Task<OfficeResponseDto?> GetByIdAsync(int id)
  {
    return await _context.Offices
      .Where(o => o.Id == id)
      .Select(o => new OfficeResponseDto
      {
        Id = o.Id,
        Name = o.Name,
        City = o.City,
        CountryCode = o.CountryCode,
        Address = o.Address
      })
      .FirstOrDefaultAsync();
  }

  public async Task<OfficeResponseDto> CreateAsync(OfficeCreateDto dto)
  {
    var office = new Office();
    ApplyDto(office, dto);

    _context.Offices.Add(office);
    await _context.SaveChangesAsync();

    return await GetByIdAsync(office.Id)
      ?? throw new InvalidOperationException("Failed to reload newly created office.");
  }

  public async Task<OfficeResponseDto?> UpdateAsync(int id, OfficeCreateDto dto)
  {
    var office = await _context.Offices.FindAsync(id);
    if (office is null) return null;

    ApplyDto(office, dto);

    await _context.SaveChangesAsync();
    return await GetByIdAsync(office.Id);
  }

  public async Task<bool> DeleteAsync(int id)
  {
    var office = await _context.Offices.FindAsync(id);
    if (office is null) return false;

    _context.Offices.Remove(office);
    await _context.SaveChangesAsync();
    return true;
  }


}