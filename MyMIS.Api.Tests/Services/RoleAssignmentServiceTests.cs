using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Authorization;
using MyMIS.Api.Data;
using MyMIS.Api.Helpers;
using MyMIS.Api.Models;
using MyMIS.Api.Services;
using Xunit;

namespace MyMIS.Api.Tests.Services;

public class RoleAssignmentServiceTests : IDisposable
{
  // ---------------- a fake cache ----------------

  // A FAKE (a simple working stand-in) rather than a MOCK (a programmed object
  // you set expectations on). The service asks about several role ids, and a
  // plain dictionary of "role id → permissions" reads more clearly than a
  // Moq setup per id. Like passing a hand-written { getAsync, invalidate }
  // object in JS tests instead of jest.fn() for each call.
  private sealed class FakeRolePermissionCache : IRolePermissionCache
  {
    private readonly Dictionary<int, Dictionary<string, PermissionScope>> _byRole = new();

    public void Set(int roleId, params (string Name, PermissionScope Scope)[] permissions) =>
      _byRole[roleId] = permissions.ToDictionary(p => p.Name, p => p.Scope);

    public Task<IReadOnlyDictionary<string, PermissionScope>> GetAsync(int roleId) =>
      Task.FromResult<IReadOnlyDictionary<string, PermissionScope>>(
        _byRole.TryGetValue(roleId, out var permissions) ? permissions : new Dictionary<string, PermissionScope>());

    public void Invalidate(int roleId)
    {
      // Assignment never edits a role's permissions, so nothing to do.
    }
  }

  // ---------------- setup ----------------

  // An employee id that isn't any seeded employee, so "self" only happens when a test asks for it.
  private const int CallerEmployeeId = 777_777;

  private readonly AppDbContext _context;
  private readonly FakeRolePermissionCache _cache = new();
  private readonly RoleAssignmentService _service;
  private readonly DateTime _now = DateTime.UtcNow;

  private readonly Role _superAdmin;
  private readonly Role _root;       // a second system role, only to reach the "last holder" rule
  private readonly Role _employee;
  private readonly Role _hr;
  private readonly Role _manager;
  private readonly Role _auditor;

  public RoleAssignmentServiceTests()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
      .UseInMemoryDatabase(Guid.NewGuid().ToString())
      .Options;

    _context = new AppDbContext(options);

    _superAdmin = NewRole("SuperAdmin", isSystem: true);
    _root = NewRole("Root", isSystem: true);
    _employee = NewRole("Employee");
    _hr = NewRole("HR");
    _manager = NewRole("Manager");
    _auditor = NewRole("Auditor");

    _context.Roles.AddRange(_superAdmin, _root, _employee, _hr, _manager, _auditor);
    _context.SaveChanges();   // sync: constructors can't await. Ids are assigned here.

    // What each role can do, as the real cache would report it.
    var everything = PermissionNames.All.Select(name => (name, PermissionScope.Company)).ToArray();
    _cache.Set(_superAdmin.Id, everything);
    _cache.Set(_root.Id, everything);
    _cache.Set(_employee.Id, (PermissionNames.Departments.Read, PermissionScope.Company));
    _cache.Set(_hr.Id,
      (PermissionNames.Departments.Read, PermissionScope.Company),
      (PermissionNames.Employees.Update, PermissionScope.Company),
      (PermissionNames.Employees.WorkDetailsUpdate, PermissionScope.Company),
      (PermissionNames.Roles.Read, PermissionScope.Company),
      (PermissionNames.Roles.Assign, PermissionScope.Company));
    _cache.Set(_manager.Id, (PermissionNames.Employees.WorkDetailsUpdate, PermissionScope.Department));
    // Auditor holds employees.read, which HR does NOT, so HR can't hand it out.
    _cache.Set(_auditor.Id,
      (PermissionNames.Employees.Read, PermissionScope.Company),
      (PermissionNames.Roles.Read, PermissionScope.Company));

