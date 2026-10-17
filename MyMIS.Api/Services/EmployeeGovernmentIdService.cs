using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;
using MyMIS.Api.Models;
using MyMIS.Api.Validation;

namespace MyMIS.Api.Services;

public class EmployeeGovernmentIdService(AppDbContext context)
{
  private readonly AppDbContext _context = context;

  private Task<bool> EmployeeExistsAsync(int employeeId) =>
    _context.Employees.AnyAsync(e => e.Id == employeeId);

  // The active row as a DTO, or null when nothing has been recorded.
  // Static so EmployeeService can reuse it without depending on this service.
  public static Task<EmployeeGovernmentIdResponseDto?> FindActiveAsync(AppDbContext context, int employeeId) =>
    context.EmployeeGovernmentIds
      .IgnoreQueryFilters()
      .Where(g => g.EmployeeId == employeeId && g.DeletedAt == null)
      .Select(g => new EmployeeGovernmentIdResponseDto
      {
        SssNumber = g.SssNumber,
        PhilHealthNumber = g.PhilHealthNumber,
        PagIbigNumber = g.PagIbigNumber,
        TinNumber = g.TinNumber,
        UpdatedAt = g.UpdatedAt,
        UpdatedByName = g.UpdatedBy.FirstName + " " + g.UpdatedBy.LastName,
      })
      .FirstOrDefaultAsync();

  // Normalizes all four numbers. Returns null if any provided value is invalid.
  private static (string? Sss, string? PhilHealth, string? PagIbig, string? Tin)? NormalizeAll(
    EmployeeGovernmentIdUpdateDto dto)
  {
    var sss = GovernmentIdRules.Normalize(dto.SssNumber);
    var philHealth = GovernmentIdRules.Normalize(dto.PhilHealthNumber);
    var pagIbig = GovernmentIdRules.Normalize(dto.PagIbigNumber);
    var tin = GovernmentIdRules.Normalize(dto.TinNumber);

    var hasInvalid =
      (sss is not null && !GovernmentIdRules.IsValidSss(sss)) ||
      (philHealth is not null && !GovernmentIdRules.IsValidPhilHealth(philHealth)) ||
      (pagIbig is not null && !GovernmentIdRules.IsValidPagIbig(pagIbig)) ||
      (tin is not null && !GovernmentIdRules.IsValidTin(tin));

    if (hasInvalid)
    {
      return null;
    }

    return (sss, philHealth, pagIbig, tin is null ? null : GovernmentIdRules.ToStoredTin(tin));
  }

  public static EmployeeGovernmentId? NewEntityForNewEmployee(EmployeeGovernmentIdUpdateDto dto, int createdById)
  {
    var (sss, philHealth, pagIbig, tin) = NormalizeAll(dto)
     ?? throw new InvalidOperationException(
       "Government IDs must be validated before CreateAsync is called.");

    var nothingToSave = sss is null && philHealth is null && pagIbig is null && tin is null;
    if (nothingToSave)
    {
      return null;
    }

    var now = DateTime.UtcNow;
    return new EmployeeGovernmentId
    {
      SssNumber = sss,
      PhilHealthNumber = philHealth,
      PagIbigNumber = pagIbig,
      TinNumber = tin,
      CreatedAt = now,
      UpdatedAt = now,
      UpdatedById = createdById,
    };
  }

  public async Task<EmployeeGovernmentIdResponseDto?> GetAsync(int employeeId)
  {
    if (!await EmployeeExistsAsync(employeeId))
    {
      return null;
    }

    return await FindActiveAsync(_context, employeeId) ?? new EmployeeGovernmentIdResponseDto();
  }

  public async Task<ServiceResult<EmployeeGovernmentIdResponseDto>> UpdateAsync(
    int employeeId, int updatedById, EmployeeGovernmentIdUpdateDto dto)
  {
    if (!await EmployeeExistsAsync(employeeId))
    {
      return ServiceResult<EmployeeGovernmentIdResponseDto>.NotFound($"Employee {employeeId} was not found.");
    }

    var normalized = NormalizeAll(dto);
    if (normalized is null)
    {
      return ServiceResult<EmployeeGovernmentIdResponseDto>.Invalid(
        "One or more government ID numbers are not valid.");
    }

    var (sss, philHealth, pagIbig, tin) = normalized.Value;

    // Safety net for callers that skip HTTP validation (e.g. the future change-request approval).
    var hasInvalid =
        (sss is not null && !GovernmentIdRules.IsValidSss(sss)) ||
        (philHealth is not null && !GovernmentIdRules.IsValidPhilHealth(philHealth)) ||
        (pagIbig is not null && !GovernmentIdRules.IsValidPagIbig(pagIbig)) ||
        (tin is not null && !GovernmentIdRules.IsValidTin(tin));

    if (hasInvalid)
    {
      return ServiceResult<EmployeeGovernmentIdResponseDto>.Invalid(
          "One or more government ID numbers are not valid.");
    }

    tin = tin is null ? null : GovernmentIdRules.ToStoredTin(tin);

    var now = DateTime.UtcNow;

    var governmentIds = await _context.EmployeeGovernmentIds
        .FirstOrDefaultAsync(eg => eg.EmployeeId == employeeId && eg.DeletedAt == null);

    if (governmentIds is null)
    {
      var nothingToSave = sss is null && philHealth is null && pagIbig is null && tin is null;
      if (nothingToSave)
      {
        return ServiceResult<EmployeeGovernmentIdResponseDto>.Success(new EmployeeGovernmentIdResponseDto());
      }

      governmentIds = new EmployeeGovernmentId
      {
        EmployeeId = employeeId,
        CreatedAt = now,
      };
      _context.EmployeeGovernmentIds.Add(governmentIds);
    }

    governmentIds.SssNumber = sss;
    governmentIds.PhilHealthNumber = philHealth;
    governmentIds.PagIbigNumber = pagIbig;
    governmentIds.TinNumber = tin;
    governmentIds.UpdatedAt = now;
    governmentIds.UpdatedById = updatedById;

    await _context.SaveChangesAsync();

    var response = await GetAsync(employeeId);
    return ServiceResult<EmployeeGovernmentIdResponseDto>.Success(response!);
  }
}