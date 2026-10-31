using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using MyMIS.Api.Authorization;
using MyMIS.Api.Data;
using MyMIS.Api.Models;
using Xunit;

namespace MyMIS.Api.Tests.Authorization;

public class RolePermissionCacheTests : IDisposable
{
  private readonly ServiceProvider _provider;
  private readonly RolePermissionCache _cache;
  private readonly DateTime _now = DateTime.UtcNow;

  public RolePermissionCacheTests()
  {
    // Computed ONCE, outside the lambda below. The lambda runs every time a
    // DbContext is created; with Guid.NewGuid() inside it, every scope would get
    // a brand-new empty database and the cache would never see the seed data.
    var databaseName = Guid.NewGuid().ToString();

    // A mini DI container: like Program.cs's builder.Services, minus everything
    // the cache doesn't need. BuildServiceProvider() is what `builder.Build()`
    // does internally for the real app.
    _provider = new ServiceCollection()
      .AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName))
      .AddMemoryCache()
      .BuildServiceProvider();

    // The real class, given real collaborators from the mini container.
    _cache = new RolePermissionCache(
      _provider.GetRequiredService<IMemoryCache>(),
      _provider.GetRequiredService<IServiceScopeFactory>());
  }

  public void Dispose()
  {
    _provider.Dispose();
    GC.SuppressFinalize(this);
  }

  // Runs `seed` against the SAME in-memory database the cache reads from,
  // in its own scope, exactly like the cache does internally.
  // Func<AppDbContext, Task> = "a function that takes the db and returns a Task",
  // like (db) => Promise<void> in TypeScript.
  private async Task SeedAsync(Func<AppDbContext, Task> seed)
  {
    using var scope = _provider.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await seed(db);
    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task GetAsync_UnknownRoleId_ReturnsEmpty()
  {
    // Arrange + Act
    var result = await _cache.GetAsync(999);

    // Assert
    Assert.Empty(result);
  }

  [Fact]
  public async Task GetAsync_NormalRole_ReturnsOnlyItsPermissionsWithScopes()
  {
    // Arrange
    var read = new Permission { Name = "employees.read" };
    var update = new Permission { Name = "employees.update" };
    var assign = new Permission { Name = "roles.assign" };   // exists, but the role doesn't hold it

    var role = new Role
    {
      Name = "Manager",
      NormalizedName = "MANAGER",
      CreatedAt = _now,
      UpdatedAt = _now,
      RolePermissions =
      [
        new RolePermission { Permission = read, Scope = PermissionScope.Company },
        new RolePermission { Permission = update, Scope = PermissionScope.Department },
      ],
    };

    await SeedAsync(db =>
    {
      db.Permissions.Add(new Permission { Name = "employees.read" });
      db.Permissions.Add(new Permission { Name = "employees.update" });
      db.Permissions.Add(new Permission { Name = "roles.assign" });
      db.Roles.Add(role);
      return Task.CompletedTask;
    });

    // Act
    var result = await _cache.GetAsync(role.Id);

    // Assert: exactly the two it holds, each with its OWN scope
    Assert.Equal(2, result.Count);
    Assert.Equal(PermissionScope.Company, result["employees.read"]);
    Assert.Equal(PermissionScope.Department, result["employees.update"]);
    Assert.DoesNotContain("roles.assign", result.Keys);
  }

  [Fact]
  public async Task GetAsync_SystemRole_ReturnsEveryPermissionCompanyWide()
  {
    // Arrange
    var read = new Permission { Name = "employees.read" };
    var assign = new Permission { Name = "roles.assign" };

    var role = new Role
    {
      Name = "Manager",
      NormalizedName = "MANAGER",
      CreatedAt = _now,
      UpdatedAt = _now,
      IsSystem = true,
    };

    await SeedAsync(db =>
    {
      db.Permissions.AddRange(read, assign);
      db.Roles.Add(role);
      return Task.CompletedTask;
    });

    // Act
    var result = await _cache.GetAsync(role.Id);

    // Assert
    Assert.Equal(2, result.Count);
    Assert.Contains("employees.read", result.Keys);
    Assert.Contains("roles.assign", result.Keys);
    Assert.All(result.Values, scope => Assert.Equal(PermissionScope.Company, scope));
  }

  [Fact]
  public async Task GetAsync_CalledTwice_SecondCallServedFromCache()
  {
    // Arrange
    var read = new Permission { Name = "employees.read" };
    var update = new Permission { Name = "employees.update" };
    var role = new Role
    {
      Name = "Employee",
      NormalizedName = "EMPLOYEE",
      CreatedAt = _now,
      UpdatedAt = _now,
      RolePermissions = [
        new RolePermission { Permission = read, Scope = PermissionScope.Company }
      ]
    };
    await SeedAsync(db =>
    {
      db.Permissions.AddRange(read, update);
      db.Roles.Add(role);
      return Task.CompletedTask;
    });
    await _cache.GetAsync(role.Id);
    await SeedAsync(db =>
    {
      db.RolePermissions.Add(new RolePermission
      {
        RoleId = role.Id,
        PermissionId = update.Id,
        Scope = PermissionScope.Company,
      });
      return Task.CompletedTask;
    });

    // Act
    var result = await _cache.GetAsync(role.Id);

    // Assert
    Assert.Single(result);
    Assert.True(result.ContainsKey("employees.read"));
  }

  [Fact]
  public async Task Invalidate_ThenGetAsync_ReloadsFromDatabase()
  {
    // Arrange
    var read = new Permission { Name = "employees.read" };
    var update = new Permission { Name = "employees.update" };
    var role = new Role
    {
      Name = "Employee",
      NormalizedName = "EMPLOYEE",
      CreatedAt = _now,
      UpdatedAt = _now,
      RolePermissions = [
        new RolePermission { Permission = read, Scope = PermissionScope.Company }
      ]
    };
    await SeedAsync(db =>
    {
      db.Permissions.AddRange(read, update);
      db.Roles.Add(role);
      return Task.CompletedTask;
    });

    // First load fills the cache
    await _cache.GetAsync(role.Id);

    // Database changes after that load
    await SeedAsync(db =>
    {
      db.RolePermissions.Add(new RolePermission
      {
        RoleId = role.Id,
        PermissionId = update.Id,
        Scope = PermissionScope.Company,
      });
      return Task.CompletedTask;
    });

    // Clear this role's cache entry
    _cache.Invalidate(role.Id);

    // Act
    var result = await _cache.GetAsync(role.Id);

    // Assert
    Assert.Equal(2, result.Count);
    Assert.True(result.ContainsKey("employees.read"));
    Assert.True(result.ContainsKey("employees.update"));
  }
}