using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using MyMIS.Api.IntegrationTests.Infrastructure;
using MyMIS.Api.Models;

namespace MyMIS.Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public class AuthorizationWiringTests(ApiFactory factory)
{
  // An id that no row has, so a request that gets through authorization ends in 404
  // (or 415 for a missing body) and never changes any data.
  private const int Missing = 999_999;

  // ---------------- the tables: (HTTP method, URL, permission the controller requires) ----------------

  public static TheoryData<string, string, string> PermissionGated => new()
  {
    { "POST",   "/api/Offices", "offices.manage" },
    { "PUT",    $"/api/Offices/{Missing}", "offices.manage" },
    { "DELETE", $"/api/Offices/{Missing}", "offices.manage" },

    { "GET",  "/api/HmoProviders", "hmo.manage" },
    { "GET",  $"/api/HmoProviders/{Missing}", "hmo.manage" },
    { "POST", "/api/HmoProviders", "hmo.manage" },
    { "PUT",  $"/api/HmoProviders/{Missing}", "hmo.manage" },
    { "GET",  $"/api/HmoPlans?providerId={Missing}", "hmo.manage" },
    { "GET",  $"/api/HmoPlans/{Missing}", "hmo.manage" },
    { "POST", "/api/HmoPlans", "hmo.manage" },
    { "PUT",  $"/api/HmoPlans/{Missing}", "hmo.manage" },

    { "POST",   "/api/Regions", "locations.manage" },
    { "PUT",    $"/api/Regions/{Missing}", "locations.manage" },
    { "DELETE", $"/api/Regions/{Missing}", "locations.manage" },
    { "POST",   "/api/Regions/bulk", "locations.manage" },
    { "POST",   "/api/Cities", "locations.manage" },
    { "PUT",    $"/api/Cities/{Missing}", "locations.manage" },
    { "DELETE", $"/api/Cities/{Missing}", "locations.manage" },
    { "POST",   $"/api/Cities/bulk?regionId={Missing}", "locations.manage" },
    { "POST",   "/api/Barangays", "locations.manage" },
    { "PUT",    $"/api/Barangays/{Missing}", "locations.manage" },
    { "DELETE", $"/api/Barangays/{Missing}", "locations.manage" },
    { "POST",   $"/api/Barangays/bulk?cityId={Missing}", "locations.manage" },

    // Positions also run a second, manual SameDepartment check inside the action.
    // These rows only cover the first gate (the permission).
    { "POST", "/api/Positions", "positions.create" },
    { "PUT",  $"/api/Positions/{Missing}", "positions.update" },

    { "POST", "/api/Employees", "employees.create" },
    { "GET",  "/api/Employees/availability?username=nobody", "employees.create" },
    { "PUT",  $"/api/Employees/{Missing}", "employees.update" },

    { "GET",    $"/api/Employees/{Missing}/phones", "employees.update" },
    { "GET",    $"/api/Employees/{Missing}/phones/1", "employees.update" },
    { "POST",   $"/api/Employees/{Missing}/phones", "employees.update" },
    { "PUT",    $"/api/Employees/{Missing}/phones/1", "employees.update" },
    { "DELETE", $"/api/Employees/{Missing}/phones/1", "employees.update" },

    { "GET",    $"/api/Employees/{Missing}/emergency-contacts", "employees.update" },
    { "POST",   $"/api/Employees/{Missing}/emergency-contacts", "employees.update" },
    { "PUT",    $"/api/Employees/{Missing}/emergency-contacts/1", "employees.update" },
    { "DELETE", $"/api/Employees/{Missing}/emergency-contacts/1", "employees.update" },

    { "GET", $"/api/Employees/{Missing}/emails", "employees.update" },
    { "GET", $"/api/Employees/{Missing}/emails/1", "employees.update" },
    { "GET", $"/api/Employees/{Missing}/addresses", "employees.update" },
  };

