using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyMIS.Api.Data;
using MyMIS.Api.Helpers;
using MyMIS.Api.IntegrationTests.Infrastructure;
using MyMIS.Api.Models;

namespace MyMIS.Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public class SmokeTests(ApiFactory factory)
{
  private static object NewOffice(string suffix) => new
  {
    name = $"Smoke Office {suffix}",
    city = "Manila",
    countryCode = "PH",
  };

  [Fact]
  public async Task Migrations_OnAFreshDatabase_SeedThePermissionRows()
  {
    // Arrange
    using var scope = factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Act
    var names = await db.Permissions.Select(p => p.Name).ToListAsync();

    // Assert
    string[] expected =
    [
      "employees.create", "employees.update", "positions.create", "positions.update",
      "locations.manage", "offices.manage", "hmo.manage",
    ];

    foreach (var permission in expected)
    {
      Assert.Contains(permission, names);
    }
  }

  [Fact]
  public async Task Login_WithAWrongPassword_Returns401()
  {
    // Arrange
    var employee = await factory.SeedEmployeeAsync();
    using var client = factory.CreateApiClient();

    // Act
    var response = await client.PostAsJsonAsync(
      "/api/Auth/login", new { username = employee.Username, password = "not-the-password" });

    // Assert
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task PostOffice_WithoutAToken_Returns401()
  {
    // Arrange
    using var client = factory.CreateApiClient();

    // Act
    var response = await client.PostAsJsonAsync("/api/Offices", NewOffice(Guid.NewGuid().ToString("N")[..8]));

    // Assert
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task PostOffice_LoggedInWithoutThePermission_Returns403()
  {
    // Arrange
    using var client = await factory.CreateAuthorizedClientAsync(Role.User);

    // Act
    var response = await client.PostAsJsonAsync("/api/Offices", NewOffice(Guid.NewGuid().ToString("N")[..8]));

    // Assert
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task PostOffice_WithTheOfficesManagePermission_CreatesTheOfficeInPostgres()
  {
    // Arrange
    var suffix = Guid.NewGuid().ToString("N")[..8];
    using var client = await factory.CreateAuthorizedClientAsync(Role.User, "offices.manage");

    // Act
    var response = await client.PostAsJsonAsync("/api/Offices", NewOffice(suffix));

    // Assert: the request got through the policy, and the row really exists in the database
    Assert.True(response.IsSuccessStatusCode, $"Expected success but got {(int)response.StatusCode}.");

    using var scope = factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    Assert.True(await db.Offices.AnyAsync(o => o.Name == $"Smoke Office {suffix}"));
  }

  [Fact]
  public async Task PostOffice_AsSuperAdminWithoutAnyPermission_IsAllowed()
  {
    // Arrange: SuperAdmin bypasses every permission check
    using var client = await factory.CreateAuthorizedClientAsync(Role.SuperAdmin);

    // Act
    var response = await client.PostAsJsonAsync("/api/Offices", NewOffice(Guid.NewGuid().ToString("N")[..8]));

    // Assert
    Assert.True(response.IsSuccessStatusCode, $"Expected success but got {(int)response.StatusCode}.");
  }

  [Fact]
  public async Task Offices_TwoNamesThatNormalizeTheSame_AreRejectedByThePostgresIndex()
  {
    // Arrange: the kind of test InMemory could never do. It enforces no indexes at all.
    var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    using var scope = factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    db.Offices.Add(new Office { Name = $"Acme {suffix}", NormalizedName = $"ACME {suffix}", City = "Manila", CountryCode = "PH" });
    await db.SaveChangesAsync();

    // Act
    db.Offices.Add(new Office { Name = $"ACME {suffix}", NormalizedName = $"ACME {suffix}", City = "Cebu", CountryCode = "PH" });
    var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

    // Assert
    Assert.Equal("IX_Offices_NormalizedName", exception.GetUniqueViolationConstraint());
  }
}