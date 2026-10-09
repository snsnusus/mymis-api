using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Helpers;
using MyMIS.Api.IntegrationTests.Infrastructure;
using MyMIS.Api.Models;
using static MyMIS.Api.IntegrationTests.Infrastructure.TestData;

namespace MyMIS.Api.IntegrationTests;

// Real HTTP requests that hit a real database index (409 through the exception filter),
// plus the second authorization gate on Positions that the policy table couldn't reach.
[Collection(IntegrationCollection.Name)]
public class ApiConflictTests(ApiFactory factory)
{
  // ---------------- helpers ----------------

  private static object NewPosition(int departmentId, string slug) => new
  {
    title = "Associate",
    slug,
    description = "A position description.",
    sortOrder = 1,
    isActive = true,
    isApprover = false,
    departmentId,
  };

  // Includes the response body in the failure message, so a wrong status explains itself.
  private static async Task AssertStatusAsync(HttpStatusCode expected, HttpResponseMessage response)
  {
    Assert.True(
      response.StatusCode == expected,
      $"Expected {(int)expected} but got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
  }

  // Checks the whole 409 body: status, the field-keyed error list, and the plain message,
  // against what UniqueConstraints says for that index.
  private static async Task AssertUniqueConflictAsync(HttpResponseMessage response, string constraint)
  {
    await AssertStatusAsync(HttpStatusCode.Conflict, response);

    var (field, message) = UniqueConstraints.ByName[constraint];
    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var root = body.RootElement;

    Assert.Equal(409, root.GetProperty("status").GetInt32());
    Assert.Equal(message, root.GetProperty("message").GetString());

    var errors = root.GetProperty("errors").GetProperty(field);
    Assert.Equal(message, Assert.Single(errors.EnumerateArray()).GetString());
  }

  // A logged-in client whose employee belongs to the given department (the token carries a departmentId claim).
  private async Task<HttpClient> ClientInDepartmentAsync(int departmentId, params string[] permissions)
  {
    var employee = await factory.SeedEmployeeAsync(Role.User, permissions);

    await factory.ExecuteDbAsync(async db =>
      await db.Employees
        .Where(e => e.Id == employee.Id)
        .ExecuteUpdateAsync(set => set.SetProperty(e => e.DepartmentId, departmentId)));

    // Log in after the update, so the new token includes the department
    var token = await factory.LoginAsync(employee.Username);
    var client = factory.CreateApiClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return client;
  }

  // ---------------- 409 from the real index, through the filter ----------------

  [Fact]
  public async Task PostPosition_SameSlugInTheSameDepartment_Returns409()
  {
    // Arrange: positions have no pre-check, so this 409 can only come from the index and the filter
    var client = await factory.GetClientAsync(Role.SuperAdmin);
    var department = await factory.SeedDepartmentAsync();
    var slug = $"P{Unique()}";

    var first = await client.PostAsJsonAsync("/api/Positions", NewPosition(department.Id, slug));
    await AssertStatusAsync(HttpStatusCode.Created, first);

    // Act
    var second = await client.PostAsJsonAsync("/api/Positions", NewPosition(department.Id, slug));

    // Assert
    await AssertUniqueConflictAsync(second, "IX_Positions_DepartmentId_Slug");
  }

  [Fact]
  public async Task PostPosition_SameSlugInAnotherDepartment_IsAllowed()
  {
    // Arrange: the index is per department, not company-wide
    var client = await factory.GetClientAsync(Role.SuperAdmin);
    var slug = $"P{Unique()}";
    var firstDepartment = await factory.SeedDepartmentAsync();
    var secondDepartment = await factory.SeedDepartmentAsync();

    await AssertStatusAsync(
      HttpStatusCode.Created, await client.PostAsJsonAsync("/api/Positions", NewPosition(firstDepartment.Id, slug)));

    // Act
    var second = await client.PostAsJsonAsync("/api/Positions", NewPosition(secondDepartment.Id, slug));

    // Assert
    await AssertStatusAsync(HttpStatusCode.Created, second);
  }

  [Fact]
  public async Task PostCity_SameNameInTheSameRegion_Returns409()
  {
    // Arrange
    var client = await factory.GetClientAsync(Role.SuperAdmin);
    var region = await factory.SeedRegionAsync();
    var city = new { name = $"City {Unique()}", regionId = region.Id };

    await AssertStatusAsync(HttpStatusCode.Created, await client.PostAsJsonAsync("/api/Cities", city));

    // Act
    var second = await client.PostAsJsonAsync("/api/Cities", city);

    // Assert
    await AssertUniqueConflictAsync(second, "IX_Cities_RegionId_Name");
  }

  [Fact]
  public async Task PostCity_SameNameInAnotherRegion_IsAllowed()
  {
    // Arrange
    var client = await factory.GetClientAsync(Role.SuperAdmin);
    var name = $"City {Unique()}";
    var firstRegion = await factory.SeedRegionAsync();
    var secondRegion = await factory.SeedRegionAsync();

    await AssertStatusAsync(
      HttpStatusCode.Created,
      await client.PostAsJsonAsync("/api/Cities", new { name, regionId = firstRegion.Id }));

    // Act
    var second = await client.PostAsJsonAsync("/api/Cities", new { name, regionId = secondRegion.Id });

    // Assert
    await AssertStatusAsync(HttpStatusCode.Created, second);
  }

  [Fact]
  public async Task PostBarangay_SameNameInTheSameCity_Returns409()
  {
    // Arrange
    var client = await factory.GetClientAsync(Role.SuperAdmin);
    var city = await factory.SeedCityAsync(await factory.SeedRegionAsync());
    var barangay = new { name = $"Barangay {Unique()}", cityId = city.Id };

    await AssertStatusAsync(HttpStatusCode.Created, await client.PostAsJsonAsync("/api/Barangays", barangay));

    // Act
    var second = await client.PostAsJsonAsync("/api/Barangays", barangay);

    // Assert
    await AssertUniqueConflictAsync(second, "IX_Barangays_CityId_Name");
  }

  // ---------------- Positions: the second gate (SameDepartment) ----------------

  [Fact]
  public async Task PostPosition_UserWithThePermissionInTheSameDepartment_Returns201()
  {
    // Arrange
    var department = await factory.SeedDepartmentAsync();
    var client = await ClientInDepartmentAsync(department.Id, "positions.create");

    // Act
    var response = await client.PostAsJsonAsync("/api/Positions", NewPosition(department.Id, $"P{Unique()}"));

    // Assert
    await AssertStatusAsync(HttpStatusCode.Created, response);
  }

  [Fact]
  public async Task PostPosition_UserWithThePermissionButAnotherDepartment_Returns403()
  {
    // Arrange: passes gate 1 (the permission), fails gate 2 (the department)
    var ownDepartment = await factory.SeedDepartmentAsync();
    var otherDepartment = await factory.SeedDepartmentAsync();
    var client = await ClientInDepartmentAsync(ownDepartment.Id, "positions.create");

    // Act
    var response = await client.PostAsJsonAsync("/api/Positions", NewPosition(otherDepartment.Id, $"P{Unique()}"));

    // Assert
    await AssertStatusAsync(HttpStatusCode.Forbidden, response);
  }

  [Fact]
  public async Task PutPosition_UserWithThePermissionButThePositionIsInAnotherDepartment_Returns403()
  {
    // Arrange
    var ownDepartment = await factory.SeedDepartmentAsync();
    var otherDepartment = await factory.SeedDepartmentAsync();
    var position = await factory.SeedPositionAsync(otherDepartment);
    var client = await ClientInDepartmentAsync(ownDepartment.Id, "positions.update");

    // Act
    var response = await client.PutAsJsonAsync(
      $"/api/Positions/{position.Id}", NewPosition(otherDepartment.Id, position.Slug));

    // Assert
    await AssertStatusAsync(HttpStatusCode.Forbidden, response);
  }

  [Fact]
  public async Task PutPosition_UserWithThePermissionInTheSameDepartment_Returns200()
  {
    // Arrange
    var department = await factory.SeedDepartmentAsync();
    var position = await factory.SeedPositionAsync(department);
    var client = await ClientInDepartmentAsync(department.Id, "positions.update");

    // Act
    var response = await client.PutAsJsonAsync(
      $"/api/Positions/{position.Id}", NewPosition(department.Id, position.Slug));

    // Assert
    await AssertStatusAsync(HttpStatusCode.OK, response);
  }

  [Fact]
  public async Task PutPosition_ThatDoesNotExist_Returns404NotForbidden()
  {
    // Arrange: existence is checked before the department, so a missing id is a 404 even for this user
    var department = await factory.SeedDepartmentAsync();
    var client = await ClientInDepartmentAsync(department.Id, "positions.update");

    // Act
    var response = await client.PutAsJsonAsync("/api/Positions/999999", NewPosition(department.Id, $"P{Unique()}"));

    // Assert
    await AssertStatusAsync(HttpStatusCode.NotFound, response);
  }
}