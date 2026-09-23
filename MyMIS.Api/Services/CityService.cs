using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;

namespace MyMIS.Api.Services;

public class CityService(AppDbContext context)
{
  private readonly AppDbContext _context = context;

  public async Task<List<CityResponseDto>> GetAllAsync(int? regionId)
  {
    var query = _context.Cities
        .Include(c => c.Region)
        .AsQueryable();

    if (regionId.HasValue)
    {
      query = query.Where(c => c.RegionId == regionId.Value);
    }

    return await query
        .Select(c => new CityResponseDto
        {
          Id = c.Id,
          Name = c.Name,
          PsgcCode = c.PsgcCode,
          RegionId = c.RegionId,
          RegionName = c.Region.Name
        })
        .ToListAsync();
  }

  public async Task<CityResponseDto?> GetByIdAsync(int id)
  {
    return await _context.Cities
        .Include(c => c.Region)
        .Where(c => c.Id == id)
        .Select(c => new CityResponseDto
        {
          Id = c.Id,
          Name = c.Name,
          PsgcCode = c.PsgcCode,
          RegionId = c.RegionId,
          RegionName = c.Region.Name
        })
        .FirstOrDefaultAsync();
  }

  public async Task<CityResponseDto> CreateAsync(CityCreateDto dto)
  {
    var city = new City
    {
      Name = dto.Name,
      PsgcCode = dto.PsgcCode,
      RegionId = dto.RegionId
    };
    _context.Cities.Add(city);
    await _context.SaveChangesAsync();

    return await GetByIdAsync(city.Id)
        ?? throw new InvalidOperationException("Failed to reload newly created city.");
  }

  public async Task<CityResponseDto?> UpdateAsync(int id, CityCreateDto dto)
  {
    var city = await _context.Cities.FindAsync(id);
    if (city is null) return null;

    city.Name = dto.Name;
    city.PsgcCode = dto.PsgcCode;
    city.RegionId = dto.RegionId;

    await _context.SaveChangesAsync();
    return await GetByIdAsync(city.Id);
  }

  public async Task<bool> DeleteAsync(int id)
  {
    var city = await _context.Cities.FindAsync(id);
    if (city is null) return false;

    _context.Cities.Remove(city);
    await _context.SaveChangesAsync();
    return true;
  }
}