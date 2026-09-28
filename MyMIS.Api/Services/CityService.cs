using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;
using MyMIS.Api.Helpers;
using System.Text.Json;

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

  public async Task<BulkInsertResultDto?> BulkCreateAsync(int regionId, IReadOnlyList<JsonElement> rows)
  {
    // Request-level check, once, before touching any row.
    var region = await _context.Regions.FindAsync(regionId);
    if (region is null)
    {
      return null;
    }

    // One query: every city name already used in this region.
    var takenNames = (await _context.Cities
            .Where(c => c.RegionId == regionId)
            .Select(c => c.Name)
            .ToListAsync())
        .ToHashSet();

    var errors = new List<BulkRowErrorDto>();
    var toInsert = new List<City>();

    // A single pass: parse, validate, check duplicates, build.
    for (var i = 0; i < rows.Count; i++)
    {
      var rowNumber = i + 1;

      if (!BulkRowParser.TryParse<CityBulkItemDto>(rows[i], rowNumber, out var dto, out var error))
      {
        errors.Add(error);
        continue;
      }

      var name = dto.Name!.Trim(); // safe: [Required] already passed

      if (!takenNames.Add(name))
      {
        errors.Add(new BulkRowErrorDto
        {
          Row = rowNumber,
          Data = rows[i],
          Errors = [$"A city named '{name}' already exists in {region.Name}."],
        });
        continue;
      }

      toInsert.Add(new City
      {
        Name = name,
        PsgcCode = string.IsNullOrWhiteSpace(dto.PsgcCode) ? null : dto.PsgcCode.Trim(),
        RegionId = regionId, // assigned here, from the request, not from the row
      });
    }

    if (toInsert.Count > 0)
    {
      _context.Cities.AddRange(toInsert);
      await _context.SaveChangesAsync();
    }

    return new BulkInsertResultDto
    {
      Total = rows.Count,
      Inserted = toInsert.Count,
      Errors = errors,
    };
  }
}