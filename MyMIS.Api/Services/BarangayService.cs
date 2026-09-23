using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;

namespace MyMIS.Api.Services;

public class BarangayService(AppDbContext context)
{
  private readonly AppDbContext _context = context;

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
}