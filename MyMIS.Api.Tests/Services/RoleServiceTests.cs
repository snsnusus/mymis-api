using Microsoft.EntityFrameworkCore;
using Moq;
using MyMIS.Api.Authorization;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;
using MyMIS.Api.Models;
using MyMIS.Api.Services;

namespace MyMIS.Api.Tests.Services;

public class RoleServiceTests : IDisposable
{
  // The fake caller's role id. The mocked cache answers for exactly this id.
  private const int CallerRoleId = 99;

  private readonly AppDbContext _context;
  private readonly DateTime _now = DateTime.UtcNow;
  private readonly int _employeeRoleId;

  public RoleServiceTests()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
      .UseInMemoryDatabase(Guid.NewGuid().ToString())
      .Options;

    _context = new AppDbContext(options);

    // The catalog: ValidatePermissionsAsync turns names into ids from this table
    // (and throws if a row is missing), so every test needs it.
    _context.Permissions.AddRange(PermissionNames.All.Select(name => new Permission { Name = name }));

    // The system Employee role: DeleteAsync moves former holders onto it.
    var employeeRole = new Role
    {
      Name = "Employee",
      NormalizedName = SystemRoleNames.Employee,
      CreatedAt = _now,
      UpdatedAt = _now,
    };
    _context.Roles.Add(employeeRole);

