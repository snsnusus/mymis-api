using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;
using MyMIS.Api.Models;

namespace MyMIS.Api.Services;

public class EmployeeHmoService(AppDbContext context)
{
  private readonly AppDbContext _context = context;

  public async Task<ServiceResult<EmployeeHmoResponseDto>> CreateAsync(
      int employeeId,
      EmployeeHmoCreateDto dto)
  {
    if (!await _context.Employees.AnyAsync(e => e.Id == employeeId))
    {
      return ServiceResult<EmployeeHmoResponseDto>.NotFound(
          $"Employee {employeeId} was not found.");
    }

    if (await _context.EmployeeHmos.AnyAsync(h => h.EmployeeId == employeeId))
    {
      return ServiceResult<EmployeeHmoResponseDto>.Conflict(
          "This employee already has an active HMO enrollment. Update it, or end it before enrolling again.");
    }

    var planResult = await ValidateForCreateAsync(dto);
    if (!planResult.IsSuccess)
    {
      return ServiceResult<EmployeeHmoResponseDto>.Invalid(planResult.ErrorMessage!);
    }

    var enrollment = NewEntity(dto, planResult.Value!);
    enrollment.EmployeeId = employeeId;

    _context.EmployeeHmos.Add(enrollment);
    await _context.SaveChangesAsync();

    var response = await GetActiveAsync(employeeId);
    return ServiceResult<EmployeeHmoResponseDto>.Success(response!);
  }

  public async Task<ServiceResult<EmployeeHmoResponseDto>> UpdateAsync(
      int employeeId,
      EmployeeHmoUpdateDto dto)
  {
    var enrollment = await _context.EmployeeHmos
        .FirstOrDefaultAsync(h => h.EmployeeId == employeeId);

    if (enrollment is null)
    {
      return ServiceResult<EmployeeHmoResponseDto>.NotFound(
          $"Employee {employeeId} has no active HMO enrollment.");
    }

    var planResult = await ValidatePlanAsync(dto);
    if (!planResult.IsSuccess)
    {
      return ServiceResult<EmployeeHmoResponseDto>.Invalid(planResult.ErrorMessage!);
    }

    var plan = planResult.Value!;
    if (plan.HmoProviderId != enrollment.HmoProviderId)
    {
      return ServiceResult<EmployeeHmoResponseDto>.Invalid(
          "To change HMO provider, end this enrollment and create a new one.");
    }

    enrollment.HmoPlanId = plan.Id;
    enrollment.MemberNumber = dto.MemberNumber.Trim().ToUpperInvariant();
    enrollment.EffectiveDate = dto.EffectiveDate!.Value;
    enrollment.EndDate = dto.EndDate;
    enrollment.UpdatedAt = DateTime.UtcNow;

    await _context.SaveChangesAsync();

    var response = await GetActiveAsync(employeeId);
    return ServiceResult<EmployeeHmoResponseDto>.Success(response!);
  }

  // Soft-deletes the active enrollment and its dependents. The rows stay as history.
  public async Task<ServiceResult<bool>> DeleteAsync(int employeeId)
  {
    var enrollment = await _context.EmployeeHmos
        .Include(h => h.Dependents)
        .FirstOrDefaultAsync(h => h.EmployeeId == employeeId);

    if (enrollment is null)
    {
      return ServiceResult<bool>.NotFound(
          $"Employee {employeeId} has no active HMO enrollment.");
    }

    var now = DateTime.UtcNow;
    enrollment.DeletedAt = now;
    enrollment.UpdatedAt = now;

    foreach (var dependent in enrollment.Dependents)
    {
      dependent.DeletedAt = now;
      dependent.UpdatedAt = now;
    }

    await _context.SaveChangesAsync();
    return ServiceResult<bool>.Success(true);
  }

  public async Task<EmployeeHmoResponseDto?> GetActiveAsync(int employeeId)
  {
    return await _context.EmployeeHmos.AsNoTracking()
      .Where(h => h.EmployeeId == employeeId)
      .Select((h) => new EmployeeHmoResponseDto
      {
        Id = h.Id,
        Provider = new HmoRefDto
        {
          Id = h.HmoProvider.Id,
          Name = h.HmoProvider.Name,
        },
        Plan = new HmoRefDto
        {
          Id = h.HmoPlan.Id,
          Name = h.HmoPlan.Name,
        },
        MemberNumber = h.MemberNumber,
        EffectiveDate = h.EffectiveDate,
        EndDate = h.EndDate,
        Dependents = h.Dependents.OrderBy(d => d.Id).Select((d) => new EmployeeHmoDependentResponseDto
        {
          Id = d.Id,
          FirstName = d.FirstName,
          MiddleName = d.MiddleName,
          LastName = d.LastName,
          Suffix = d.Suffix,
          Birthdate = d.Birthdate,
          Relationship = d.Relationship,
          Plan = new HmoRefDto
          {
            Id = d.HmoPlan.Id,
            Name = d.HmoPlan.Name
          },
          MemberNumber = d.MemberNumber

        }).ToList(),
      })
      .FirstOrDefaultAsync();
  }