  // Routes that only need a login (a plain [Authorize]).
  public static TheoryData<string, string> LoginOnly => new()
  {
    { "GET", "/api/Offices" },
    { "GET", $"/api/Offices/{Missing}" },
    { "GET", "/api/Regions" },
    { "GET", $"/api/Regions/{Missing}" },
    { "GET", "/api/Cities" },
    { "GET", $"/api/Cities/{Missing}" },
    { "GET", "/api/Barangays" },
    { "GET", $"/api/Barangays/{Missing}" },
    { "GET", "/api/Positions" },
    { "GET", $"/api/Positions/{Missing}" },
    { "GET", "/api/Departments" },
    { "GET", $"/api/Departments/{Missing}" },
    { "GET", "/api/Employees/me/phones" },
    { "GET", "/api/Employees/me/emails" },
    { "GET", "/api/Employees/me/addresses" },
    { "GET", "/api/Employees/me/emergency-contacts" },
    { "POST", "/api/Auth/change-password" },
  };

  // Routes gated by [Authorize(Roles = "...")]: one row per role,
  // with whether that role is allowed through.
  public static TheoryData<string, string, Role, bool> RoleGated
  {
    get
    {
      (string Method, string Url, Role[] Allowed)[] endpoints =
      [
        ("POST",   "/api/Departments", new[] { Role.Admin, Role.SuperAdmin }),
        ("PUT",    $"/api/Departments/{Missing}", new[] { Role.Admin, Role.SuperAdmin }),
        ("DELETE", $"/api/Departments/{Missing}", new[] { Role.SuperAdmin }),
        ("DELETE", $"/api/Positions/{Missing}", new[] { Role.SuperAdmin }),
        ("PUT",    $"/api/Employees/partial/{Missing}", new[] { Role.Admin, Role.SuperAdmin }),
      ];

      var data = new TheoryData<string, string, Role, bool>();

      foreach (var (method, url, allowed) in endpoints)
      {
        foreach (var role in Enum.GetValues<Role>())
        {
          data.Add(method, url, role, allowed.Contains(role));
        }
      }

      return data;
    }
  }

  // ---------------- helpers ----------------

  private static async Task<HttpStatusCode> SendAsync(HttpClient client, string method, string url)
  {
    // No body on purpose: authorization runs before the body is read.
    using var request = new HttpRequestMessage(new HttpMethod(method), url);
    using var response = await client.SendAsync(request);
    return response.StatusCode;
  }

  private static bool RejectedByAuth(HttpStatusCode status) =>
    status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;

  private static string Describe(string method, string url, HttpStatusCode status) =>
    $"{method} {url} returned {(int)status}";

  // ---------------- permission-gated routes ----------------

  [Theory]
  [MemberData(nameof(PermissionGated))]
  public async Task PermissionGated_WithoutAToken_Returns401(string method, string url, string permission)
  {
    // Arrange
    using var client = factory.CreateApiClient();

    // Act
    var status = await SendAsync(client, method, url);

    // Assert
    Assert.True(
      status == HttpStatusCode.Unauthorized,
      $"{Describe(method, url, status)}, expected 401 (route needs {permission}).");
  }

  [Theory]
  [MemberData(nameof(PermissionGated))]
  public async Task PermissionGated_LoggedInWithNoPermissions_Returns403(string method, string url, string permission)
  {
    // Arrange
    var client = await factory.GetClientAsync(Role.User);

    // Act
    var status = await SendAsync(client, method, url);

    // Assert
    Assert.True(
      status == HttpStatusCode.Forbidden,
      $"{Describe(method, url, status)}, expected 403 (route needs {permission}).");
  }

  [Theory]
  [MemberData(nameof(PermissionGated))]
  public async Task PermissionGated_WithOnlyTheRequiredPermission_IsNotRejected(string method, string url, string permission)
  {
    // Arrange: a plain User holding just this one permission
    var client = await factory.GetClientAsync(Role.User, permission);

    // Act
    var status = await SendAsync(client, method, url);

    // Assert: it can still be 400, 404 or 415 further in, just never an auth rejection
    Assert.False(
      RejectedByAuth(status),
      $"{Describe(method, url, status)} even though the user holds {permission}.");
  }

