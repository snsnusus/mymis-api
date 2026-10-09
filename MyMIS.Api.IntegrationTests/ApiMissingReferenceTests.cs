using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Filters;
using MyMIS.Api.IntegrationTests.Infrastructure;
using MyMIS.Api.Models;
using static MyMIS.Api.IntegrationTests.Infrastructure.TestData;

namespace MyMIS.Api.IntegrationTests;

// A save that points at a record that doesn't exist: real request, real foreign key, real 400.
[Collection(IntegrationCollection.Name)]
public class ApiMissingReferenceTests(ApiFactory factory)
{
  // An id that no row has
  private const int Missing = 999_999;

  private static async Task AssertMissingReferenceAsync(HttpResponseMessage response)
  {
    Assert.True(
      response.StatusCode == HttpStatusCode.BadRequest,
      $"Expected 400 but got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var root = body.RootElement;

    Assert.Equal(400, root.GetProperty("status").GetInt32());
    Assert.Equal(ForeignKeyViolationExceptionFilter.MissingReferenceMessage, root.GetProperty("message").GetString());
  }

  [Fact]
  public async Task PostBarangay_WithACityThatDoesNotExist_Returns400AndSavesNothing()
  {
    // Arrange
    var client = await factory.GetClientAsync(Role.SuperAdmin);
    var name = $"Barangay {Unique()}";

    // Act
    var response = await client.PostAsJsonAsync("/api/Barangays", new { name, cityId = Missing });

    // Assert
    await AssertMissingReferenceAsync(response);
    Assert.False(await factory.QueryDbAsync(db => db.Barangays.AnyAsync(b => b.Name == name)));
  }

  [Fact]
  public async Task PostCity_WithARegionThatDoesNotExist_Returns400AndSavesNothing()
  {
    // Arrange
    var client = await factory.GetClientAsync(Role.SuperAdmin);
    var name = $"City {Unique()}";

    // Act
    var response = await client.PostAsJsonAsync("/api/Cities", new { name, regionId = Missing });

    // Assert
    await AssertMissingReferenceAsync(response);
    Assert.False(await factory.QueryDbAsync(db => db.Cities.AnyAsync(c => c.Name == name)));
  }

  [Fact]
  public async Task PostPosition_WithADepartmentThatDoesNotExist_Returns400AndSavesNothing()
  {
    // Arrange: SuperAdmin passes the SameDepartment check, so the foreign key is what rejects it
    var client = await factory.GetClientAsync(Role.SuperAdmin);
    var slug = $"P{Unique()}";

    // Act
    var response = await client.PostAsJsonAsync("/api/Positions", new
    {
      title = "Associate",
      slug,
      description = "A position description.",
      sortOrder = 1,
      isActive = true,
      isApprover = false,
      departmentId = Missing,
    });

    // Assert
    await AssertMissingReferenceAsync(response);
    Assert.False(await factory.QueryDbAsync(db => db.Positions.AnyAsync(p => p.Slug == slug)));
  }

  [Fact]
  public async Task PutBarangay_MovedToACityThatDoesNotExist_Returns400AndKeepsTheOldCity()
  {
    // Arrange: an update, not an insert, so the rejected entity is Modified instead of Added
    var client = await factory.GetClientAsync(Role.SuperAdmin);
    var city = await factory.SeedCityAsync(await factory.SeedRegionAsync());
    var barangay = await factory.SeedBarangayAsync(city);

    // Act
    var response = await client.PutAsJsonAsync(
      $"/api/Barangays/{barangay.Id}", new { name = barangay.Name, cityId = Missing });

    // Assert
    await AssertMissingReferenceAsync(response);

    var savedCityId = await factory.QueryDbAsync(async db =>
      (await db.Barangays.AsNoTracking().SingleAsync(b => b.Id == barangay.Id)).CityId);

    Assert.Equal(city.Id, savedCityId);
  }
}