  // Everything a new enrollment needs checked against the database.
  // Public so EmployeesController can run it before creating an employee (step 6).
  // On success, returns the employee's plan so callers can build the entity without re-querying.
  public async Task<ServiceResult<HmoPlan>> ValidateForCreateAsync(EmployeeHmoCreateDto dto)
  {
    var planResult = await ValidatePlanAsync(dto);
    if (!planResult.IsSuccess)
    {
      return planResult;
    }

    var dependentError = await ValidateDependentPlansAsync(
        planResult.Value!,
        (dto.Dependents ?? []).Select(d => d.HmoPlanId));

    return dependentError is null
        ? planResult
        : ServiceResult<HmoPlan>.Invalid(dependentError);
  }

  // The employee's plan: exists and is still offered (plan and provider both active).
  // Contract dates on the provider are kept for the record only; they don't limit enrollment.
  private async Task<ServiceResult<HmoPlan>> ValidatePlanAsync(EmployeeHmoFieldsDto dto)
  {
    var planId = dto.HmoPlanId!.Value;

    var plan = await _context.HmoPlans
        .AsNoTracking()
        .Include(p => p.HmoProvider)
        .FirstOrDefaultAsync(p => p.Id == planId);

    if (plan is null)
    {
      return ServiceResult<HmoPlan>.Invalid($"HMO plan {planId} was not found.");
    }

    if (!plan.IsActive || !plan.HmoProvider.IsActive)
    {
      return ServiceResult<HmoPlan>.Invalid("This HMO plan is no longer offered.");
    }

    return ServiceResult<HmoPlan>.Success(plan);
  }

  // Checks every plan used by the dependents in one query.
  // A null id means "same plan as the employee", resolved here first,
  // so the default goes through the same rules as an explicit choice.
  private async Task<string?> ValidateDependentPlansAsync(
      HmoPlan employeePlan,
      IEnumerable<int?> dependentPlanIds)
  {
    var planIds = dependentPlanIds
        .Select(id => id ?? employeePlan.Id)
        .Distinct()
        .ToList();

    if (planIds.Count == 0)
    {
      return null; // no dependents, nothing to check
    }

    var plans = await _context.HmoPlans
        .AsNoTracking()
        .Where(p => planIds.Contains(p.Id))
        .Select(p => new { p.Id, p.Name, p.HmoProviderId, p.AllowDependents, p.IsActive })
        .ToListAsync();

    foreach (var planId in planIds)
    {
      var plan = plans.FirstOrDefault(p => p.Id == planId);
      if (plan is null)
      {
        return $"HMO plan {planId} was not found.";
      }

      if (plan.HmoProviderId != employeePlan.HmoProviderId)
      {
        return $"Dependents must be on a plan from the employee's HMO provider; \"{plan.Name}\" is not.";
      }

      if (!plan.AllowDependents)
      {
        return $"The \"{plan.Name}\" plan does not allow dependents.";
      }

      if (!plan.IsActive)
      {
        return $"The \"{plan.Name}\" plan is no longer offered.";
      }
    }

    return null;
  }

  public async Task<ServiceResult<EmployeeHmoResponseDto>> AddDependentAsync(
      int employeeId,
      EmployeeHmoDependentCreateDto dto)
  {
    var enrollment = await LoadActiveEnrollmentAsync(employeeId);
    if (enrollment is null)
    {
      return ServiceResult<EmployeeHmoResponseDto>.NotFound(
          $"Employee {employeeId} has no active HMO enrollment.");
    }

    var memberNumber = NullIfBlank(dto.MemberNumber)?.ToUpperInvariant();
    if (await DependentMemberNumberTakenAsync(enrollment.Id, memberNumber, excludeDependentId: null))
    {
      return ServiceResult<EmployeeHmoResponseDto>.Conflict(
          "Another dependent on this enrollment already uses this member number.");
    }

    var planError = await ValidateDependentPlansAsync(enrollment.HmoPlan, [dto.HmoPlanId]);
    if (planError is not null)
    {
      return ServiceResult<EmployeeHmoResponseDto>.Invalid(planError);
    }

    var dependent = NewDependent(dto, enrollment.HmoPlanId, DateTime.UtcNow);
    dependent.EmployeeHmoId = enrollment.Id;

    _context.EmployeeHmoDependents.Add(dependent);
    await _context.SaveChangesAsync();

    var response = await GetActiveAsync(employeeId);
    return ServiceResult<EmployeeHmoResponseDto>.Success(response!);
  }

