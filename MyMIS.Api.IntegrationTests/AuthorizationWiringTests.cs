using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using MyMIS.Api.Authorization;
using MyMIS.Api.IntegrationTests.Infrastructure;

namespace MyMIS.Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public class AuthorizationWiringTests(ApiFactory factory)
{
  // An id that no row has, so a request that gets through authorization ends in 404
  // (or 415 for a missing body) and never changes any data.
  private const int Missing = 999_999;

  // ---------------- the tables: (HTTP method, URL, permission the controller requires) ----------------

  // One row per permission-gated route (design doc §6). Gate 1 only: the {Missing} ids
  // mean anything that passes authorization ends in 404/400/415 and changes no data.
  public static TheoryData<string, string, string> PermissionGated => new()
  {
    // Offices
    { "GET",    "/api/Offices", PermissionNames.Offices.Read },
    { "GET",    $"/api/Offices/{Missing}", PermissionNames.Offices.Read },
    { "POST",   "/api/Offices", PermissionNames.Offices.Create },
    { "PUT",    $"/api/Offices/{Missing}", PermissionNames.Offices.Update },
    { "DELETE", $"/api/Offices/{Missing}", PermissionNames.Offices.Delete },

    // HMO providers and plans (admin data; the /lookup routes are login-only)
    { "GET",  "/api/HmoProviders", PermissionNames.HmoProviders.Read },
    { "GET",  $"/api/HmoProviders/{Missing}", PermissionNames.HmoProviders.Read },
    { "POST", "/api/HmoProviders", PermissionNames.HmoProviders.Create },
    { "PUT",  $"/api/HmoProviders/{Missing}", PermissionNames.HmoProviders.Update },
    { "GET",  $"/api/HmoPlans?providerId={Missing}", PermissionNames.HmoPlans.Read },
    { "GET",  $"/api/HmoPlans/{Missing}", PermissionNames.HmoPlans.Read },
    { "POST", "/api/HmoPlans", PermissionNames.HmoPlans.Create },
    { "PUT",  $"/api/HmoPlans/{Missing}", PermissionNames.HmoPlans.Update },

    // Locations (bulk upload = create)
    { "GET",    "/api/Regions", PermissionNames.Locations.Read },
    { "GET",    $"/api/Regions/{Missing}", PermissionNames.Locations.Read },
    { "POST",   "/api/Regions", PermissionNames.Locations.Create },
    { "POST",   "/api/Regions/bulk", PermissionNames.Locations.Create },
    { "PUT",    $"/api/Regions/{Missing}", PermissionNames.Locations.Update },
    { "DELETE", $"/api/Regions/{Missing}", PermissionNames.Locations.Delete },
    { "GET",    "/api/Cities", PermissionNames.Locations.Read },
    { "GET",    $"/api/Cities/{Missing}", PermissionNames.Locations.Read },
    { "POST",   "/api/Cities", PermissionNames.Locations.Create },
    { "POST",   $"/api/Cities/bulk?regionId={Missing}", PermissionNames.Locations.Create },
    { "PUT",    $"/api/Cities/{Missing}", PermissionNames.Locations.Update },
    { "DELETE", $"/api/Cities/{Missing}", PermissionNames.Locations.Delete },
    { "GET",    "/api/Barangays", PermissionNames.Locations.Read },
    { "GET",    $"/api/Barangays/{Missing}", PermissionNames.Locations.Read },
    { "POST",   "/api/Barangays", PermissionNames.Locations.Create },
    { "POST",   $"/api/Barangays/bulk?cityId={Missing}", PermissionNames.Locations.Create },
    { "PUT",    $"/api/Barangays/{Missing}", PermissionNames.Locations.Update },
    { "DELETE", $"/api/Barangays/{Missing}", PermissionNames.Locations.Delete },

    // Departments (PUT also runs gate 2 inside the action; this row covers gate 1)
    { "GET",    "/api/Departments", PermissionNames.Departments.Read },
    { "GET",    $"/api/Departments/{Missing}", PermissionNames.Departments.Read },
    { "POST",   "/api/Departments", PermissionNames.Departments.Create },
    { "PUT",    $"/api/Departments/{Missing}", PermissionNames.Departments.Update },
    { "DELETE", $"/api/Departments/{Missing}", PermissionNames.Departments.Delete },

    // Positions (POST/PUT also run gate 2 inside the action)
    { "GET",    "/api/Positions", PermissionNames.Positions.Read },
    { "GET",    $"/api/Positions/{Missing}", PermissionNames.Positions.Read },
    { "POST",   "/api/Positions", PermissionNames.Positions.Create },
    { "PUT",    $"/api/Positions/{Missing}", PermissionNames.Positions.Update },
    { "DELETE", $"/api/Positions/{Missing}", PermissionNames.Positions.Delete },

    // Employees (PUT and partial also run gate 2 through EmployeeAccessGuard)
    { "POST",   "/api/Employees", PermissionNames.Employees.Create },
    { "GET",    "/api/Employees/availability?username=nobody", PermissionNames.Employees.Create },
    { "PUT",    $"/api/Employees/{Missing}", PermissionNames.Employees.Update },
    { "DELETE", $"/api/Employees/{Missing}", PermissionNames.Employees.Delete },
    { "PUT",    $"/api/Employees/partial/{Missing}", PermissionNames.Employees.WorkDetailsUpdate },

    // Employee sub-resources: view = employees.read, add/change/remove = employees.update
    { "GET",    $"/api/Employees/{Missing}/phones", PermissionNames.Employees.Read },
    { "GET",    $"/api/Employees/{Missing}/phones/1", PermissionNames.Employees.Read },
    { "POST",   $"/api/Employees/{Missing}/phones", PermissionNames.Employees.Update },
    { "PUT",    $"/api/Employees/{Missing}/phones/1", PermissionNames.Employees.Update },
    { "DELETE", $"/api/Employees/{Missing}/phones/1", PermissionNames.Employees.Update },

    { "GET",    $"/api/Employees/{Missing}/emails", PermissionNames.Employees.Read },
    { "GET",    $"/api/Employees/{Missing}/emails/1", PermissionNames.Employees.Read },
    { "POST",   $"/api/Employees/{Missing}/emails", PermissionNames.Employees.Update },
    { "PUT",    $"/api/Employees/{Missing}/emails/1", PermissionNames.Employees.Update },
    { "DELETE", $"/api/Employees/{Missing}/emails/1", PermissionNames.Employees.Update },

    { "GET",    $"/api/Employees/{Missing}/addresses", PermissionNames.Employees.Read },
    { "POST",   $"/api/Employees/{Missing}/addresses", PermissionNames.Employees.Update },
    { "PUT",    $"/api/Employees/{Missing}/addresses/1", PermissionNames.Employees.Update },
    { "DELETE", $"/api/Employees/{Missing}/addresses/1", PermissionNames.Employees.Update },

    { "GET",    $"/api/Employees/{Missing}/emergency-contacts", PermissionNames.Employees.Read },
    { "POST",   $"/api/Employees/{Missing}/emergency-contacts", PermissionNames.Employees.Update },
    { "PUT",    $"/api/Employees/{Missing}/emergency-contacts/1", PermissionNames.Employees.Update },
    { "DELETE", $"/api/Employees/{Missing}/emergency-contacts/1", PermissionNames.Employees.Update },

    { "GET", $"/api/Employees/{Missing}/government-ids", PermissionNames.Employees.Read },
    { "PUT", $"/api/Employees/{Missing}/government-ids", PermissionNames.Employees.Update },

    { "GET",    $"/api/Employees/{Missing}/hmo", PermissionNames.Employees.Read },
    { "POST",   $"/api/Employees/{Missing}/hmo", PermissionNames.Employees.Update },
    { "PUT",    $"/api/Employees/{Missing}/hmo", PermissionNames.Employees.Update },
    { "DELETE", $"/api/Employees/{Missing}/hmo", PermissionNames.Employees.Update },
    { "POST",   $"/api/Employees/{Missing}/hmo/dependents", PermissionNames.Employees.Update },
    { "PUT",    $"/api/Employees/{Missing}/hmo/dependents/1", PermissionNames.Employees.Update },
    { "DELETE", $"/api/Employees/{Missing}/hmo/dependents/1", PermissionNames.Employees.Update },
  };

