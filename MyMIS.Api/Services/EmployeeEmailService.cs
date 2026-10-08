using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;
using MyMIS.Api.Models;

namespace MyMIS.Api.Services;

public class EmployeeEmailService(AppDbContext context)
{
  private readonly AppDbContext _context = context;

  private Task<bool> EmployeeExistsAsync(int employeeId) =>
    _context.Employees.AnyAsync(e => e.Id == employeeId);

  private IQueryable<EmployeeEmail> QueryEmails(int employeeId) =>
    _context.EmployeeEmails.AsNoTracking()
      .Where(ee => ee.EmployeeId == employeeId);

  private static EmployeeEmailResponseDto MapToResponse(EmployeeEmail ee) => new()
  {
    Id = ee.Id,
    Ownership = ee.Ownership,
    Email = ee.Email,
    IsPrimary = ee.IsPrimary,
  };

  private async Task ClearPrimaryAsync(int employeeId)
  {
    var current = await _context.EmployeeEmails
      .FirstOrDefaultAsync(ee => ee.EmployeeId == employeeId && ee.IsPrimary);

    if (current is null)
    {
      return;
    }

    current.IsPrimary = false;
    current.UpdatedAt = DateTime.UtcNow;
    await _context.SaveChangesAsync();
  }

  public static EmployeeEmail NewEntity(ContactOwnership ownership, string email, bool isPrimary)
  {
    var now = DateTime.UtcNow;
    return new EmployeeEmail
    {
      Ownership = ownership,
      Email = email,
      IsPrimary = isPrimary,
      CreatedAt = now,
      UpdatedAt = now,
    };
  }

  public async Task<bool> IsEmailTakenAsync(string email, int? excludeEmailId = null)
  {
    var query = _context.EmployeeEmails
      .Where(ee => ee.Email == email);

    if (excludeEmailId is int idToExclude)
    {
      query = query.Where(ee => ee.Id != idToExclude);
    }

    return await query.AnyAsync();
  }

  public async Task<List<EmployeeEmailResponseDto>?> GetAllAsync(int employeeId)
  {
    if (!await EmployeeExistsAsync(employeeId))
    {
      return null;
    }

    var emails = await QueryEmails(employeeId)
      .OrderByDescending(ee => ee.IsPrimary)
      .ThenBy(ee => ee.CreatedAt)
      .ThenBy(ee => ee.Id)
      .ToListAsync();

    return [.. emails.Select(MapToResponse)];
  }

  public async Task<EmployeeEmailResponseDto?> GetByIdAsync(int employeeId, int emailId)
  {
    var email = await QueryEmails(employeeId)
      .FirstOrDefaultAsync(ee => ee.Id == emailId);

    return email is null ? null : MapToResponse(email);
  }

  public async Task<ServiceResult<EmployeeEmailResponseDto>> CreateAsync(int employeeId, EmployeeEmailCreateDto dto)
  {
    if (!await EmployeeExistsAsync(employeeId))
    {
      return ServiceResult<EmployeeEmailResponseDto>.NotFound(
        $"Employee {employeeId} was not found.");
    }

    var normalized = EmailRules.Normalize(dto.Email);

    if (!EmailRules.IsValid(normalized))
    {
      return ServiceResult<EmployeeEmailResponseDto>.Invalid("Email address is not valid.");
    }

    if (await IsEmailTakenAsync(normalized))
    {
      return ServiceResult<EmployeeEmailResponseDto>.Conflict("This email address is already in use.");
    }

    var hasEmails = await _context.EmployeeEmails
        .AnyAsync(ee => ee.EmployeeId == employeeId);
    var makePrimary = !hasEmails || dto.IsPrimary;

    var entity = NewEntity(dto.Ownership!.Value, normalized, makePrimary);
    entity.EmployeeId = employeeId;

    await using var transaction = await _context.Database.BeginTransactionAsync();

    if (makePrimary && hasEmails)
    {
      await ClearPrimaryAsync(employeeId);
    }

    _context.EmployeeEmails.Add(entity);
    await _context.SaveChangesAsync();
    await transaction.CommitAsync();

    return ServiceResult<EmployeeEmailResponseDto>.Success(MapToResponse(entity));
  }

