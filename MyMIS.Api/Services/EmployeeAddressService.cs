using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;
using MyMIS.Api.Models;

namespace MyMIS.Api.Services;

public class EmployeeAddressService(AppDbContext context)
{
  private readonly AppDbContext _context = context;

  public async Task<List<EmployeeAddressResponseDto>?> GetAllAsync(int employeeId)
  {
    if (!await EmployeeExistsAsync(employeeId))
    {
      return null;
    }

    var addresses = await QueryAddresses(employeeId)
        .OrderByDescending(a => a.IsPrimary)
        .ThenBy(a => a.CreatedAt)
        .ThenBy(a => a.Id)
        .ToListAsync();

    return addresses.Select(MapToResponse).ToList();
  }

  public async Task<EmployeeAddressResponseDto?> GetByIdAsync(int employeeId, int addressId)
  {
    var address = await QueryAddresses(employeeId)
        .FirstOrDefaultAsync(a => a.Id == addressId);

    return address is null ? null : MapToResponse(address);
  }

  public async Task<ServiceResult<EmployeeAddressResponseDto>> CreateAsync(
      int employeeId,
      EmployeeAddressCreateDto dto)
  {
    if (!await EmployeeExistsAsync(employeeId))
    {
      return ServiceResult<EmployeeAddressResponseDto>.NotFound(
          $"Employee {employeeId} was not found.");
    }

    var error = await ValidateAsync(dto);
    if (error is not null)
    {
      return ServiceResult<EmployeeAddressResponseDto>.Invalid(error);
    }

    var hasAddresses = await _context.EmployeeAddresses
        .AnyAsync(a => a.EmployeeId == employeeId);
    var makePrimary = !hasAddresses || dto.IsPrimary;

    var address = NewEntity(dto, makePrimary);
    address.EmployeeId = employeeId;

    await using var transaction = await _context.Database.BeginTransactionAsync();

    if (makePrimary && hasAddresses)
    {
      await ClearPrimaryAsync(employeeId);
    }

    _context.EmployeeAddresses.Add(address);
    await _context.SaveChangesAsync();
    await transaction.CommitAsync();

    var response = await GetByIdAsync(employeeId, address.Id);
    return ServiceResult<EmployeeAddressResponseDto>.Success(response!);
  }

  public async Task<ServiceResult<EmployeeAddressResponseDto>> UpdateAsync(
      int employeeId,
      int addressId,
      EmployeeAddressCreateDto dto)
  {
    var address = await _context.EmployeeAddresses
        .FirstOrDefaultAsync(a => a.Id == addressId && a.EmployeeId == employeeId);

    if (address is null)
    {
      return ServiceResult<EmployeeAddressResponseDto>.NotFound(
          $"Address {addressId} was not found.");
    }

    if (address.IsPrimary && !dto.IsPrimary)
    {
      return ServiceResult<EmployeeAddressResponseDto>.Invalid(
          "This is the primary address. To change the primary address, mark another address as primary instead.");
    }

    var error = await ValidateAsync(dto);
    if (error is not null)
    {
      return ServiceResult<EmployeeAddressResponseDto>.Invalid(error);
    }

    var becomingPrimary = !address.IsPrimary && dto.IsPrimary;

    await using var transaction = await _context.Database.BeginTransactionAsync();

    if (becomingPrimary)
    {
      await ClearPrimaryAsync(employeeId);
      address.IsPrimary = true;
    }

    address.Type = dto.Type!.Value;
    address.Address = AddressMapper.ToEntity(dto.Address!);
    address.UpdatedAt = DateTime.UtcNow;

    await _context.SaveChangesAsync();
    await transaction.CommitAsync();

    var response = await GetByIdAsync(employeeId, address.Id);
    return ServiceResult<EmployeeAddressResponseDto>.Success(response!);
  }

