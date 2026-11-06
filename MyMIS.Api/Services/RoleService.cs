using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Authorization;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;
using MyMIS.Api.Models;

namespace MyMIS.Api.Services;

public class RoleService(AppDbContext context, IRolePermissionCache rolePermissions)
{
  private readonly AppDbContext _context = context;

  // Used by the write side (7d): invalidate a role's cache entry after an edit,
  // and read the CALLER's permissions for the escalation guard.
  private readonly IRolePermissionCache _rolePermissions = rolePermissions;

  // Same normalization as OfficeService: "  hr-Payroll " → "HR-PAYROLL".
  private static string NormalizeName(string name) => name.Trim().ToUpperInvariant();

  // ---------------- small helpers for the write side ----------------

  // DTO list → the (Name, Scope) shape ValidatePermissionsAsync takes.
  // Scope!.Value: [Required] already rejected a missing scope, so it can't be null here.
  private static List<(string Name, PermissionScope Scope)> ToRequested(
    IEnumerable<RolePermissionCreateDto> permissions) =>
    [.. permissions.Select(p => (p.Permission.Trim(), p.Scope!.Value))];

  private static string? TrimOrNull(string? value) =>
    string.IsNullOrWhiteSpace(value) ? null : value.Trim();

  // Carries a validation failure over to another result type.
  // ServiceResult<Dictionary<...>> and ServiceResult<RoleResponseDto> are different
  // types (like Promise<A> vs Promise<B> in TS), so the error has to be re-wrapped.
  private static ServiceResult<T> Fail<T>(ServiceResult<Dictionary<int, PermissionScope>> failed) =>
    failed.ErrorType switch
    {
      ServiceErrorType.Validation => ServiceResult<T>.Invalid(failed.ErrorMessage!),
      ServiceErrorType.Forbidden => ServiceResult<T>.Forbidden(failed.ErrorMessage!),
      _ => throw new InvalidOperationException($"Unexpected validation error type: {failed.ErrorType}"),
    };

  private async Task<bool> NameExistsAsync(string normalizedName, int? excludeId = null) =>
    await _context.Roles.AnyAsync(r =>
      r.NormalizedName == normalizedName && (excludeId == null || r.Id != excludeId));

  // Checks a requested permission list and, if it's acceptable, returns it as
  // permissionId → scope (ready to save). The checks, in order:
  //   400  a permission listed twice
  //   400  a name that isn't in the catalog
  //   400  a company-only permission with Department scope
  //   403  the caller's own role doesn't hold it at that scope (escalation guard)
  //
  // Input checks come before the 403, so a typo is reported as a typo, not as
  // "you're not allowed".
  private async Task<ServiceResult<Dictionary<int, PermissionScope>>> ValidatePermissionsAsync(
    IReadOnlyList<(string Name, PermissionScope Scope)> requested,
    int callerRoleId)
  {
    // GroupBy + Where(Count > 1): finds names that appear more than once,
    // like grouping an array by name in JS and keeping groups longer than 1.
    var duplicate = requested
      .GroupBy(p => p.Name)
      .FirstOrDefault(group => group.Count() > 1)?.Key;
    if (duplicate is not null)
    {
      return ServiceResult<Dictionary<int, PermissionScope>>.Invalid(
        $"'{duplicate}' is listed more than once.");
    }

    var unknown = requested.FirstOrDefault(p => !PermissionNames.All.Contains(p.Name)).Name;
    if (unknown is not null)
    {
      return ServiceResult<Dictionary<int, PermissionScope>>.Invalid(
        $"Unknown permission '{unknown}'.");
    }

    var companyOnly = requested.FirstOrDefault(p =>
      p.Scope == PermissionScope.Department && PermissionNames.CompanyOnly.Contains(p.Name)).Name;
    if (companyOnly is not null)
    {
      return ServiceResult<Dictionary<int, PermissionScope>>.Invalid(
        $"'{companyOnly}' can only be granted company-wide.");
    }

    // Escalation guard (R3): you can only grant what you hold yourself, at the same
    // or a wider scope. The caller's permissions come from the same cache the
    // authorization handler uses, so this matches exactly what they can do.
    var callerPermissions = await _rolePermissions.GetAsync(callerRoleId);
    foreach (var (name, scope) in requested)
    {
      if (!callerPermissions.TryGetValue(name, out var held) || !held.Covers(scope))
      {
        return ServiceResult<Dictionary<int, PermissionScope>>.Forbidden(
          $"You can't grant '{name}' ({scope}) because your own role doesn't hold it at that scope.");
      }
    }

    // Translate names to ids for saving. One query for the whole list.
    var names = requested.Select(p => p.Name).ToList();
    var ids = await _context.Permissions
      .Where(p => names.Contains(p.Name))
      .ToDictionaryAsync(p => p.Name, p => p.Id);

    // Every name passed the catalog check, so a missing row means the database
    // and PermissionNames disagree: a missed migration, i.e. a server bug, not user input.
    if (ids.Count != names.Count)
    {
      throw new InvalidOperationException(
        "A catalog permission has no row in the Permissions table. Has a migration been missed?");
    }

    return ServiceResult<Dictionary<int, PermissionScope>>.Success(
      requested.ToDictionary(p => ids[p.Name], p => p.Scope));
  }