    _service = new RoleAssignmentService(_context, _cache);
  }

  public void Dispose()
  {
    _context.Dispose();
    GC.SuppressFinalize(this);
  }

  // ---------------- helpers ----------------

  private Role NewRole(string name, bool isSystem = false) => new()
  {
    Name = name,
    NormalizedName = name.ToUpperInvariant(),
    IsSystem = isSystem,
    CreatedAt = _now,
    UpdatedAt = _now,
  };

  private async Task<Employee> SeedEmployeeAsync(Role role, bool deleted = false)
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
      RoleId = role.Id,
      DeletedAt = deleted ? _now : null,
    };

    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();
    return employee;
  }

  private async Task<RefreshToken> SeedRefreshTokenAsync(int employeeId, DateTime? revokedAt = null)
  {
    var token = new RefreshToken
    {
      TokenHash = Guid.NewGuid().ToString("N"),
      EmployeeId = employeeId,
      ExpiresAt = _now.AddDays(7),
      CreatedAt = _now,
      RevokedAt = revokedAt,
    };

    _context.RefreshTokens.Add(token);
    await _context.SaveChangesAsync();
    return token;
  }

  private Task<RefreshToken> SavedTokenAsync(int id) =>
    _context.RefreshTokens.AsNoTracking().FirstAsync(t => t.Id == id);

  private Task<Employee> SavedEmployeeAsync(int id) =>
    _context.Employees.AsNoTracking().FirstAsync(e => e.Id == id);

  // ---------------- GetAsync ----------------

  [Fact]
  public async Task GetAsync_ExistingEmployee_ReturnsTheirRoleIdAndName()
  {
    // Arrange
    var employee = await SeedEmployeeAsync(_hr);

    // Act
    var role = await _service.GetAsync(employee.Id);

    // Assert
    Assert.NotNull(role);
    Assert.Equal(_hr.Id, role.RoleId);
    Assert.Equal("HR", role.RoleName);
  }

  [Fact]
  public async Task GetAsync_UnknownEmployee_ReturnsNull()
  {
    // Act
    var role = await _service.GetAsync(123_456);

    // Assert
    Assert.Null(role);
  }

  [Fact]
  public async Task GetAsync_SoftDeletedEmployee_ReturnsNull()
  {
    // Arrange: the soft-delete query filter hides former employees
    var former = await SeedEmployeeAsync(_hr, deleted: true);

    // Act
    var role = await _service.GetAsync(former.Id);

    // Assert
    Assert.Null(role);
  }

  // ---------------- CheckCanAssignAsync (shared with employee create) ----------------

  [Fact]
  public async Task CheckCanAssignAsync_RoleDoesNotExist_ReturnsValidation()
  {
    // Act
    var result = await _service.CheckCanAssignAsync(123_456, _hr.Id);

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
  }

  [Fact]
  public async Task CheckCanAssignAsync_SystemRoleByANonSystemCaller_ReturnsForbidden()
  {
    // Act: HR tries to hand out SuperAdmin
    var result = await _service.CheckCanAssignAsync(_superAdmin.Id, _hr.Id);

    // Assert
    Assert.Equal(ServiceErrorType.Forbidden, result.ErrorType);
  }

  [Fact]
  public async Task CheckCanAssignAsync_RoleHoldsAPermissionTheCallerLacks_ReturnsForbidden()
  {
    // Act: Auditor includes employees.read, which HR doesn't hold
    var result = await _service.CheckCanAssignAsync(_auditor.Id, _hr.Id);

    // Assert
    Assert.Equal(ServiceErrorType.Forbidden, result.ErrorType);
    Assert.Contains(PermissionNames.Employees.Read, result.ErrorMessage);
  }

  [Fact]
  public async Task CheckCanAssignAsync_CallerCompanyScopeCoversADepartmentScopedRole_Succeeds()
  {
    // Act: HR (work-details company-wide) assigns Manager (work-details department-scoped)
    var result = await _service.CheckCanAssignAsync(_manager.Id, _hr.Id);

    // Assert
    Assert.True(result.IsSuccess);
    Assert.Equal(_manager.Id, result.Value!.Id);
  }

  [Fact]
  public async Task CheckCanAssignAsync_SystemCallerAssignsASystemRole_Succeeds()
  {
    // Act
    var result = await _service.CheckCanAssignAsync(_superAdmin.Id, _superAdmin.Id);

    // Assert
    Assert.True(result.IsSuccess);
  }

  // ---------------- AssignAsync ----------------

  [Fact]
  public async Task AssignAsync_TargetDoesNotExist_ReturnsNotFound()
  {
    // Act
    var result = await _service.AssignAsync(123_456, _manager.Id, CallerEmployeeId, _hr.Id);

    // Assert
    Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
  }

  [Fact]
  public async Task AssignAsync_OwnRole_ReturnsForbiddenAndChangesNothing()
  {
    // Arrange: the caller IS the target (A1), even as a SuperAdmin
    var self = await SeedEmployeeAsync(_superAdmin);

    // Act
    var result = await _service.AssignAsync(self.Id, _employee.Id, callerEmployeeId: self.Id, _superAdmin.Id);

    // Assert
    Assert.Equal(ServiceErrorType.Forbidden, result.ErrorType);
    Assert.Equal(_superAdmin.Id, (await SavedEmployeeAsync(self.Id)).RoleId);
  }

  [Fact]
  public async Task AssignAsync_SameRoleAsNow_SucceedsWithoutRevokingTokens()
  {
    // Arrange: even a SuperAdmin target, so the no-op runs BEFORE the SuperAdmin rule
    var target = await SeedEmployeeAsync(_superAdmin);
    var token = await SeedRefreshTokenAsync(target.Id);

    // Act: HR "re-saves" the unchanged role
    var result = await _service.AssignAsync(target.Id, _superAdmin.Id, CallerEmployeeId, _hr.Id);

    // Assert
    Assert.True(result.IsSuccess);
    Assert.Null((await SavedTokenAsync(token.Id)).RevokedAt);
  }

  [Fact]
  public async Task AssignAsync_NonSystemCallerMovesASuperAdmin_ReturnsForbidden()
  {
    // Arrange (A4): HR may move anyone EXCEPT out of SuperAdmin
    await SeedEmployeeAsync(_superAdmin);   // a second SuperAdmin, so "last holder" isn't the reason
    var target = await SeedEmployeeAsync(_superAdmin);

    // Act
    var result = await _service.AssignAsync(target.Id, _employee.Id, CallerEmployeeId, _hr.Id);

    // Assert
    Assert.Equal(ServiceErrorType.Forbidden, result.ErrorType);
    Assert.Equal(_superAdmin.Id, (await SavedEmployeeAsync(target.Id)).RoleId);
  }

  [Fact]
  public async Task AssignAsync_NewRoleDoesNotExist_ReturnsValidation()
  {
    // Arrange
    var target = await SeedEmployeeAsync(_employee);

    // Act
    var result = await _service.AssignAsync(target.Id, 123_456, CallerEmployeeId, _hr.Id);

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
  }

  [Fact]
  public async Task AssignAsync_NewRoleExceedsTheCaller_ReturnsForbiddenAndChangesNothing()
  {
    // Arrange
    var target = await SeedEmployeeAsync(_employee);
    var token = await SeedRefreshTokenAsync(target.Id);

    // Act: HR tries to make someone an Auditor (employees.read, which HR lacks)
    var result = await _service.AssignAsync(target.Id, _auditor.Id, CallerEmployeeId, _hr.Id);

    // Assert: nothing saved, nothing revoked
    Assert.Equal(ServiceErrorType.Forbidden, result.ErrorType);
    Assert.Equal(_employee.Id, (await SavedEmployeeAsync(target.Id)).RoleId);
    Assert.Null((await SavedTokenAsync(token.Id)).RevokedAt);
  }

  [Fact]
  public async Task AssignAsync_Success_ChangesTheRoleAndRevokesOnlyTheTargetsActiveTokens()
  {
    // Arrange
    var target = await SeedEmployeeAsync(_employee);
    var bystander = await SeedEmployeeAsync(_employee);

    var activeToken = await SeedRefreshTokenAsync(target.Id);
    var alreadyRevokedAt = _now.AddHours(-1);
    var oldToken = await SeedRefreshTokenAsync(target.Id, revokedAt: alreadyRevokedAt);
    var bystanderToken = await SeedRefreshTokenAsync(bystander.Id);

    // Act: HR makes the target a Manager
    var result = await _service.AssignAsync(target.Id, _manager.Id, CallerEmployeeId, _hr.Id);

    // Assert: the response and the saved role
    Assert.True(result.IsSuccess);
    Assert.Equal(_manager.Id, result.Value!.RoleId);
    Assert.Equal("Manager", result.Value.RoleName);
    Assert.Equal(_manager.Id, (await SavedEmployeeAsync(target.Id)).RoleId);

    // Assert: the target's active token is now revoked...
    Assert.NotNull((await SavedTokenAsync(activeToken.Id)).RevokedAt);
    // ...an already-revoked one keeps its ORIGINAL time (not overwritten)...
    Assert.Equal(alreadyRevokedAt, (await SavedTokenAsync(oldToken.Id)).RevokedAt);
    // ...and nobody else's session is touched.
    Assert.Null((await SavedTokenAsync(bystanderToken.Id)).RevokedAt);
  }

  [Fact]
  public async Task AssignAsync_LastActiveHolderOfASystemRole_ReturnsConflict()
  {
    // Arrange: the ONLY SuperAdmin, moved by a caller from a DIFFERENT system role.
    // (With one system role this can't happen, because the caller would have to be
    // another SuperAdmin. The Root role exists only to reach this safety net.)
    var onlySuperAdmin = await SeedEmployeeAsync(_superAdmin);

    // Act
    var result = await _service.AssignAsync(onlySuperAdmin.Id, _employee.Id, CallerEmployeeId, _root.Id);

    // Assert
    Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
    Assert.Equal(_superAdmin.Id, (await SavedEmployeeAsync(onlySuperAdmin.Id)).RoleId);
  }

  [Fact]
  public async Task AssignAsync_SuperAdminMovesAnotherSuperAdmin_Succeeds()
  {
    // Arrange: two SuperAdmins, so the target isn't the last one
    await SeedEmployeeAsync(_superAdmin);
    var target = await SeedEmployeeAsync(_superAdmin);

    // Act
    var result = await _service.AssignAsync(target.Id, _hr.Id, CallerEmployeeId, _superAdmin.Id);

    // Assert
    Assert.True(result.IsSuccess);
    Assert.Equal(_hr.Id, (await SavedEmployeeAsync(target.Id)).RoleId);
  }
}