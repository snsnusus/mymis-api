using System.Net;
using System.Net.Http.Json;
using MyMIS.Api.Authorization;
using MyMIS.Api.DTOs;
using MyMIS.Api.IntegrationTests.Infrastructure;
using MyMIS.Api.Models;

namespace MyMIS.Api.IntegrationTests;

/// <summary>
/// <c>GET /api/Auth/me/permissions</c> over real HTTP and Postgres, read through
/// the same cache the permission checks use.
/// </summary>
[Collection(IntegrationCollection.Name)]
public class AuthPermissionsApiTests(ApiFactory factory)
{
  private const string Url = "/api/Auth/me/permissions";

  private static async Task<MyPermissionsResponseDto> GetPermissionsAsync(HttpClient client)
  {
    var response = await client.GetAsync(Url);

    Assert.True(
      response.StatusCode == HttpStatusCode.OK,
      $"Expected 200 but got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

    return (await response.Content.ReadFromJsonAsync<MyPermissionsResponseDto>())!;
  }

  [Fact]
  public async Task GetMyPermissions_ReturnsExactlyTheCallersPermissionsSortedByName()
  {
    var client = await factory.GetClientAsync(PermissionNames.Positions.Update, PermissionNames.Offices.Read);

    var body = await GetPermissionsAsync(client);

    Assert.Equal(
      [PermissionNames.Offices.Read, PermissionNames.Positions.Update],
      body.Permissions.Select(p => p.Permission));
    Assert.All(body.Permissions, p => Assert.Equal(PermissionScope.Company, p.Scope));
  }

  [Fact]
  public async Task GetMyPermissions_SuperAdmin_ReturnsEveryPermissionCompanyWide()
  {
    var client = await factory.GetSuperAdminClientAsync();

    var body = await GetPermissionsAsync(client);

    Assert.Equal(PermissionNames.All.Order(), body.Permissions.Select(p => p.Permission));
    Assert.All(body.Permissions, p => Assert.Equal(PermissionScope.Company, p.Scope));
  }

  [Fact]
  public async Task GetMyPermissions_RoleWithNoPermissions_ReturnsAnEmptyList()
  {
    var client = await factory.GetClientWithoutPermissionsAsync();

    var body = await GetPermissionsAsync(client);

    Assert.Empty(body.Permissions);
  }
}