  public async Task<ServiceResult<bool>> DeleteAsync(int employeeId, int addressId)
  {
    var address = await _context.EmployeeAddresses
        .FirstOrDefaultAsync(a => a.Id == addressId && a.EmployeeId == employeeId);

    if (address is null)
    {
      return ServiceResult<bool>.NotFound($"Address {addressId} was not found.");
    }

    var count = await _context.EmployeeAddresses
        .CountAsync(a => a.EmployeeId == employeeId);

    if (count == 1)
    {
      return ServiceResult<bool>.Invalid(
          "An employee must have at least one address. Add another address before removing this one.");
    }

    var wasPrimary = address.IsPrimary;
    var now = DateTime.UtcNow;

    await using var transaction = await _context.Database.BeginTransactionAsync();

    address.IsPrimary = false;
    address.DeletedAt = now;
    address.UpdatedAt = now;
    await _context.SaveChangesAsync();

    if (wasPrimary)
    {
      var next = await _context.EmployeeAddresses
          .Where(a => a.EmployeeId == employeeId)
          .OrderBy(a => a.CreatedAt)
          .ThenBy(a => a.Id)
          .FirstAsync();

      next.IsPrimary = true;
      next.UpdatedAt = now;
      await _context.SaveChangesAsync();
    }

    await transaction.CommitAsync();
    return ServiceResult<bool>.Success(true);
  }

  // Checks for a single address (POST/PUT on the address endpoints).
  public async Task<string?> ValidateAsync(EmployeeAddressCreateDto dto)
  {
    var barangayId = dto.Address!.BarangayId!.Value;
    var exists = await _context.Barangays.AnyAsync(b => b.Id == barangayId);
    return exists ? null : $"Barangay {barangayId} was not found.";
  }

  // Checks for the list sent with POST /api/Employees.
  public async Task<string?> ValidateForNewEmployeeAsync(IReadOnlyCollection<EmployeeAddressCreateDto> dtos)
  {
    if (dtos.Count(d => d.IsPrimary) > 1)
    {
      return "Only one address can be marked as primary.";
    }

    var barangayIds = dtos
        .Select(d => d.Address!.BarangayId!.Value)
        .Distinct()
        .ToList();

    var found = await _context.Barangays.CountAsync(b => barangayIds.Contains(b.Id));
    return found == barangayIds.Count ? null : "One or more barangays were not found.";
  }

  // Builds the unsaved addresses for a new employee. The one marked primary
  // wins; if none is marked, the first address becomes primary.
  public static List<EmployeeAddress> NewEntitiesForNewEmployee(IReadOnlyList<EmployeeAddressCreateDto> dtos)
  {
    var primaryIndex = dtos.ToList().FindIndex(d => d.IsPrimary);
    if (primaryIndex < 0)
    {
      primaryIndex = 0;
    }

    return dtos.Select((dto, index) => NewEntity(dto, index == primaryIndex)).ToList();
  }

  private static EmployeeAddress NewEntity(EmployeeAddressCreateDto dto, bool isPrimary)
  {
    var now = DateTime.UtcNow;
    return new EmployeeAddress
    {
      Type = dto.Type!.Value,
      Address = AddressMapper.ToEntity(dto.Address!),
      IsPrimary = isPrimary,
      CreatedAt = now,
      UpdatedAt = now,
    };
  }

  private Task<bool> EmployeeExistsAsync(int employeeId) =>
      _context.Employees.AnyAsync(e => e.Id == employeeId);

  private IQueryable<EmployeeAddress> QueryAddresses(int employeeId) =>
      _context.EmployeeAddresses
          .AsNoTracking()
          .Include(a => a.Address.Barangay)
              .ThenInclude(b => b.City)
              .ThenInclude(city => city.Region)
          .Where(a => a.EmployeeId == employeeId);

  private async Task ClearPrimaryAsync(int employeeId)
  {
    var current = await _context.EmployeeAddresses
        .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.IsPrimary);

    if (current is null)
    {
      return;
    }

    current.IsPrimary = false;
    current.UpdatedAt = DateTime.UtcNow;
    await _context.SaveChangesAsync();
  }

  private static EmployeeAddressResponseDto MapToResponse(EmployeeAddress a) => new()
  {
    Id = a.Id,
    Type = a.Type,
    Address = AddressMapper.ToResponse(a.Address),
    IsPrimary = a.IsPrimary,
  };
}