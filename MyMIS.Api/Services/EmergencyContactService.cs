using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;
using MyMIS.Api.Models;

namespace MyMIS.Api.Services;

public class EmergencyContactService(AppDbContext context)
{
  private readonly AppDbContext _context = context;

  public async Task<List<EmergencyContactResponseDto>?> GetAllAsync(int employeeId)
  {
    if (!await EmployeeExistsAsync(employeeId))
    {
      return null;
    }

    var contacts = await QueryContacts(employeeId)
        .OrderByDescending(c => c.IsPrimary)
        .ThenBy(c => c.CreatedAt)
        .ThenBy(c => c.Id)
        .ToListAsync();

    return contacts.Select(MapToResponse).ToList();
  }

  public async Task<EmergencyContactResponseDto?> GetByIdAsync(int employeeId, int contactId)
  {
    var contact = await QueryContacts(employeeId)
        .FirstOrDefaultAsync(c => c.Id == contactId);

    return contact is null ? null : MapToResponse(contact);
  }

  public async Task<ServiceResult<EmergencyContactResponseDto>> CreateAsync(
      int employeeId,
      EmergencyContactCreateDto dto)
  {
    if (!await EmployeeExistsAsync(employeeId))
    {
      return ServiceResult<EmergencyContactResponseDto>.NotFound(
          $"Employee {employeeId} was not found.");
    }

    var (phone, error) = await ValidateAsync(dto);
    if (error is not null)
    {
      return ServiceResult<EmergencyContactResponseDto>.Invalid(error);
    }

    var hasContacts = await _context.EmergencyContacts
        .AnyAsync(c => c.EmployeeId == employeeId);
    var makePrimary = !hasContacts || dto.IsPrimary;

    var contact = NewEntity(dto, phone!, makePrimary);
    contact.EmployeeId = employeeId;

    await using var transaction = await _context.Database.BeginTransactionAsync();

    if (makePrimary && hasContacts)
    {
      await ClearPrimaryAsync(employeeId);
    }

    _context.EmergencyContacts.Add(contact);
    await _context.SaveChangesAsync();
    await transaction.CommitAsync();

    var response = await GetByIdAsync(employeeId, contact.Id);
    return ServiceResult<EmergencyContactResponseDto>.Success(response!);
  }

  public async Task<ServiceResult<EmergencyContactResponseDto>> UpdateAsync(
      int employeeId,
      int contactId,
      EmergencyContactCreateDto dto)
  {
    var contact = await _context.EmergencyContacts
        .FirstOrDefaultAsync(c => c.Id == contactId && c.EmployeeId == employeeId);

    if (contact is null)
    {
      return ServiceResult<EmergencyContactResponseDto>.NotFound(
          $"Emergency contact {contactId} was not found.");
    }

    if (contact.IsPrimary && !dto.IsPrimary)
    {
      return ServiceResult<EmergencyContactResponseDto>.Invalid(
          "This is the primary emergency contact. To change the primary contact, mark another contact as primary instead.");
    }

    var (phone, error) = await ValidateAsync(dto);
    if (error is not null)
    {
      return ServiceResult<EmergencyContactResponseDto>.Invalid(error);
    }

    var becomingPrimary = !contact.IsPrimary && dto.IsPrimary;

    await using var transaction = await _context.Database.BeginTransactionAsync();

    if (becomingPrimary)
    {
      await ClearPrimaryAsync(employeeId);
      contact.IsPrimary = true;
    }

    ApplyDto(contact, dto, phone!);
    contact.UpdatedAt = DateTime.UtcNow;

    await _context.SaveChangesAsync();
    await transaction.CommitAsync();

    var response = await GetByIdAsync(employeeId, contact.Id);
    return ServiceResult<EmergencyContactResponseDto>.Success(response!);
  }

