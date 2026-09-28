using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;
using MyMIS.Api.Models;
using System.Text.Json;

namespace MyMIS.Api.Services;

public class BarangayService(AppDbContext context)
{
  private readonly AppDbContext _context = context;

  // Trims a value and turns empty or whitespace-only strings into null.
  private static string? NullIfBlank(string? value) =>
      string.IsNullOrWhiteSpace(value) ? null : value.Trim();

  public async Task<List<BarangayResponseDto>> GetAllAsync(int? cityId)
  {
    var query = _context.Barangays
        .Include(b => b.City)
        .AsQueryable();

    if (cityId.HasValue)
    {
      query = query.Where(b => b.CityId == cityId.Value);
    }

    return await query
        .Select(b => new BarangayResponseDto
        {
          Id = b.Id,
          Name = b.Name,
          PsgcCode = b.PsgcCode,
          ZipCode = b.ZipCode,
          CityId = b.CityId,
          CityName = b.City.Name
        })
        .ToListAsync();
  }

  public async Task<BarangayResponseDto?> GetByIdAsync(int id)
  {
    return await _context.Barangays
        .Include(b => b.City)
        .Where(b => b.Id == id)
        .Select(b => new BarangayResponseDto
        {
          Id = b.Id,
          Name = b.Name,
          PsgcCode = b.PsgcCode,
          ZipCode = b.ZipCode,
          CityId = b.CityId,
          CityName = b.City.Name
        })
        .FirstOrDefaultAsync();
  }

  public async Task<BarangayResponseDto> CreateAsync(BarangayCreateDto dto)
  {
    var barangay = new Barangay
    {
      Name = dto.Name,
      PsgcCode = dto.PsgcCode,
      ZipCode = dto.ZipCode,
      CityId = dto.CityId
    };
    _context.Barangays.Add(barangay);
    await _context.SaveChangesAsync();

    return await GetByIdAsync(barangay.Id)
        ?? throw new InvalidOperationException("Failed to reload newly created barangay.");
  }

  public async Task<BarangayResponseDto?> UpdateAsync(int id, BarangayCreateDto dto)
  {
    var barangay = await _context.Barangays.FindAsync(id);
    if (barangay is null) return null;

    barangay.Name = dto.Name;
    barangay.PsgcCode = dto.PsgcCode;
    barangay.ZipCode = dto.ZipCode;
    barangay.CityId = dto.CityId;

    await _context.SaveChangesAsync();
    return await GetByIdAsync(barangay.Id);
  }

  public async Task<bool> DeleteAsync(int id)
  {
    var barangay = await _context.Barangays.FindAsync(id);
    if (barangay is null) return false;

    _context.Barangays.Remove(barangay);
    await _context.SaveChangesAsync();
    return true;
  }

  public async Task<BulkInsertResultDto?> BulkCreateAsync(int cityId, IReadOnlyList<JsonElement> rows)
  {
    // Request-level check, once, before touching any row.
    var city = await _context.Cities.FindAsync(cityId);
    if (city is null)
    {
      return null;
    }

    // One query: every barangay name already used in this city.
    var takenNames = (await _context.Barangays
            .Where(b => b.CityId == cityId)
            .Select(b => b.Name)
            .ToListAsync())
        .ToHashSet();

    var errors = new List<BulkRowErrorDto>();
    var toInsert = new List<Barangay>();

    for (var i = 0; i < rows.Count; i++)
    {
      var rowNumber = i + 1;

      if (!BulkRowParser.TryParse<BarangayBulkItemDto>(rows[i], rowNumber, out var dto, out var error))
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
          Errors = [$"A barangay named '{name}' already exists in {city.Name}."],
        });
        continue;
      }

      toInsert.Add(new Barangay
      {
        Name = name,
        PsgcCode = NullIfBlank(dto.PsgcCode),
        ZipCode = NullIfBlank(dto.ZipCode),
        CityId = cityId, // assigned here, from the request, not from the row
      });
    }

    if (toInsert.Count > 0)
    {
      _context.Barangays.AddRange(toInsert);
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