  // GET /api/Permissions. No database: the catalog IS PermissionNames.All
  // `static`: uses no instance fields, so it doesn't need a RoleService object.
  public static List<PermissionResponseDto> GetPermissionCatalog() =>
    [.. PermissionNames.All
      .Order()
      .Select(name => new PermissionResponseDto
      {
        Name = name,
        CompanyOnly = PermissionNames.CompanyOnly.Contains(name),
        Customizable = PermissionNames.Customizable.Contains(name)
      })];

  // GET /api/Roles: every role with how many (active) employees hold it.
  public async Task<List<RoleSummaryResponseDto>> GetAllAsync()
  {
    return await _context.Roles
      .AsNoTracking()
      .OrderBy(r => r.Name)
      .Select(r => new RoleSummaryResponseDto
      {
        Id = r.Id,
        Name = r.Name,
        Description = r.Description,
        IsSystem = r.IsSystem,
        IsCustom = r.IsCustom,
        // A COUNT SUBQUERY: EF turns this into
        //   (SELECT COUNT(*) FROM "Employees" e WHERE e."RoleId" = r."Id" AND e."DeletedAt" IS NULL)
        // inside the same SQL statement, one query for the whole list, not one per role.
        // Employees' soft-delete filter applies automatically, so former employees don't count.
        EmployeeCount = _context.Employees.Count(e => e.RoleId == r.Id),
      })
      .ToListAsync();
  }

  // GET /api/Roles/{id}: the role plus its permissions and scopes.
  public async Task<RoleResponseDto?> GetByIdAsync(int id)
  {
    var role = await _context.Roles
      .AsNoTracking()
      .Where(r => r.Id == id)
      .Select(r => new RoleResponseDto
      {
        Id = r.Id,
        Name = r.Name,
        Description = r.Description,
        IsSystem = r.IsSystem,
        IsCustom = r.IsCustom,
        EmployeeCount = _context.Employees.Count(e => e.RoleId == r.Id),
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
        // A NESTED LIST PROJECTION: the role's RolePermissions, each mapped to a DTO.
        // EF loads them in the same round trip, so there's no separate query per row
        // and no .Include() needed, because Select already says which columns to fetch.
        Permissions = r.RolePermissions
          .OrderBy(rp => rp.Permission.Name)
          .Select(rp => new RolePermissionResponseDto
          {
            Permission = rp.Permission.Name,
            Scope = rp.Scope,
          })
          .ToList(),
      })
      .FirstOrDefaultAsync();

    if (role is null)
    {
      return null;
    }

    // Decision 1a: a system role's permissions are COMPUTED, never stored
    // (Step 6 deleted its rows). Show what the cache gives it: every permission,
    // company-wide. Otherwise the role editor would show SuperAdmin holding nothing.
    if (role.IsSystem)
    {
      role.Permissions = await _context.Permissions
        .AsNoTracking()
        .OrderBy(p => p.Name)
        .Select(p => new RolePermissionResponseDto
        {
          Permission = p.Name,
          Scope = PermissionScope.Company,
        })
        .ToListAsync();
    }

    return role;
  }