  public async Task<bool> DeleteAsync(int employeeId, int contactId)
  {
    var contact = await _context.EmergencyContacts
        .FirstOrDefaultAsync(c => c.Id == contactId && c.EmployeeId == employeeId);

    if (contact is null)
    {
      return false;
    }

    var wasPrimary = contact.IsPrimary;
    var now = DateTime.UtcNow;

    await using var transaction = await _context.Database.BeginTransactionAsync();

    contact.IsPrimary = false;
    contact.DeletedAt = now;
    contact.UpdatedAt = now;
    await _context.SaveChangesAsync();

    if (wasPrimary)
    {
      var next = await _context.EmergencyContacts
          .Where(c => c.EmployeeId == employeeId)
          .OrderBy(c => c.CreatedAt)
          .ThenBy(c => c.Id)
          .FirstOrDefaultAsync();

      if (next is not null)
      {
        next.IsPrimary = true;
        next.UpdatedAt = now;
        await _context.SaveChangesAsync();
      }
    }

    await transaction.CommitAsync();
    return true;
  }

  private Task<bool> EmployeeExistsAsync(int employeeId) =>
      _context.Employees.AnyAsync(e => e.Id == employeeId);

  private IQueryable<EmergencyContact> QueryContacts(int employeeId) =>
      _context.EmergencyContacts
          .AsNoTracking()
          .Include(c => c.Address!.Barangay)
              .ThenInclude(b => b.City)
              .ThenInclude(city => city.Region)
          .Where(c => c.EmployeeId == employeeId);

  private async Task ClearPrimaryAsync(int employeeId)
  {
    var current = await _context.EmergencyContacts
        .FirstOrDefaultAsync(c => c.EmployeeId == employeeId && c.IsPrimary);

    if (current is null)
    {
      return;
    }

    current.IsPrimary = false;
    current.UpdatedAt = DateTime.UtcNow;
    await _context.SaveChangesAsync();
  }

  public async Task<(Phone? Phone, string? Error)> ValidateAsync(EmergencyContactCreateDto dto)
  {
    var phone = PhoneFormatter.TryNormalize(dto.Phone!);
    if (phone is null)
    {
      return (null, "Phone number is not valid for the selected country.");
    }

    if (dto.Address is not null)
    {
      var barangayExists = await _context.Barangays
          .AnyAsync(b => b.Id == dto.Address.BarangayId);

      if (!barangayExists)
      {
        return (null, $"Barangay {dto.Address.BarangayId} was not found.");
      }
    }

    return (phone, null);
  }

  private static void ApplyDto(EmergencyContact contact, EmergencyContactCreateDto dto, Phone phone)
  {
    contact.FirstName = dto.FirstName.Trim();
    contact.MiddleName = NullIfBlank(dto.MiddleName);
    contact.LastName = dto.LastName.Trim();
    contact.Suffix = NullIfBlank(dto.Suffix);
    contact.Relationship = dto.Relationship!.Value;
    contact.Phone = phone;
    contact.Address = dto.Address is null ? null : AddressMapper.ToEntity(dto.Address);
  }

  // Builds a new, unsaved contact. Used by CreateAsync here and by
  // EmployeeService.CreateAsync, which saves it together with a new employee.
  public static EmergencyContact NewEntity(EmergencyContactCreateDto dto, Phone phone, bool isPrimary)
  {
    var now = DateTime.UtcNow;
    var contact = new EmergencyContact
    {
      IsPrimary = isPrimary,
      CreatedAt = now,
      UpdatedAt = now,
    };
    ApplyDto(contact, dto, phone);
    return contact;
  }

  private static string? NullIfBlank(string? value) =>
      string.IsNullOrWhiteSpace(value) ? null : value.Trim();

  public static EmergencyContactResponseDto MapToResponse(EmergencyContact c) => new()
  {
    Id = c.Id,
    FirstName = c.FirstName,
    MiddleName = c.MiddleName,
    LastName = c.LastName,
    Suffix = c.Suffix,
    Relationship = c.Relationship,
    Phone = PhoneFormatter.ToResponse(c.Phone),
    Address = c.Address is null ? null : AddressMapper.ToResponse(c.Address),
    IsPrimary = c.IsPrimary,
  };
}