    _context.SaveChanges();   // sync: constructors can't await
    _employeeRoleId = employeeRole.Id;
  }

  public void Dispose()
  {
    _context.Dispose();
    GC.SuppressFinalize(this);
  }

  // ---------------- helpers ----------------

  // A RoleService whose (mocked) cache says the CALLER's role holds exactly these
  // permissions. Returns the mock too, so tests can Verify Invalidate calls.
  private (RoleService Service, Mock<IRolePermissionCache> Cache) ServiceFor(
    params (string Name, PermissionScope Scope)[] callerHolds)
  {
    var cache = new Mock<IRolePermissionCache>();
    cache
      .Setup(c => c.GetAsync(CallerRoleId))
      .ReturnsAsync(callerHolds.ToDictionary(p => p.Name, p => p.Scope));

    return (new RoleService(_context, cache.Object), cache);
  }

  // A caller holding every permission company-wide, for tests that aren't about escalation.
  private (RoleService Service, Mock<IRolePermissionCache> Cache) ServiceForAllPermissions() =>
    ServiceFor([.. PermissionNames.All.Select(name => (name, PermissionScope.Company))]);

  private static RoleCreateDto NewDto(string name, params (string Name, PermissionScope Scope)[] permissions) => new()
  {
    Name = name,
    Permissions = [.. permissions.Select(p => new RolePermissionCreateDto { Permission = p.Name, Scope = p.Scope })],
  };

  // Inserts a normal role straight into the database. NormalizedName must be set
  // here, because the name checks compare that column, not Name.
  private async Task<Role> SeedRoleAsync(string name, params (string Name, PermissionScope Scope)[] permissions)
  {
    var ids = await _context.Permissions.ToDictionaryAsync(p => p.Name, p => p.Id);

    var role = new Role
    {
      Name = name,
      NormalizedName = name.Trim().ToUpperInvariant(),
      CreatedAt = _now,
      UpdatedAt = _now,
      RolePermissions = [.. permissions.Select(p => new RolePermission
      {
        PermissionId = ids[p.Name],
        Scope = p.Scope,
      })],
    };

    _context.Roles.Add(role);
    await _context.SaveChangesAsync();
    return role;
  }

  // A system role with NO stored permission rows, like the real SuperAdmin after Step 6.
  private async Task<Role> SeedSystemRoleAsync(string name = "SuperAdmin")
  {
    var role = new Role
    {
      Name = name,
      NormalizedName = name.ToUpperInvariant(),
      IsSystem = true,
      CreatedAt = _now,
      UpdatedAt = _now,
    };

    _context.Roles.Add(role);
    await _context.SaveChangesAsync();
    return role;
  }

  private async Task<Employee> SeedEmployeeAsync(int roleId, bool deleted = false)
  {
    var suffix = Guid.NewGuid().ToString("N")[..8];

    var employee = new Employee
    {
      FirstName = "Test",
      LastName = suffix,
      Gender = "MALE",
      MaritalStatus = "SINGLE",
      EmployeeCode = $"EMP-{suffix}",
      Username = $"u{suffix}",
      PasswordHash = "irrelevant-for-this-test",
      RoleId = roleId,
      DeletedAt = deleted ? _now : null,
    };

    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();
    return employee;
  }

  // What's actually stored for a role, read fresh (AsNoTracking) as name → scope.
  // Select first: reading rp.Permission.Name inside ToDictionaryAsync would run in C#
  // after the query, where the navigation was never loaded.
  private Task<Dictionary<string, PermissionScope>> SavedPermissionsAsync(int roleId) =>
    _context.RolePermissions
      .AsNoTracking()
      .Where(rp => rp.RoleId == roleId)
      .Select(rp => new { rp.Permission.Name, rp.Scope })
      .ToDictionaryAsync(x => x.Name, x => x.Scope);

  // ---------------- CreateAsync ----------------

  [Fact]
  public async Task CreateAsync_ValidRole_SavesTrimmedNameAndExactPermissions()
  {
    // Arrange
    var (service, _) = ServiceForAllPermissions();
    var dto = NewDto("  HR  ",
      (PermissionNames.Employees.Read, PermissionScope.Company),
      (PermissionNames.Employees.Update, PermissionScope.Department));

    // Act
    var result = await service.CreateAsync(dto, CallerRoleId);

    // Assert
    Assert.True(result.IsSuccess);
    var saved = await _context.Roles.AsNoTracking().FirstAsync(r => r.Id == result.Value!.Id);
    Assert.Equal("HR", saved.Name);
    Assert.Equal("HR", saved.NormalizedName);

    var permissions = await SavedPermissionsAsync(saved.Id);
    Assert.Equal(2, permissions.Count);
    Assert.Equal(PermissionScope.Company, permissions[PermissionNames.Employees.Read]);
    Assert.Equal(PermissionScope.Department, permissions[PermissionNames.Employees.Update]);
  }

  [Fact]
  public async Task CreateAsync_NameDiffersOnlyInCase_ReturnsConflict()
  {
    // Arrange
    await SeedRoleAsync("Payroll");
    var (service, _) = ServiceForAllPermissions();

    // Act
    var result = await service.CreateAsync(NewDto("payroll"), CallerRoleId);

    // Assert
    Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
  }

  [Fact]
  public async Task CreateAsync_PermissionListedTwice_ReturnsValidation()
  {
    // Arrange
    var (service, _) = ServiceForAllPermissions();
    var dto = NewDto("HR",
      (PermissionNames.Employees.Read, PermissionScope.Company),
      (PermissionNames.Employees.Read, PermissionScope.Department));

    // Act
    var result = await service.CreateAsync(dto, CallerRoleId);

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
  }

  [Fact]
  public async Task CreateAsync_UnknownPermission_ReturnsValidation()
  {
    // Arrange
    var (service, _) = ServiceForAllPermissions();

    // Act
    var result = await service.CreateAsync(
      NewDto("HR", ("employees.fly", PermissionScope.Company)), CallerRoleId);

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
  }

  [Fact]
  public async Task CreateAsync_CompanyOnlyPermissionWithDepartmentScope_ReturnsValidation()
  {
    // Arrange
    var (service, _) = ServiceForAllPermissions();

    // Act
    var result = await service.CreateAsync(
      NewDto("Onboarding", (PermissionNames.Employees.Create, PermissionScope.Department)), CallerRoleId);

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
  }

  [Fact]
  public async Task CreateAsync_CallerDoesNotHoldThePermission_ReturnsForbidden()
  {
    // Arrange: the caller can read employees but not update them
    var (service, _) = ServiceFor((PermissionNames.Employees.Read, PermissionScope.Company));

    // Act
    var result = await service.CreateAsync(
      NewDto("HR", (PermissionNames.Employees.Update, PermissionScope.Company)), CallerRoleId);

    // Assert
    Assert.Equal(ServiceErrorType.Forbidden, result.ErrorType);
    Assert.False(await _context.Roles.AnyAsync(r => r.NormalizedName == "HR"));
  }

  [Fact]
  public async Task CreateAsync_CallerHoldsDepartmentButRequestsCompany_ReturnsForbidden()
  {
    // Arrange: a manager-style caller can't hand out a WIDER scope than their own
    var (service, _) = ServiceFor((PermissionNames.Employees.Update, PermissionScope.Department));

    // Act
    var result = await service.CreateAsync(
      NewDto("HR", (PermissionNames.Employees.Update, PermissionScope.Company)), CallerRoleId);

    // Assert
    Assert.Equal(ServiceErrorType.Forbidden, result.ErrorType);
  }

  [Fact]
  public async Task CreateAsync_CallerHoldsCompanyAndRequestsDepartment_Succeeds()
  {
    // Arrange: Company covers Department
    var (service, _) = ServiceFor((PermissionNames.Employees.Update, PermissionScope.Company));

    // Act
    var result = await service.CreateAsync(
      NewDto("Manager", (PermissionNames.Employees.Update, PermissionScope.Department)), CallerRoleId);

    // Assert
    Assert.True(result.IsSuccess);
    var permissions = await SavedPermissionsAsync(result.Value!.Id);
    Assert.Equal(PermissionScope.Department, permissions[PermissionNames.Employees.Update]);
  }

  // ---------------- UpdateAsync ----------------

  [Fact]
  public async Task UpdateAsync_RoleDoesNotExist_ReturnsNotFound()
  {
    // Arrange
    var (service, _) = ServiceForAllPermissions();

    // Act
    var result = await service.UpdateAsync(123_456, NewDto("HR"), CallerRoleId);

    // Assert
    Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
  }

  [Fact]
  public async Task UpdateAsync_SystemRole_ReturnsConflict()
  {
    // Arrange
    var superAdmin = await SeedSystemRoleAsync();
    var (service, _) = ServiceForAllPermissions();

    // Act
    var result = await service.UpdateAsync(superAdmin.Id, NewDto("Renamed"), CallerRoleId);

    // Assert
    Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
  }

  [Fact]
  public async Task UpdateAsync_RenameToAnotherRolesName_ReturnsConflict()
  {
    // Arrange
    await SeedRoleAsync("HR");
    var payroll = await SeedRoleAsync("Payroll");
    var (service, _) = ServiceForAllPermissions();

    // Act
    var result = await service.UpdateAsync(payroll.Id, NewDto("hr"), CallerRoleId);

    // Assert
    Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
  }

  [Fact]
  public async Task UpdateAsync_RenameToItsOwnNameInAnotherCase_Succeeds()
  {
    // Arrange: the excludeId path, so a role doesn't conflict with itself
    var role = await SeedRoleAsync("hr");
    var (service, _) = ServiceForAllPermissions();

    // Act
    var result = await service.UpdateAsync(role.Id, NewDto("HR"), CallerRoleId);

    // Assert
    Assert.True(result.IsSuccess);
    Assert.Equal("HR", result.Value!.Name);
  }

  [Fact]
  public async Task UpdateAsync_NewPermissionList_ReplacesTheOldOneExactly()
  {
    // Arrange: stored = employees.read (Company) + employees.update (Company)
    var role = await SeedRoleAsync("HR",
      (PermissionNames.Employees.Read, PermissionScope.Company),
      (PermissionNames.Employees.Update, PermissionScope.Company));
    var (service, _) = ServiceForAllPermissions();

    // requested = employees.read (Department) + offices.read (Company):
    // one kept with a new scope, one removed, one added
    var dto = NewDto("HR",
      (PermissionNames.Employees.Read, PermissionScope.Department),
      (PermissionNames.Offices.Read, PermissionScope.Company));

    // Act
    var result = await service.UpdateAsync(role.Id, dto, CallerRoleId);

    // Assert
    Assert.True(result.IsSuccess);
    var permissions = await SavedPermissionsAsync(role.Id);
    Assert.Equal(2, permissions.Count);
    Assert.Equal(PermissionScope.Department, permissions[PermissionNames.Employees.Read]);
    Assert.Equal(PermissionScope.Company, permissions[PermissionNames.Offices.Read]);
    Assert.False(permissions.ContainsKey(PermissionNames.Employees.Update));
  }

  [Fact]
  public async Task UpdateAsync_Success_InvalidatesThatRolesCacheEntryOnce()
  {
    // Arrange
    var role = await SeedRoleAsync("HR");
    var (service, cache) = ServiceForAllPermissions();

    // Act
    await service.UpdateAsync(role.Id, NewDto("HR", (PermissionNames.Offices.Read, PermissionScope.Company)), CallerRoleId);

    // Assert: like Jest's expect(mock).toHaveBeenCalledTimes(1) with that id
    cache.Verify(c => c.Invalidate(role.Id), Times.Once);
  }

  [Fact]
  public async Task UpdateAsync_ValidationFails_ChangesNothingAndDoesNotInvalidate()
  {
    // Arrange
    var role = await SeedRoleAsync("HR", (PermissionNames.Employees.Read, PermissionScope.Company));
    var (service, cache) = ServiceForAllPermissions();

    // Act
    var result = await service.UpdateAsync(
      role.Id, NewDto("HR", ("employees.fly", PermissionScope.Company)), CallerRoleId);

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
    var permissions = await SavedPermissionsAsync(role.Id);
    Assert.Equal(PermissionScope.Company, Assert.Single(permissions).Value);
    cache.Verify(c => c.Invalidate(It.IsAny<int>()), Times.Never);
  }

  // ---------------- CloneAsync ----------------

  [Fact]
  public async Task CloneAsync_NormalRole_CopiesPermissionsScopesAndDescription()
  {
    // Arrange
    var source = await SeedRoleAsync("HR",
      (PermissionNames.Employees.Read, PermissionScope.Company),
      (PermissionNames.Employees.Update, PermissionScope.Department));
    source.Description = "Human resources";
    await _context.SaveChangesAsync();
    var (service, _) = ServiceForAllPermissions();

    // Act
    var result = await service.CloneAsync(source.Id, new RoleCloneDto { Name = "HR Payroll" }, CallerRoleId);

    // Assert
    Assert.True(result.IsSuccess);
    Assert.Equal("Human resources", result.Value!.Description);
    Assert.NotEqual(source.Id, result.Value.Id);

    var permissions = await SavedPermissionsAsync(result.Value.Id);
    Assert.Equal(2, permissions.Count);
    Assert.Equal(PermissionScope.Company, permissions[PermissionNames.Employees.Read]);
    Assert.Equal(PermissionScope.Department, permissions[PermissionNames.Employees.Update]);
  }

  [Fact]
  public async Task CloneAsync_DescriptionGiven_ReplacesTheSourceDescription()
  {
    // Arrange
    var source = await SeedRoleAsync("HR");
    source.Description = "Human resources";
    await _context.SaveChangesAsync();
    var (service, _) = ServiceForAllPermissions();

    // Act
    var result = await service.CloneAsync(
      source.Id, new RoleCloneDto { Name = "HR Payroll", Description = "Payroll only" }, CallerRoleId);

    // Assert
    Assert.Equal("Payroll only", result.Value!.Description);
  }

  [Fact]
  public async Task CloneAsync_SystemRole_CreatesANormalRoleWithEveryPermissionStored()
  {
    // Arrange
    var superAdmin = await SeedSystemRoleAsync();
    var (service, _) = ServiceForAllPermissions();

    // Act
    var result = await service.CloneAsync(superAdmin.Id, new RoleCloneDto { Name = "Almost Admin" }, CallerRoleId);

    // Assert: not a system role, and every permission is a real stored row
    Assert.True(result.IsSuccess);
    Assert.False(result.Value!.IsSystem);

    var permissions = await SavedPermissionsAsync(result.Value.Id);
    Assert.Equal(PermissionNames.All.Count, permissions.Count);
    Assert.All(permissions.Values, scope => Assert.Equal(PermissionScope.Company, scope));
  }

  [Fact]
  public async Task CloneAsync_CallerLacksOneOfTheSourcePermissions_ReturnsForbidden()
  {
    // Arrange: cloning must not be a back door around the escalation guard
    var source = await SeedRoleAsync("HR", (PermissionNames.Employees.Update, PermissionScope.Company));
    var (service, _) = ServiceFor((PermissionNames.Employees.Read, PermissionScope.Company));

    // Act
    var result = await service.CloneAsync(source.Id, new RoleCloneDto { Name = "HR Copy" }, CallerRoleId);

    // Assert
    Assert.Equal(ServiceErrorType.Forbidden, result.ErrorType);
  }

  [Fact]
  public async Task CloneAsync_SourceDoesNotExist_ReturnsNotFound()
  {
    // Arrange
    var (service, _) = ServiceForAllPermissions();

    // Act
    var result = await service.CloneAsync(123_456, new RoleCloneDto { Name = "Copy" }, CallerRoleId);

    // Assert
    Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
  }

  [Fact]
  public async Task CloneAsync_NewNameAlreadyTaken_ReturnsConflict()
  {
    // Arrange
    var source = await SeedRoleAsync("HR");
    await SeedRoleAsync("Payroll");
    var (service, _) = ServiceForAllPermissions();

    // Act
    var result = await service.CloneAsync(source.Id, new RoleCloneDto { Name = "payroll" }, CallerRoleId);

    // Assert
    Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
  }

  // ---------------- DeleteAsync ----------------

  [Fact]
  public async Task DeleteAsync_RoleDoesNotExist_ReturnsNotFound()
  {
    // Arrange
    var (service, _) = ServiceForAllPermissions();

    // Act
    var result = await service.DeleteAsync(123_456);

    // Assert
    Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
  }

  [Fact]
  public async Task DeleteAsync_SystemRole_ReturnsConflict()
  {
    // Arrange
    var superAdmin = await SeedSystemRoleAsync();
    var (service, _) = ServiceForAllPermissions();

    // Act
    var result = await service.DeleteAsync(superAdmin.Id);

    // Assert
    Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
  }

  [Fact]
  public async Task DeleteAsync_ActiveEmployeeHoldsTheRole_ReturnsConflictAndKeepsTheRole()
  {
    // Arrange
    var role = await SeedRoleAsync("HR");
    await SeedEmployeeAsync(role.Id);
    var (service, _) = ServiceForAllPermissions();

    // Act
    var result = await service.DeleteAsync(role.Id);

    // Assert
    Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
    Assert.Contains("1 employee", result.ErrorMessage);
    Assert.True(await _context.Roles.AnyAsync(r => r.Id == role.Id));
  }

  [Fact]
  public async Task DeleteAsync_OnlyFormerEmployeesHoldTheRole_MovesThemToEmployeeAndDeletes()
  {
    // Arrange
    var role = await SeedRoleAsync("HR");
    var former = await SeedEmployeeAsync(role.Id, deleted: true);
    var (service, _) = ServiceForAllPermissions();

    // Act
    var result = await service.DeleteAsync(role.Id);

    // Assert
    Assert.True(result.IsSuccess);
    Assert.False(await _context.Roles.AnyAsync(r => r.Id == role.Id));

    // IgnoreQueryFilters: the soft-delete filter would otherwise hide the former employee
    var reloaded = await _context.Employees
      .IgnoreQueryFilters()
      .AsNoTracking()
      .FirstAsync(e => e.Id == former.Id);
    Assert.Equal(_employeeRoleId, reloaded.RoleId);
  }

  [Fact]
  public async Task DeleteAsync_Success_InvalidatesThatRolesCacheEntryOnce()
  {
    // Arrange
    var role = await SeedRoleAsync("HR");
    var (service, cache) = ServiceForAllPermissions();

    // Act
    await service.DeleteAsync(role.Id);

    // Assert
    cache.Verify(c => c.Invalidate(role.Id), Times.Once);
  }

  // ---------------- reads ----------------

  [Fact]
  public async Task GetAllAsync_CountsOnlyActiveEmployees()
  {
    // Arrange
    var role = await SeedRoleAsync("HR");
    await SeedEmployeeAsync(role.Id);
    await SeedEmployeeAsync(role.Id, deleted: true);
    var (service, _) = ServiceForAllPermissions();

    // Act
    var roles = await service.GetAllAsync();

    // Assert
    Assert.Equal(1, roles.Single(r => r.Id == role.Id).EmployeeCount);
  }

  [Fact]
  public async Task GetByIdAsync_SystemRole_ShowsEveryPermissionCompanyWide()
  {
    // Arrange: no stored rows at all, like SuperAdmin after Step 6
    var superAdmin = await SeedSystemRoleAsync();
    var (service, _) = ServiceForAllPermissions();

    // Act
    var role = await service.GetByIdAsync(superAdmin.Id);

    // Assert
    Assert.NotNull(role);
    Assert.True(role.IsSystem);
    Assert.Equal(PermissionNames.All.Count, role.Permissions.Count);
    Assert.All(role.Permissions, p => Assert.Equal(PermissionScope.Company, p.Scope));
  }

  [Fact]
  public void GetPermissionCatalog_ListsEveryPermissionWithItsCompanyOnlyFlag()
  {
    // Act: static, so no service instance needed
    var catalog = RoleService.GetPermissionCatalog();

    // Assert
    Assert.Equal(PermissionNames.All.Count, catalog.Count);
    Assert.True(catalog.Single(p => p.Name == PermissionNames.Employees.Create).CompanyOnly);
    Assert.False(catalog.Single(p => p.Name == PermissionNames.Employees.Read).CompanyOnly);
  }

  [Fact]
  public void GetPermissionCatalog_FlagsCustomizablePermissions()
  {
    var catalog = RoleService.GetPermissionCatalog();

    Assert.True(catalog.Single(p => p.Name == PermissionNames.Employees.Read).Customizable);
    Assert.False(catalog.Single(p => p.Name == PermissionNames.Employees.Update).Customizable);
  }
}