  // Routes that only need a login (a plain [Authorize]): self-service, the
  // company directory and basic profile, and the form lookups.
  public static TheoryData<string, string> LoginOnly => new()
  {
    { "GET", "/api/Employees" },
    { "GET", $"/api/Employees/{Missing}" },
    { "GET", "/api/Employees/lookup" },
    { "GET", "/api/HmoProviders/lookup" },
    { "GET", "/api/Employees/me/phones" },
    { "GET", "/api/Employees/me/emails" },
    { "GET", "/api/Employees/me/addresses" },
    { "GET", "/api/Employees/me/emergency-contacts" },
    { "GET", "/api/Employees/me/government-ids" },
    { "GET", "/api/Employees/me/hmo" },
    { "POST", "/api/Auth/change-password" },
  };

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
    var client = await factory.GetClientWithoutPermissionsAsync();

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
    // Arrange: a role holding just this one permission
    var client = await factory.GetClientAsync(permission);

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
    var others = PermissionNames.All.Where(p => p != permission).ToArray();
    var client = await factory.GetClientAsync(others);

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
    // Arrange: SuperAdmin holds every permission through the cache
    var client = await factory.GetSuperAdminClientAsync();

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
    var client = await factory.GetClientWithoutPermissionsAsync();

    // Act
    var status = await SendAsync(client, method, url);

    // Assert
    Assert.False(RejectedByAuth(status), $"{Describe(method, url, status)} for a logged-in user with no permissions.");
  }

  [Fact]
  public void EveryControllerEndpoint_RequiresLogin_ExceptTheKnownPublicOnes()
  {
    // Arrange: ask the app for its own list of routes, and find the ones with no [Authorize] at all
    var provider = factory.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>();

    // Act
    var open = provider.ApiDescriptionGroups.Items
      .SelectMany(group => group.Items)
      .Where(api =>
      {
        var metadata = api.ActionDescriptor.EndpointMetadata ?? new List<object>();
        return !metadata.OfType<IAuthorizeData>().Any()
            || metadata.OfType<IAllowAnonymous>().Any();
      })
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