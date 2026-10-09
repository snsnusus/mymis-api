using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Filters;
using MyMIS.Api.IntegrationTests.Infrastructure;
using MyMIS.Api.Models;

namespace MyMIS.Api.IntegrationTests;

// Deleting a row that other rows still point at: real HTTP request, real foreign key, real 409.
[Collection(IntegrationCollection.Name)]
public class ApiDeleteConflictTests(ApiFactory factory)
{
  private static async Task AssertStatusAsync(HttpStatusCode expected, HttpResponseMessage response)
  {
    Assert.True(
      response.StatusCode == expected,
      $"Expected {(int)expected} but got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
  }

  private static async Task AssertStillInUseAsync(HttpResponseMessage response)
  {
    await AssertStatusAsync(HttpStatusCode.Conflict, response);

    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var root = body.RootElement;

    Assert.Equal(409, root.GetProperty("status").GetInt32());
    Assert.Equal(ForeignKeyViolationExceptionFilter.InUseMessage, root.GetProperty("message").GetString());
  }

  [Fact]
  public async Task DeleteRegion_ThatStillHasACity_Returns409AndKeepsTheRegion()
  {
    // Arrange
    var client = await factory.GetClientAsync(Role.SuperAdmin);
    var region = await factory.SeedRegionAsync();
    await factory.SeedCityAsync(region);

    // Act
    var response = await client.DeleteAsync($"/api/Regions/{region.Id}");

    // Assert
    await AssertStillInUseAsync(response);
    Assert.True(await factory.QueryDbAsync(db => db.Regions.AnyAsync(r => r.Id == region.Id)));
  }

  [Fact]
  public async Task DeleteCity_ThatStillHasABarangay_Returns409AndKeepsTheCity()
  {
    // Arrange
    var client = await factory.GetClientAsync(Role.SuperAdmin);
    var city = await factory.SeedCityAsync(await factory.SeedRegionAsync());
    await factory.SeedBarangayAsync(city);

    // Act
    var response = await client.DeleteAsync($"/api/Cities/{city.Id}");

    // Assert
    await AssertStillInUseAsync(response);
    Assert.True(await factory.QueryDbAsync(db => db.Cities.AnyAsync(c => c.Id == city.Id)));
  }

  [Fact]
  public async Task DeleteDepartment_ThatStillHasAPosition_Returns409AndKeepsTheDepartment()
  {
    // Arrange
    var client = await factory.GetClientAsync(Role.SuperAdmin);
    var department = await factory.SeedDepartmentAsync();
    await factory.SeedPositionAsync(department);

    // Act
    var response = await client.DeleteAsync($"/api/Departments/{department.Id}");

    // Assert
    await AssertStillInUseAsync(response);
    Assert.True(await factory.QueryDbAsync(db => db.Departments.AnyAsync(d => d.Id == department.Id)));
  }

  [Fact]
  public async Task DeleteBarangay_UsedByAnEmergencyContactAddress_Returns409AndKeepsTheBarangay()
  {
    // Arrange: the reference comes from an owned type in another table
    var client = await factory.GetClientAsync(Role.SuperAdmin);
    var barangay = await factory.SeedBarangayAsync(await factory.SeedCityAsync(await factory.SeedRegionAsync()));
    var employee = await factory.SeedEmployeeAsync();
    await factory.SeedEmergencyContactInBarangayAsync(employee, barangay);

    // Act
    var response = await client.DeleteAsync($"/api/Barangays/{barangay.Id}");

    // Assert
    await AssertStillInUseAsync(response);
    Assert.True(await factory.QueryDbAsync(db => db.Barangays.AnyAsync(b => b.Id == barangay.Id)));
  }

  [Fact]
  public async Task DeleteRegion_WithNoCities_StillWorks()
  {
    // Arrange: the filter must not get in the way of a delete that is allowed
    var client = await factory.GetClientAsync(Role.SuperAdmin);
    var region = await factory.SeedRegionAsync();

    // Act
    var response = await client.DeleteAsync($"/api/Regions/{region.Id}");

    // Assert
    await AssertStatusAsync(HttpStatusCode.NoContent, response);
    Assert.False(await factory.QueryDbAsync(db => db.Regions.AnyAsync(r => r.Id == region.Id)));
  }

  [Fact]
  public async Task DeleteCity_AfterItsBarangaysAreDeleted_Works()
  {
    // Arrange: the order the 409 message tells a user to follow, children first
    var client = await factory.GetClientAsync(Role.SuperAdmin);
    var city = await factory.SeedCityAsync(await factory.SeedRegionAsync());
    var barangay = await factory.SeedBarangayAsync(city);

    await AssertStatusAsync(HttpStatusCode.NoContent, await client.DeleteAsync($"/api/Barangays/{barangay.Id}"));

    // Act
    var response = await client.DeleteAsync($"/api/Cities/{city.Id}");

    // Assert
    await AssertStatusAsync(HttpStatusCode.NoContent, response);
  }
}