  // ---------------- write ----------------

  // POST /api/Roles
  public async Task<ServiceResult<RoleResponseDto>> CreateAsync(RoleCreateDto dto, int callerRoleId)
  {
    var normalized = NormalizeName(dto.Name);
    if (await NameExistsAsync(normalized))
    {
      return ServiceResult<RoleResponseDto>.Conflict($"A role named '{dto.Name.Trim()}' already exists.");
    }

    // dto.Permissions!: [Required] already rejected a missing list.
    var validation = await ValidatePermissionsAsync(ToRequested(dto.Permissions!), callerRoleId);
    if (!validation.IsSuccess)
    {
      return Fail<RoleResponseDto>(validation);
    }

    var now = DateTime.UtcNow;
    var role = new Role
    {
      Name = dto.Name.Trim(),
      NormalizedName = normalized,
      Description = TrimOrNull(dto.Description),
      CreatedAt = now,
      UpdatedAt = now,
      // permissionId → scope pairs, each becoming a RolePermission row.
      RolePermissions = [.. validation.Value!.Select(pair => new RolePermission
      {
        PermissionId = pair.Key,
        Scope = pair.Value,
      })],
    };

    _context.Roles.Add(role);
    await _context.SaveChangesAsync();

    // No cache invalidation: nobody holds a brand-new role yet.
    return ServiceResult<RoleResponseDto>.Success(
      await GetByIdAsync(role.Id) ?? throw new InvalidOperationException("Failed to reload newly created role."));
  }

