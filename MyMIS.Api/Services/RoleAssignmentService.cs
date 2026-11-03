using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Authorization;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;
using MyMIS.Api.Models;

namespace MyMIS.Api.Services;

// Who holds which role: reading it, changing it (design doc §6c), and the
// "may this caller hand out this role?" check that employee create reuses.
public class RoleAssignmentService(AppDbContext context, IRolePermissionCache rolePermissions)
{
  private readonly AppDbContext _context = context;
  private readonly IRolePermissionCache _rolePermissions = rolePermissions;

  // ---------------- helpers ----------------

  // Re-wraps an error from one ServiceResult type into another.
  // Two type parameters this time: TFrom is what we have, T is what we need,
  // like a TS function <T, TFrom>(r: Result<TFrom>): Result<T>.
  private static ServiceResult<T> Fail<T, TFrom>(ServiceResult<TFrom> failed) =>
    failed.ErrorType switch
    {
      ServiceErrorType.NotFound => ServiceResult<T>.NotFound(failed.ErrorMessage!),
      ServiceErrorType.Validation => ServiceResult<T>.Invalid(failed.ErrorMessage!),
      ServiceErrorType.Conflict => ServiceResult<T>.Conflict(failed.ErrorMessage!),
      ServiceErrorType.Forbidden => ServiceResult<T>.Forbidden(failed.ErrorMessage!),
      _ => throw new InvalidOperationException($"Unexpected error type: {failed.ErrorType}"),
    };

  private Task<bool> IsSystemRoleAsync(int roleId) =>
    _context.Roles.AnyAsync(r => r.Id == roleId && r.IsSystem);

  // ---------------- read ----------------

  // GET /api/Employees/{id}/role. null = no such (active) employee.
  public async Task<EmployeeRoleResponseDto?> GetAsync(int employeeId) =>
    await _context.Employees
      .AsNoTracking()
      .Where(e => e.Id == employeeId)
      .Select(e => new EmployeeRoleResponseDto { RoleId = e.RoleId, RoleName = e.Role.Name })
      .FirstOrDefaultAsync();

  // ---------------- the shared check (rules 3, 5 for the NEW role, and 6) ----------------

  // May the caller hand out `newRoleId`? Used by AssignAsync AND by employee create.
  // Returns the role on success, so callers don't have to load it again.
  public async Task<ServiceResult<Role>> CheckCanAssignAsync(int newRoleId, int callerRoleId)
  {
    // Rule 3: the role must exist. 400, not 404: the missing thing is a value
    // in the request body, not the resource in the URL.
    var newRole = await _context.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == newRoleId);
    if (newRole is null)
    {
      return ServiceResult<Role>.Invalid("Role not found.");
    }

    // Rule 5 (new-role half): only a SuperAdmin can hand out the SuperAdmin role.
    if (newRole.IsSystem && !await IsSystemRoleAsync(callerRoleId))
    {
      return ServiceResult<Role>.Forbidden($"Only a SuperAdmin can assign the {newRole.Name} role.");
    }

    // Rule 6: the escalation guard. Compare the NEW role's permissions with the
    // CALLER's, both read from the cache. For a system role, the cache already
    // computes "every permission, company-wide", so there's no special case.
    var callerPermissions = await _rolePermissions.GetAsync(callerRoleId);
    var rolePermissions = await _rolePermissions.GetAsync(newRoleId);

    foreach (var (name, scope) in rolePermissions)
    {
      if (!callerPermissions.TryGetValue(name, out var held) || !held.Covers(scope))
      {
        return ServiceResult<Role>.Forbidden(
          $"You can't assign the {newRole.Name} role: it includes '{name}' ({scope}), " +
          "which your own role doesn't hold at that scope.");
      }
    }

    return ServiceResult<Role>.Success(newRole);
  }

  // ---------------- assign (all 8 rules, in order) ----------------

  // PUT /api/Employees/{id}/role
  public async Task<ServiceResult<EmployeeRoleResponseDto>> AssignAsync(
    int employeeId, int newRoleId, int callerEmployeeId, int callerRoleId)
  {
    // Rule 1: the target exists. Tracked + its current role loaded, since we may change it.
    var employee = await _context.Employees
      .Include(e => e.Role)
      .FirstOrDefaultAsync(e => e.Id == employeeId);

    if (employee is null)
    {
      return ServiceResult<EmployeeRoleResponseDto>.NotFound("Employee not found.");
    }

    // Rule 2 (A1): never your own role, whatever you hold.
    if (employeeId == callerEmployeeId)
    {
      return ServiceResult<EmployeeRoleResponseDto>.Forbidden("You can't change your own role.");
    }

    // Rule 4: already in that role → nothing to do. Checked BEFORE the permission
    // rules on purpose: re-saving an unchanged form shouldn't fail, and must not
    // revoke anyone's tokens for a change that didn't happen.
    if (employee.RoleId == newRoleId)
    {
      return ServiceResult<EmployeeRoleResponseDto>.Success(
        new EmployeeRoleResponseDto { RoleId = employee.RoleId, RoleName = employee.Role.Name });
    }

    // Rule 5 (current-role half, A4): only a SuperAdmin can move someone OUT of SuperAdmin.
    if (employee.Role.IsSystem && !await IsSystemRoleAsync(callerRoleId))
    {
      return ServiceResult<EmployeeRoleResponseDto>.Forbidden(
        $"Only a SuperAdmin can change the role of someone in the {employee.Role.Name} role.");
    }

    // Rules 3, 5 (new-role half) and 6.
    var check = await CheckCanAssignAsync(newRoleId, callerRoleId);
    if (!check.IsSuccess)
    {
      return Fail<EmployeeRoleResponseDto, Role>(check);
    }

    var newRole = check.Value!;

    // Rule 7 (1b): never move the LAST active holder out of a system role.
    // Rules 2 and 5 already make this nearly impossible (the caller must be another
    // SuperAdmin), so it's a safety net, e.g. for a future second system role.
    if (employee.Role.IsSystem && !newRole.IsSystem)
    {
      var holders = await _context.Employees.CountAsync(e => e.RoleId == employee.RoleId);
      if (holders <= 1)
      {
        return ServiceResult<EmployeeRoleResponseDto>.Conflict(
          $"{employee.Role.Name} must keep at least one active employee.");
      }
    }

    // Rule 8: change the role AND revoke every active refresh token (decision 8),
    // so every device must log in again and pick up the new roleId claim.
    employee.RoleId = newRoleId;

    var now = DateTime.UtcNow;
    var activeTokens = await _context.RefreshTokens
      .Where(t => t.EmployeeId == employeeId && t.RevokedAt == null)
      .ToListAsync();

    foreach (var token in activeTokens)
    {
      token.RevokedAt = now;
    }

    // One SaveChanges = one transaction: the new role and the revocations land together.
    await _context.SaveChangesAsync();

    return ServiceResult<EmployeeRoleResponseDto>.Success(
      new EmployeeRoleResponseDto { RoleId = newRole.Id, RoleName = newRole.Name });
  }
}