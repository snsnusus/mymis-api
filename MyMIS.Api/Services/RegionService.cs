using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;
using MyMIS.Api.Models;
using System.Text.Json;

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

  public async Task<BulkInsertResultDto> BulkCreateAsync(IReadOnlyList<JsonElement> rows)
  {
    // One query: every existing region name. There are only 17 regions,
    // so loading them all is cheap.
    var takenNames = (await _context.Regions
            .Select(r => r.Name)
            .ToListAsync())
        .ToHashSet();

    var errors = new List<BulkRowErrorDto>();
    var toInsert = new List<Region>();

    for (var i = 0; i < rows.Count; i++)
    {
      var rowNumber = i + 1;

      if (!BulkRowParser.TryParse<RegionBulkItemDto>(rows[i], rowNumber, out var dto, out var error))
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
          Errors = [$"A region named '{name}' already exists."],
        });
        continue;
      }

      toInsert.Add(new Region
      {
        Name = name,
        PsgcCode = string.IsNullOrWhiteSpace(dto.PsgcCode) ? null : dto.PsgcCode.Trim(),
      });
    }

    if (toInsert.Count > 0)
    {
      _context.Regions.AddRange(toInsert);
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