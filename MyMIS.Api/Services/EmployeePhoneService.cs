using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;
using MyMIS.Api.Models;

namespace MyMIS.Api.Services;

public class EmployeePhoneService(AppDbContext context)
{
  private readonly AppDbContext _context = context;

  public async Task<List<EmployeePhoneResponseDto>?> GetAllAsync(int employeeId)
  {
    if (!await EmployeeExistsAsync(employeeId))
    {
      return null;
    }

    var phones = await QueryPhones(employeeId)
      .OrderByDescending(ep => ep.IsPrimary)
      .ThenBy(ep => ep.CreatedAt)
      .ThenBy(ep => ep.Id)
      .ToListAsync();

    return [.. phones.Select(MapToResponse)];
  }

  public async Task<EmployeePhoneResponseDto?> GetByIdAsync(int employeeId, int phoneId)
  {
    var phone = await QueryPhones(employeeId)
        .FirstOrDefaultAsync(ep => ep.Id == phoneId);

    return phone is null ? null : MapToResponse(phone);
  }

  public async Task<ServiceResult<EmployeePhoneResponseDto>> CreateAsync(int employeeId, EmployeePhoneCreateDto dto)
  {
    if (!await EmployeeExistsAsync(employeeId))
    {
      return ServiceResult<EmployeePhoneResponseDto>.NotFound(
          $"Employee {employeeId} was not found.");
    }

    var normalized = PhoneFormatter.TryNormalize(dto.Phone!);

    if (normalized is null)
    {
      return ServiceResult<EmployeePhoneResponseDto>.Invalid("Phone number is not valid for the selected country.");
    }

    var lineType = PhoneFormatter.DetectLineType(normalized);

    if (lineType == PhoneLineType.Mobile)
    {
      var taken = await IsMobileNumberTakenAsync(normalized.Number);
      if (taken)
      {
        return ServiceResult<EmployeePhoneResponseDto>.Conflict("This mobile number is already in use.");
      }
    }

    var hasPhones = await _context.EmployeePhones
        .AnyAsync(ep => ep.EmployeeId == employeeId);
    var makePrimary = !hasPhones || dto.IsPrimary;

    var phone = NewEntity(dto.Ownership!.Value, normalized, lineType, makePrimary);
    phone.EmployeeId = employeeId;

    await using var transaction = await _context.Database.BeginTransactionAsync();

    if (makePrimary && hasPhones)
    {
      await ClearPrimaryAsync(employeeId);
    }

    _context.EmployeePhones.Add(phone);
    await _context.SaveChangesAsync();
    await transaction.CommitAsync();

    var response = await GetByIdAsync(employeeId, phone.Id);
    return ServiceResult<EmployeePhoneResponseDto>.Success(response!);
  }

  public async Task<ServiceResult<EmployeePhoneResponseDto>> UpdateAsync(int employeeId, int phoneId, EmployeePhoneCreateDto dto)
  {
    var phone = await _context.EmployeePhones
        .FirstOrDefaultAsync(ep => ep.Id == phoneId && ep.EmployeeId == employeeId);

    if (phone is null)
    {
      return ServiceResult<EmployeePhoneResponseDto>.NotFound(
          $"Phone {phoneId} was not found.");
    }

    if (phone.IsPrimary && !dto.IsPrimary)
    {
      return ServiceResult<EmployeePhoneResponseDto>.Invalid(
          "This is the primary phone number. To change the primary phone number, mark another as primary instead.");
    }

    var normalized = PhoneFormatter.TryNormalize(dto.Phone!);

    if (normalized is null)
    {
      return ServiceResult<EmployeePhoneResponseDto>.Invalid("Phone number is not valid for the selected country.");
    }

    var lineType = PhoneFormatter.DetectLineType(normalized);

    if (lineType == PhoneLineType.Mobile)
    {
      var taken = await IsMobileNumberTakenAsync(normalized.Number, phoneId);
      if (taken)
      {
        return ServiceResult<EmployeePhoneResponseDto>.Conflict("This mobile number is already in use.");
      }
    }

    var becomingPrimary = !phone.IsPrimary && dto.IsPrimary;

    await using var transaction = await _context.Database.BeginTransactionAsync();

    if (becomingPrimary)
    {
      await ClearPrimaryAsync(employeeId);
      phone.IsPrimary = true;
    }

    phone.Ownership = dto.Ownership!.Value;
    phone.LineType = lineType;
    phone.Phone = normalized;
    phone.UpdatedAt = DateTime.UtcNow;

    await _context.SaveChangesAsync();
    await transaction.CommitAsync();

    var response = await GetByIdAsync(employeeId, phone.Id);
    return ServiceResult<EmployeePhoneResponseDto>.Success(response!);
  }