  public async Task<ServiceResult<EmployeeEmailResponseDto>> UpdateAsync(int employeeId, int emailId, EmployeeEmailCreateDto dto)
  {
    var email = await _context.EmployeeEmails
      .FirstOrDefaultAsync(ee => ee.Id == emailId && ee.EmployeeId == employeeId);

    if (email is null)
    {
      return ServiceResult<EmployeeEmailResponseDto>.NotFound(
          $"Email {emailId} was not found.");
    }

    if (email.IsPrimary && !dto.IsPrimary)
    {
      return ServiceResult<EmployeeEmailResponseDto>.Invalid(
          "This is the primary email. To change the primary email, mark another as primary instead.");
    }

    var normalized = EmailRules.Normalize(dto.Email);

    if (!EmailRules.IsValid(normalized))
    {
      return ServiceResult<EmployeeEmailResponseDto>.Invalid("Email address is not valid.");
    }

    if (await IsEmailTakenAsync(normalized, emailId))
    {
      return ServiceResult<EmployeeEmailResponseDto>.Conflict("This email address is already in use.");
    }

    var becomingPrimary = !email.IsPrimary && dto.IsPrimary;

    await using var transaction = await _context.Database.BeginTransactionAsync();

    if (becomingPrimary)
    {
      await ClearPrimaryAsync(employeeId);
      email.IsPrimary = true;
    }

    email.Ownership = dto.Ownership!.Value;
    email.Email = normalized;
    email.UpdatedAt = DateTime.UtcNow;

    await _context.SaveChangesAsync();
    await transaction.CommitAsync();

    return ServiceResult<EmployeeEmailResponseDto>.Success(MapToResponse(email));
  }

  public async Task<ServiceResult<bool>> DeleteAsync(int employeeId, int emailId)
  {
    var email = await _context.EmployeeEmails
        .FirstOrDefaultAsync(ee => ee.Id == emailId && ee.EmployeeId == employeeId);

    if (email is null)
    {
      return ServiceResult<bool>.NotFound($"Email {emailId} was not found.");
    }

    var count = await _context.EmployeeEmails
        .CountAsync(ee => ee.EmployeeId == employeeId);

    if (count == 1)
    {
      return ServiceResult<bool>.Invalid(
          "An employee must have at least one email. Add another email before removing this one.");
    }

    var wasPrimary = email.IsPrimary;
    var now = DateTime.UtcNow;

    await using var transaction = await _context.Database.BeginTransactionAsync();

    email.IsPrimary = false;
    email.DeletedAt = now;
    email.UpdatedAt = now;
    await _context.SaveChangesAsync();

    if (wasPrimary)
    {
      var next = await _context.EmployeeEmails
          .Where(ee => ee.EmployeeId == employeeId)
          .OrderBy(ee => ee.CreatedAt)
          .ThenBy(ee => ee.Id)
          .FirstAsync();

      next.IsPrimary = true;
      next.UpdatedAt = now;
      await _context.SaveChangesAsync();
    }

    await transaction.CommitAsync();
    return ServiceResult<bool>.Success(true);
  }

  public async Task<ServiceResult<bool>> ValidateForNewEmployeeAsync(IReadOnlyCollection<EmployeeEmailCreateDto> dtos)
  {
    if (dtos.Count(d => d.IsPrimary) > 1)
    {
      return ServiceResult<bool>.Invalid("Only one email can be marked as primary.");
    }

    HashSet<string> emails = [];
    var position = 0;
    foreach (var dto in dtos)
    {
      position++;
      var normalized = EmailRules.Normalize(dto.Email);

      if (!EmailRules.IsValid(normalized))
      {
        return ServiceResult<bool>.Invalid($"Email address {position} is not valid.");
      }

      if (!emails.Add(normalized))
      {
        return ServiceResult<bool>.Invalid($"Email address {position} repeats an email entered above.");
      }
    }

    var found = await _context.EmployeeEmails.AnyAsync(ee =>
      emails.Contains(ee.Email));

    if (found)
    {
      return ServiceResult<bool>.Conflict("One or more of these email addresses is already in use.");
    }

    return ServiceResult<bool>.Success(true);
  }

  public static List<EmployeeEmail> NewEntitiesForNewEmployee(IReadOnlyList<EmployeeEmailCreateDto> dtos)
  {
    var primaryIndex = dtos.ToList().FindIndex(d => d.IsPrimary);
    if (primaryIndex < 0)
    {
      primaryIndex = 0;
    }

    return [.. dtos.Select((dto, index) => {
      var normalized = EmailRules.Normalize(dto.Email);

      if (!EmailRules.IsValid(normalized))
      {
        throw new InvalidOperationException(
          "Emails must be validated with ValidateForNewEmployeeAsync before calling NewEntitiesForNewEmployee.");
      }

      return NewEntity(dto.Ownership!.Value, normalized, index == primaryIndex);
    })];
  }
}