  // PUT /api/Roles/{id}: renames, re-describes, and REPLACES the permission list (R1).
  public async Task<ServiceResult<RoleResponseDto>> UpdateAsync(int id, RoleCreateDto dto, int callerRoleId)
  {
    // Tracked (no AsNoTracking) with its rows loaded, because we're about to change them.
    var role = await _context.Roles
      .Include(r => r.RolePermissions)
      .FirstOrDefaultAsync(r => r.Id == id);

    if (role is null)
    {
      return ServiceResult<RoleResponseDto>.NotFound("Role not found.");
    }

    if (role.IsSystem)
    {
      return ServiceResult<RoleResponseDto>.Conflict(
        $"The {role.Name} role is managed by the system and can't be edited.");
    }

    var normalized = NormalizeName(dto.Name);
    if (await NameExistsAsync(normalized, excludeId: id))
    {
      return ServiceResult<RoleResponseDto>.Conflict($"A role named '{dto.Name.Trim()}' already exists.");
    }

    var validation = await ValidatePermissionsAsync(ToRequested(dto.Permissions!), callerRoleId);
    if (!validation.IsSuccess)
    {
      return Fail<RoleResponseDto>(validation);
    }

    var wanted = validation.Value!;

    // Replace the list by DIFFING, not by clearing and re-adding.
    // Clear-then-add would leave EF tracking a deleted (RoleId 5, PermissionId 1) row AND
    // a new (5, 1) row with the same composite key in one SaveChanges, and it throws
    // "another instance with the same key value is already being tracked".
    //
    // 1. Existing rows: keep them (updating the scope) or remove them.
    //    .ToList() makes a copy, because you can't remove from a collection while looping over it.
    foreach (var existing in role.RolePermissions.ToList())
    {
      if (wanted.TryGetValue(existing.PermissionId, out var scope))
      {
        existing.Scope = scope;
      }
      else
      {
        // Removing it from a REQUIRED relationship makes EF delete the row on save.
        role.RolePermissions.Remove(existing);
      }
    }

    // 2. Requested permissions the role doesn't have yet: add them.
    var keptIds = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();
    foreach (var (permissionId, scope) in wanted)
    {
      if (!keptIds.Contains(permissionId))
      {
        role.RolePermissions.Add(new RolePermission { PermissionId = permissionId, Scope = scope });
      }
    }

    role.Name = dto.Name.Trim();
    role.NormalizedName = normalized;
    role.Description = TrimOrNull(dto.Description);
    role.UpdatedAt = DateTime.UtcNow;

    await _context.SaveChangesAsync();

    // Everyone holding this role sees the change on their next request
    // instead of waiting up to 10 minutes for the cache entry to expire.
    _rolePermissions.Invalidate(id);

    return ServiceResult<RoleResponseDto>.Success(
      await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to reload updated role."));
  }

  // POST /api/Roles/{id}/clone (R4): copies permissions, scopes and description.
  public async Task<ServiceResult<RoleResponseDto>> CloneAsync(int id, RoleCloneDto dto, int callerRoleId)
  {
    // GetByIdAsync already turns a system role into "every permission, company-wide",
    // so cloning SuperAdmin copies all of them as stored rows.
    var source = await GetByIdAsync(id);
    if (source is null)
    {
      return ServiceResult<RoleResponseDto>.NotFound("Role not found.");
    }

    var normalized = NormalizeName(dto.Name);
    if (await NameExistsAsync(normalized))
    {
      return ServiceResult<RoleResponseDto>.Conflict($"A role named '{dto.Name.Trim()}' already exists.");
    }

    // The escalation guard applies here too: cloning a role you couldn't build
    // yourself would be a back door to the same permissions.
    var requested = source.Permissions.Select(p => (p.Permission, p.Scope)).ToList();
    var validation = await ValidatePermissionsAsync(requested, callerRoleId);
    if (!validation.IsSuccess)
    {
      return Fail<RoleResponseDto>(validation);
    }

    var now = DateTime.UtcNow;
    var clone = new Role
    {
      Name = dto.Name.Trim(),
      NormalizedName = normalized,
      // null = copy the source's description; anything else replaces it.
      Description = dto.Description is null ? source.Description : TrimOrNull(dto.Description),
      IsSystem = false,   // never copied
      IsCustom = false,   // never copied
      CreatedAt = now,
      UpdatedAt = now,
      RolePermissions = [.. validation.Value!.Select(pair => new RolePermission
      {
        PermissionId = pair.Key,
        Scope = pair.Value,
      })],
    };

    _context.Roles.Add(clone);
    await _context.SaveChangesAsync();

    return ServiceResult<RoleResponseDto>.Success(
      await GetByIdAsync(clone.Id) ?? throw new InvalidOperationException("Failed to reload cloned role."));
  }

  public async Task<ServiceResult<bool>> DeleteAsync(int id)
  {
    var role = await _context.Roles.FindAsync(id);
    if (role is null)
    {
      return ServiceResult<bool>.NotFound("Role not found.");
    }

    if (role.IsSystem)
    {
      return ServiceResult<bool>.Conflict($"The {role.Name} role is managed by the system and can't be deleted.");
    }

    // The soft-delete query filter applies here, so this counts ACTIVE employees only.
    var activeHolders = await _context.Employees.CountAsync(e => e.RoleId == id);
    if (activeHolders > 0)
    {
      return ServiceResult<bool>.Conflict(
        $"This role is still assigned to {activeHolders} employee(s). Move them to another role first.");
    }

    var employeeRoleId = await _context.Roles
      .Where(r => r.NormalizedName == SystemRoleNames.Employee)
      .Select(r => (int?)r.Id)
      .FirstOrDefaultAsync()
      ?? throw new InvalidOperationException("The system Employee role is missing. Have the migrations been applied?");

    // IgnoreQueryFilters() switches OFF the soft-delete filter for this one query,
    // so we can reach former employees. The DeletedAt check then keeps ONLY them.
    var formerHolders = await _context.Employees
      .IgnoreQueryFilters()
      .Where(e => e.RoleId == id && e.DeletedAt != null)
      .ToListAsync();

    foreach (var former in formerHolders)
    {
      former.RoleId = employeeRoleId;
    }

    _context.Roles.Remove(role);   // its RolePermissions rows go with it (cascade)

    // ONE SaveChanges = ONE database transaction: the reassignments and the delete
    // succeed or fail together. EF also orders the commands itself, updating the
    // employees' RoleId BEFORE deleting the role, so the foreign key is never violated.
    await _context.SaveChangesAsync();

    // Nobody holds the role any more, but drop its cache entry so nothing stale lingers.
    _rolePermissions.Invalidate(id);

    return ServiceResult<bool>.Success(true);
  }
}