  [Theory]
  [MemberData(nameof(PermissionGated))]
  public async Task PermissionGated_WithEveryOtherPermission_Returns403(string method, string url, string permission)
  {
    // Arrange: all permissions except the one this route needs
    var others = TestPermissions.All.Where(p => p != permission).ToArray();
    var client = await factory.GetClientAsync(Role.User, others);

    // Act
    var status = await SendAsync(client, method, url);

    // Assert
    Assert.True(
      status == HttpStatusCode.Forbidden,
      $"{Describe(method, url, status)}, expected 403 for a user without {permission}.");
  }

  [Theory]
  [MemberData(nameof(PermissionGated))]
  public async Task PermissionGated_AsSuperAdmin_IsNotRejected(string method, string url, string permission)
  {
    // Arrange: SuperAdmin holds no permissions at all, but bypasses the check
    var client = await factory.GetClientAsync(Role.SuperAdmin);

    // Act
    var status = await SendAsync(client, method, url);

    // Assert
    Assert.False(
      RejectedByAuth(status),
      $"{Describe(method, url, status)} for a SuperAdmin (route needs {permission}).");
  }

  // ---------------- login-only routes ----------------

  [Theory]
  [MemberData(nameof(LoginOnly))]
  public async Task LoginOnly_WithoutAToken_Returns401(string method, string url)
  {
    // Arrange
    using var client = factory.CreateApiClient();

    // Act
    var status = await SendAsync(client, method, url);

    // Assert
    Assert.True(status == HttpStatusCode.Unauthorized, $"{Describe(method, url, status)}, expected 401.");
  }

  [Theory]
  [MemberData(nameof(LoginOnly))]
  public async Task LoginOnly_LoggedInWithNoPermissions_IsNotRejected(string method, string url)
  {
    // Arrange
    var client = await factory.GetClientAsync(Role.User);

    // Act
    var status = await SendAsync(client, method, url);

    // Assert
    Assert.False(RejectedByAuth(status), $"{Describe(method, url, status)} for a logged-in plain User.");
  }

  // ---------------- role-gated routes ----------------

  [Theory]
  [MemberData(nameof(RoleGated))]
  public async Task RoleGated_OnlyTheListedRolesGetThrough(string method, string url, Role role, bool allowed)
  {
    // Arrange
    var client = await factory.GetClientAsync(role);

    // Act
    var status = await SendAsync(client, method, url);

    // Assert
    if (allowed)
    {
      Assert.False(RejectedByAuth(status), $"{Describe(method, url, status)} for role {role}, which should be allowed.");
    }
    else
    {
      Assert.True(
        status == HttpStatusCode.Forbidden,
        $"{Describe(method, url, status)} for role {role}, expected 403.");
    }
  }

  // ---------------- inventory ----------------

  [Fact]
  public void EveryControllerEndpoint_RequiresLogin_ExceptTheKnownPublicOnes()
  {
    // Arrange: ask the app for its own list of routes, and find the ones with no [Authorize] at all
    var provider = factory.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>();

    // Act
    var open = provider.ApiDescriptionGroups.Items
      .SelectMany(group => group.Items)
      .Where(api => !(api.ActionDescriptor.EndpointMetadata ?? new List<object>())
        .OfType<IAuthorizeData>()
        .Any())
      .Select(api => $"{api.HttpMethod} {api.RelativePath}")
      .OrderBy(endpoint => endpoint, StringComparer.Ordinal)
      .ToList();

    // Assert: login, token refresh and logout must be reachable without a token. The thumbnail
    // callback has no [Authorize] either, because the Lambda proves itself with a secret header instead.
    Assert.Equal(
      new[]
      {
        "PATCH api/Employees/{id}/avatar-thumbnail",
        "POST api/Auth/login",
        "POST api/Auth/logout",
        "POST api/Auth/refresh",
      },
      open);
  }
}