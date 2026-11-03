using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MyMIS.Api.Authorization;
using MyMIS.Api.DTOs;
using MyMIS.Api.IntegrationTests.Infrastructure;
using static MyMIS.Api.IntegrationTests.Infrastructure.TestData;

namespace MyMIS.Api.IntegrationTests;

// Assigning roles over real HTTP and Postgres: token revocation and the effect
// on the target's next login, which the unit tests can't show.
[Collection(IntegrationCollection.Name)]
public class EmployeeRolesApiTests(ApiFactory factory)
{
  // ---------------- helpers ----------------

  private static async Task AssertStatusAsync(HttpStatusCode expected, HttpResponseMessage response)
  {
    Assert.True(
      response.StatusCode == expected,
      $"Expected {(int)expected} but got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
  }

  // Logs in and returns BOTH tokens (factory.LoginAsync only returns the access token).
  private async Task<AuthResponseDto> LoginForTokensAsync(string username)
  {
    using var client = factory.CreateApiClient();
    var response = await client.PostAsJsonAsync("/api/Auth/login", new { username, password = ApiFactory.DefaultPassword });
    await AssertStatusAsync(HttpStatusCode.OK, response);
    return (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;
  }

  private HttpClient ClientWithToken(string accessToken)
  {
    var client = factory.CreateApiClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    return client;
  }

  // Creates a role (as SuperAdmin) holding these permissions company-wide.
  private async Task<RoleResponseDto> CreateRoleAsync(params string[] permissions)
  {
    var admin = await factory.GetSuperAdminClientAsync();
    var response = await admin.PostAsJsonAsync("/api/Roles", new
    {
      name = $"Assign {Unique()}",
      permissions = permissions.Select(permission => new { permission, scope = "Company" }).ToArray(),
    });
    await AssertStatusAsync(HttpStatusCode.Created, response);
    return (await response.Content.ReadFromJsonAsync<RoleResponseDto>())!;
  }

  // ---------------- tests ----------------

  [Fact]
  public async Task AssigningARole_RevokesTheTargetsRefreshTokens()
  {
    // Arrange: the target is logged in somewhere, holding a refresh token
    var target = await factory.SeedEmployeeAsync();
    var tokens = await LoginForTokensAsync(target.Username);
    var role = await CreateRoleAsync(PermissionNames.HmoProviders.Read);

    // Act: SuperAdmin changes their role
    var admin = await factory.GetSuperAdminClientAsync();
    var assign = await admin.PutAsJsonAsync($"/api/Employees/{target.Id}/role", new { roleId = role.Id });
    await AssertStatusAsync(HttpStatusCode.OK, assign);

    // Assert: that refresh token no longer works (decision 8), so the next access
    // token they get can only come from a fresh login, carrying the NEW roleId
    using var anonymous = factory.CreateApiClient();
    var refresh = await anonymous.PostAsJsonAsync("/api/Auth/refresh", new { refreshToken = tokens.RefreshToken });
    Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
  }

  [Fact]
  public async Task AfterAssignment_ANewLogin_GetsTheNewRolesPermissions()
  {
    // Arrange: an ordinary employee can't read HMO providers
    var target = await factory.SeedEmployeeAsync();
    var before = ClientWithToken((await LoginForTokensAsync(target.Username)).AccessToken);
    Assert.Equal(HttpStatusCode.Forbidden, (await before.GetAsync("/api/HmoProviders")).StatusCode);

    var role = await CreateRoleAsync(PermissionNames.HmoProviders.Read);
    var admin = await factory.GetSuperAdminClientAsync();
    await AssertStatusAsync(
      HttpStatusCode.OK,
      await admin.PutAsJsonAsync($"/api/Employees/{target.Id}/role", new { roleId = role.Id }));

    // Act: they log in again
    var after = ClientWithToken((await LoginForTokensAsync(target.Username)).AccessToken);

    // Assert: the new token carries the new roleId, and the new role's permission works
    Assert.Equal(HttpStatusCode.OK, (await after.GetAsync("/api/HmoProviders")).StatusCode);
  }

  [Fact]
  public async Task ChangingYourOwnRole_Returns403()
  {
    // Arrange: a SuperAdmin, logged in as THEMSELVES
    var self = await factory.SeedSuperAdminAsync();
    var client = ClientWithToken((await LoginForTokensAsync(self.Username)).AccessToken);
    var role = await CreateRoleAsync(PermissionNames.Offices.Read);

    // Act
    var response = await client.PutAsJsonAsync($"/api/Employees/{self.Id}/role", new { roleId = role.Id });

    // Assert (A1): even SuperAdmin can't change their own role
    await AssertStatusAsync(HttpStatusCode.Forbidden, response);
  }

  [Fact]
  public async Task NonSuperAdmin_MovingASuperAdmin_Returns403()
  {
    // Arrange (A4): an HR-style caller who can assign roles
    var hr = await factory.GetClientAsync(PermissionNames.Roles.Assign, PermissionNames.Roles.Read);
    var superAdmin = await factory.SeedSuperAdminAsync();
    var employeeRoleId = await factory.QueryDbAsync(db => db.GetRoleIdAsync(SystemRoleNames.Employee));

    // Act
    var response = await hr.PutAsJsonAsync($"/api/Employees/{superAdmin.Id}/role", new { roleId = employeeRoleId });

    // Assert
    await AssertStatusAsync(HttpStatusCode.Forbidden, response);
  }
}