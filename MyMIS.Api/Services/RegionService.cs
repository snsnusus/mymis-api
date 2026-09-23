using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;

namespace MyMIS.Api.Services;

public class RegionService(AppDbContext context)
{
  private readonly AppDbContext _context = context;

  public async Task<List<RegionResponseDto>> GetAllAsync()
  {
    return await _context.Regions
        .Select(r => new RegionResponseDto
        {
          Id = r.Id,
          Name = r.Name,
          PsgcCode = r.PsgcCode
        })
        .ToListAsync();
  }

  public async Task<RegionResponseDto?> GetByIdAsync(int id)
  {
    return await _context.Regions
        .Where(r => r.Id == id)
        .Select(r => new RegionResponseDto
        {
          Id = r.Id,
          Name = r.Name,
          PsgcCode = r.PsgcCode
        })
        .FirstOrDefaultAsync();
  }

  public async Task<RegionResponseDto> CreateAsync(RegionCreateDto dto)
  {
    var region = new Region
    {
      Name = dto.Name,
      PsgcCode = dto.PsgcCode
    };
    _context.Regions.Add(region);
    await _context.SaveChangesAsync();

    return await GetByIdAsync(region.Id)
        ?? throw new InvalidOperationException("Failed to reload newly created region.");
  }

  public async Task<RegionResponseDto?> UpdateAsync(int id, RegionCreateDto dto)
  {
    var region = await _context.Regions.FindAsync(id);
    if (region is null) return null;

    region.Name = dto.Name;
    region.PsgcCode = dto.PsgcCode;

    await _context.SaveChangesAsync();
    return await GetByIdAsync(region.Id);
  }

  public async Task<bool> DeleteAsync(int id)
  {
    var region = await _context.Regions.FindAsync(id);
    if (region is null) return false;

    _context.Regions.Remove(region);
    await _context.SaveChangesAsync();
    return true;
  }
}