  public async Task<ServiceResult<bool>> DeleteAsync(int employeeId, int phoneId)
  {
    var phone = await _context.EmployeePhones
        .FirstOrDefaultAsync(ep => ep.Id == phoneId && ep.EmployeeId == employeeId);

    if (phone is null)
    {
      return ServiceResult<bool>.NotFound($"Phone {phoneId} was not found.");
    }

    var count = await _context.EmployeePhones
        .CountAsync(ep => ep.EmployeeId == employeeId);

    if (count == 1)
    {
      return ServiceResult<bool>.Invalid(
          "An employee must have at least one phone number. Add another phone number before removing this one.");
    }

    var wasPrimary = phone.IsPrimary;
    var now = DateTime.UtcNow;

    await using var transaction = await _context.Database.BeginTransactionAsync();

    phone.IsPrimary = false;
    phone.DeletedAt = now;
    phone.UpdatedAt = now;
    await _context.SaveChangesAsync();

    if (wasPrimary)
    {
      var next = await _context.EmployeePhones
          .Where(ep => ep.EmployeeId == employeeId)
          .OrderBy(ep => ep.CreatedAt)
          .ThenBy(ep => ep.Id)
          .FirstAsync();

      next.IsPrimary = true;
      next.UpdatedAt = now;
      await _context.SaveChangesAsync();
    }

    await transaction.CommitAsync();
    return ServiceResult<bool>.Success(true);
  }

  private Task<bool> EmployeeExistsAsync(int employeeId) =>
    _context.Employees.AnyAsync(e => e.Id == employeeId);

  private IQueryable<EmployeePhone> QueryPhones(int employeeId) =>
    _context.EmployeePhones.AsNoTracking()
      .Where(ep => ep.EmployeeId == employeeId);

  public static EmployeePhone NewEntity(ContactOwnership ownership, Phone phone, PhoneLineType type, bool isPrimary)
  {
    var now = DateTime.UtcNow;
    var employeePhone = new EmployeePhone
    {
      Ownership = ownership,
      Phone = phone,
      LineType = type,
      IsPrimary = isPrimary,
      CreatedAt = now,
      UpdatedAt = now,
    };
    return employeePhone;
  }

  private static EmployeePhoneResponseDto MapToResponse(EmployeePhone ep) => new()
  {
    Id = ep.Id,
    Ownership = ep.Ownership,
    LineType = ep.LineType,
    Phone = PhoneFormatter.ToResponse(ep.Phone),
    IsPrimary = ep.IsPrimary,
  };

  private async Task ClearPrimaryAsync(int employeeId)
  {
    var current = await _context.EmployeePhones
        .FirstOrDefaultAsync(ep => ep.EmployeeId == employeeId && ep.IsPrimary);

    if (current is null)
    {
      return;
    }

    current.IsPrimary = false;
    current.UpdatedAt = DateTime.UtcNow;
    await _context.SaveChangesAsync();
  }

  public async Task<bool> IsMobileNumberTakenAsync(string number, int? excludePhoneId = null)
  {
    var query = _context.EmployeePhones
      .Where(ep => ep.LineType == PhoneLineType.Mobile)
      .Where(ep => ep.Phone.Number == number);

    if (excludePhoneId is int idToExclude)
    {
      query = query.Where(ep => ep.Id != idToExclude);
    }

    return await query.AnyAsync();
  }

  public async Task<ServiceResult<bool>> ValidateForNewEmployeeAsync(IReadOnlyCollection<EmployeePhoneCreateDto> dtos)
  {
    if (dtos.Count(d => d.IsPrimary) > 1)
    {
      return ServiceResult<bool>.Invalid("Only one phone number can be marked as primary.");
    }

    HashSet<string> mobileNumbers = [];
    var position = 0;
    foreach (var dto in dtos)
    {
      position++;
      var normalized = PhoneFormatter.TryNormalize(dto.Phone!);

      if (normalized is null)
      {
        return ServiceResult<bool>.Invalid($"Phone {position} is not valid.");
      }

      var lineType = PhoneFormatter.DetectLineType(normalized);

      if (lineType == PhoneLineType.Mobile)
      {
        var isAdded = mobileNumbers.Add(normalized.Number);

        if (!isAdded)
        {
          return ServiceResult<bool>.Invalid($"Phone {position} repeats a mobile number entered above.");
        }
      }
    }

    var found = await _context.EmployeePhones.AnyAsync(ep =>
      mobileNumbers.Contains(ep.Phone.Number) && ep.LineType == PhoneLineType.Mobile);
    if (found)
    {
      return ServiceResult<bool>.Conflict("One or more of these mobile numbers is already in use.");
    }

    return ServiceResult<bool>.Success(true);
  }

  public static List<EmployeePhone> NewEntitiesForNewEmployee(IReadOnlyList<EmployeePhoneCreateDto> dtos)
  {
    var primaryIndex = dtos.ToList().FindIndex(d => d.IsPrimary);
    if (primaryIndex < 0)
    {
      primaryIndex = 0;
    }

    return [.. dtos.Select((dto, index) => {
      var normalized = PhoneFormatter.TryNormalize(dto.Phone!)
        ?? throw new InvalidOperationException("Phones must be validated with ValidateForNewEmployeeAsync before calling NewEntitiesForNewEmployee.");
      var lineType = PhoneFormatter.DetectLineType(normalized);

      return NewEntity(dto.Ownership!.Value, normalized, lineType, index == primaryIndex);
    })];
  }
}