using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Authorization;
using MyMIS.Api.DTOs;
using MyMIS.Api.IntegrationTests.Infrastructure;
using static MyMIS.Api.IntegrationTests.Infrastructure.TestData;

namespace MyMIS.Api.IntegrationTests;

// The Roles API over real HTTP and Postgres: the parts the unit tests can't prove,
// such as cache invalidation reaching an already-logged-in user.
[Collection(IntegrationCollection.Name)]
public class RolesApiTests(ApiFactory factory)
{
  // ---------------- helpers ----------------

  private static async Task AssertStatusAsync(HttpStatusCode expected, HttpResponseMessage response)
  {
    Assert.True(
      response.StatusCode == expected,
      $"Expected {(int)expected} but got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
  }

  // The JSON body for POST/PUT /api/Roles. Scopes travel as strings ("Company").
  private static object RoleBody(string name, params string[] companyWidePermissions) => new
  {
    name,
    permissions = companyWidePermissions.Select(permission => new { permission, scope = "Company" }).ToArray(),
  };

  // Creates a role through the API (as SuperAdmin) and returns it.
  private async Task<RoleResponseDto> CreateRoleAsync(string name, params string[] companyWidePermissions)
  {
    var admin = await factory.GetSuperAdminClientAsync();
    var response = await admin.PostAsJsonAsync("/api/Roles", RoleBody(name, companyWidePermissions));
    await AssertStatusAsync(HttpStatusCode.Created, response);
    return (await response.Content.ReadFromJsonAsync<RoleResponseDto>())!;
  }

  // A logged-in client for a new employee who holds `roleId`.
  private async Task<HttpClient> ClientWithRoleAsync(int roleId)
  {
    var employee = await factory.SeedWithoutPermissionsAsync();

    await factory.ExecuteDbAsync(async db =>
      await db.Employees
        .Where(e => e.Id == employee.Id)
        .ExecuteUpdateAsync(set => set.SetProperty(e => e.RoleId, roleId)));

    // Log in AFTER the update, so the token carries the new roleId
    var token = await factory.LoginAsync(employee.Username);
    var client = factory.CreateApiClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return client;
  }

  // ---------------- tests ----------------

  [Fact]
  public async Task UpdatingARole_TakesEffectForItsHoldersImmediately_WithoutLoggingInAgain()
  {
    // Arrange: a role that can't read offices, and someone already logged in with it
    var name = $"Live {Unique()}";
    var role = await CreateRoleAsync(name);
    var holder = await ClientWithRoleAsync(role.Id);

    // This first request also puts the role's (empty) permissions into the cache
    Assert.Equal(HttpStatusCode.Forbidden, (await holder.GetAsync("/api/Offices")).StatusCode);

    // Act: SuperAdmin adds offices.read to the role
    var admin = await factory.GetSuperAdminClientAsync();
    var updated = await admin.PutAsJsonAsync($"/api/Roles/{role.Id}", RoleBody(name, PermissionNames.Offices.Read));
    await AssertStatusAsync(HttpStatusCode.OK, updated);

    // Assert: the SAME token now passes. This proves the live link (permissions are read
    // through the role, not stored in the token) AND the cache invalidation (without
    // Invalidate, the cached empty list would keep returning 403 for up to 10 minutes).
    Assert.Equal(HttpStatusCode.OK, (await holder.GetAsync("/api/Offices")).StatusCode);
  }

  [Fact]
  public async Task CreateRole_GrantingAPermissionTheCallerLacks_Returns403WithTheReason()
  {
    // Arrange: can create roles and read employees, but NOT update them
    var client = await factory.GetClientAsync(PermissionNames.Roles.Create, PermissionNames.Employees.Read);

    // Act
    var response = await client.PostAsJsonAsync(
      "/api/Roles", RoleBody($"Escalate {Unique()}", PermissionNames.Employees.Update));

    // Assert: a 403 WITH a body (not the empty one from Forbid()), naming the permission
    await AssertStatusAsync(HttpStatusCode.Forbidden, response);
    Assert.Contains(PermissionNames.Employees.Update, await response.Content.ReadAsStringAsync());
  }

  [Fact]
  public async Task DeleteRole_StillHeldByAnActiveEmployee_Returns409()
  {
    // Arrange
    var role = await CreateRoleAsync($"Held {Unique()}");
    await ClientWithRoleAsync(role.Id);   // seeds an active employee holding it

    // Act
    var admin = await factory.GetSuperAdminClientAsync();
    var response = await admin.DeleteAsync($"/api/Roles/{role.Id}");

    // Assert
    await AssertStatusAsync(HttpStatusCode.Conflict, response);
    Assert.Contains("1 employee", await response.Content.ReadAsStringAsync());
  }

  [Fact]
  public async Task UpdateRole_WithoutThePermissionsField_Returns400AndKeepsThePermissions()
  {
    // Arrange: a role holding offices.read
    var name = $"Keep {Unique()}";
    var role = await CreateRoleAsync(name, PermissionNames.Offices.Read);
    var admin = await factory.GetSuperAdminClientAsync();

    // Act: a rename-only body. Must NOT be read as "replace with nothing".
    var response = await admin.PutAsJsonAsync($"/api/Roles/{role.Id}", new { name });

    // Assert: rejected by the nullable + [Required] DTO, and nothing was stripped
    await AssertStatusAsync(HttpStatusCode.BadRequest, response);

    var reloaded = await (await admin.GetAsync($"/api/Roles/{role.Id}"))
      .Content.ReadFromJsonAsync<RoleResponseDto>();
    Assert.Equal(PermissionNames.Offices.Read, Assert.Single(reloaded!.Permissions).Permission);
  }
}