  public async Task<ServiceResult<EmployeeHmoResponseDto>> UpdateDependentAsync(
      int employeeId,
      int dependentId,
      EmployeeHmoDependentCreateDto dto)
  {
    var enrollment = await LoadActiveEnrollmentAsync(employeeId);
    if (enrollment is null)
    {
      return ServiceResult<EmployeeHmoResponseDto>.NotFound(
          $"Employee {employeeId} has no active HMO enrollment.");
    }

    // Matching on both ids is the IDOR guard: a dependent of another
    // employee's enrollment is "not found", never editable.
    var dependent = await _context.EmployeeHmoDependents
        .FirstOrDefaultAsync(d => d.Id == dependentId && d.EmployeeHmoId == enrollment.Id);

    if (dependent is null)
    {
      return ServiceResult<EmployeeHmoResponseDto>.NotFound(
          $"Dependent {dependentId} was not found.");
    }

    var memberNumber = NullIfBlank(dto.MemberNumber)?.ToUpperInvariant();
    if (await DependentMemberNumberTakenAsync(enrollment.Id, memberNumber, excludeDependentId: dependentId))
    {
      return ServiceResult<EmployeeHmoResponseDto>.Conflict(
          "Another dependent on this enrollment already uses this member number.");
    }

    var planError = await ValidateDependentPlansAsync(enrollment.HmoPlan, [dto.HmoPlanId]);
    if (planError is not null)
    {
      return ServiceResult<EmployeeHmoResponseDto>.Invalid(planError);
    }

    dependent.FirstName = dto.FirstName.Trim();
    dependent.MiddleName = NullIfBlank(dto.MiddleName);
    dependent.LastName = dto.LastName.Trim();
    dependent.Suffix = NullIfBlank(dto.Suffix);
    dependent.Birthdate = dto.Birthdate!.Value;
    dependent.Relationship = dto.Relationship!.Value;
    dependent.HmoPlanId = dto.HmoPlanId ?? enrollment.HmoPlanId;
    dependent.MemberNumber = memberNumber;
    dependent.UpdatedAt = DateTime.UtcNow;

    await _context.SaveChangesAsync();

    var response = await GetActiveAsync(employeeId);
    return ServiceResult<EmployeeHmoResponseDto>.Success(response!);
  }

  public async Task<ServiceResult<bool>> DeleteDependentAsync(int employeeId, int dependentId)
  {
    var enrollment = await LoadActiveEnrollmentAsync(employeeId);
    if (enrollment is null)
    {
      return ServiceResult<bool>.NotFound(
          $"Employee {employeeId} has no active HMO enrollment.");
    }

    var dependent = await _context.EmployeeHmoDependents
        .FirstOrDefaultAsync(d => d.Id == dependentId && d.EmployeeHmoId == enrollment.Id);

    if (dependent is null)
    {
      return ServiceResult<bool>.NotFound($"Dependent {dependentId} was not found.");
    }

    var now = DateTime.UtcNow;
    dependent.DeletedAt = now;
    dependent.UpdatedAt = now;

    await _context.SaveChangesAsync();
    return ServiceResult<bool>.Success(true);
  }

  // Builds an unsaved enrollment with no EmployeeId. Either the caller sets it,
  // or EF fills it in when the enrollment is added to employee.HmoEnrollments.
  // `plan` must come from ValidateForCreateAsync.
  public static EmployeeHmo NewEntity(EmployeeHmoCreateDto dto, HmoPlan plan)
  {
    var now = DateTime.UtcNow;

    return new EmployeeHmo
    {
      HmoProviderId = plan.HmoProviderId, // copied from the plan, never sent by the client
      HmoPlanId = plan.Id,
      MemberNumber = dto.MemberNumber.Trim().ToUpperInvariant(),
      EffectiveDate = dto.EffectiveDate!.Value,
      EndDate = dto.EndDate,
      Dependents = [.. (dto.Dependents ?? []).Select(d => NewDependent(d, plan.Id, now))],
      CreatedAt = now,
      UpdatedAt = now,
    };
  }

  private static EmployeeHmoDependent NewDependent(
      EmployeeHmoDependentCreateDto dto,
      int defaultPlanId,
      DateTime now) => new()
      {
        FirstName = dto.FirstName.Trim(),
        MiddleName = NullIfBlank(dto.MiddleName),
        LastName = dto.LastName.Trim(),
        Suffix = NullIfBlank(dto.Suffix),
        Birthdate = dto.Birthdate!.Value,
        Relationship = dto.Relationship!.Value,
        HmoPlanId = dto.HmoPlanId ?? defaultPlanId, // the default is stored as a real value
        MemberNumber = NullIfBlank(dto.MemberNumber)?.ToUpperInvariant(),
        CreatedAt = now,
        UpdatedAt = now,
      };

  private static string? NullIfBlank(string? value) =>
      string.IsNullOrWhiteSpace(value) ? null : value.Trim();

  // The employee's active enrollment with its plan loaded, for reading only.
  private Task<EmployeeHmo?> LoadActiveEnrollmentAsync(int employeeId) =>
      _context.EmployeeHmos
          .AsNoTracking()
          .Include(h => h.HmoPlan)
          .FirstOrDefaultAsync(h => h.EmployeeId == employeeId);

  // Dependent member numbers have no unique index, so this check is the only guard.
  private async Task<bool> DependentMemberNumberTakenAsync(
      int enrollmentId,
      string? memberNumber,
      int? excludeDependentId)
  {
    if (memberNumber is null)
    {
      return false; // optional field; blanks never clash
    }

    return await _context.EmployeeHmoDependents.AnyAsync(d =>
        d.EmployeeHmoId == enrollmentId &&
        d.MemberNumber == memberNumber &&
        d.Id != excludeDependentId);
  }
}