using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.Models;
using MyMIS.Api.Services;

namespace MyMIS.Api.Tests.Services;

public class CityServiceBulkCreateTests : IDisposable
{
  private readonly AppDbContext _context;
  private readonly CityService _service;
  private readonly Region _ncr;
  private readonly Region _calabarzon;

  public CityServiceBulkCreateTests()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    _context = new AppDbContext(options);
    _service = new CityService(_context);

    _ncr = new Region { Name = "NCR" };
    _calabarzon = new Region { Name = "Calabarzon" };
    _context.Regions.AddRange(_ncr, _calabarzon);
    _context.SaveChanges();
  }

  public void Dispose()
  {
    _context.Dispose();
    GC.SuppressFinalize(this);
  }

  // ---------------- helpers ----------------

  private static JsonElement Json(string json)
  {
    using var document = JsonDocument.Parse(json);
    return document.RootElement.Clone();
  }

  private static List<JsonElement> Rows(params string[] json) => [.. json.Select(Json)];

  private async Task SeedCityAsync(Region region, string name)
  {
    _context.Cities.Add(new City { Name = name, RegionId = region.Id });
    await _context.SaveChangesAsync();
  }

  private Task<List<City>> SavedCitiesAsync(int regionId) =>
    _context.Cities.AsNoTracking().Where(c => c.RegionId == regionId).ToListAsync();

  // ---------------- tests ----------------

  [Fact]
  public async Task BulkCreateAsync_RegionDoesNotExist_ReturnsNull()
  {
    // Act
    var result = await _service.BulkCreateAsync(9999, Rows("""{"name":"Manila"}"""));

    // Assert
    Assert.Null(result);
  }

  [Fact]
  public async Task BulkCreateAsync_AllRowsValid_InsertsThemTrimmedUnderTheRequestedRegion()
  {
    // Act
    var result = await _service.BulkCreateAsync(_ncr.Id, Rows(
      """{"name":"  Manila  ","psgcCode":"133900000"}""",
      """{"name":"Pasig","psgcCode":"   "}"""));

    // Assert: result
    Assert.NotNull(result);
    Assert.Equal(2, result.Total);
    Assert.Equal(2, result.Inserted);
    Assert.Equal(0, result.Failed);
    Assert.Empty(result.Errors);
    Assert.Equal("Inserted 2 out of 2 successfully.", result.Message);

    // Assert: saved rows
    var saved = (await SavedCitiesAsync(_ncr.Id)).OrderBy(c => c.Name).ToList();
    Assert.Equal(new[] { "Manila", "Pasig" }, saved.Select(c => c.Name));
    Assert.Equal("133900000", saved[0].PsgcCode);
    Assert.Null(saved[1].PsgcCode);
  }

  [Fact]
  public async Task BulkCreateAsync_ParentIdInsideARow_IsIgnored()
  {
    // Act: the row claims another region, but the request's region wins
    var result = await _service.BulkCreateAsync(_ncr.Id, Rows("""{"name":"Manila","regionId":999}"""));

    // Assert
    Assert.NotNull(result);
    Assert.Equal(1, result.Inserted);
    var saved = Assert.Single(await SavedCitiesAsync(_ncr.Id));
    Assert.Equal("Manila", saved.Name);
  }

  [Fact]
  public async Task BulkCreateAsync_InvalidRowBetweenValidOnes_IsSkippedAndReportedWithItsRowNumber()
  {
    // Act: row 2 has no name
    var result = await _service.BulkCreateAsync(_ncr.Id, Rows(
      """{"name":"Manila"}""",
      """{"psgcCode":"X"}""",
      """{"name":"Pasig"}"""));

    // Assert
    Assert.NotNull(result);
    Assert.Equal(3, result.Total);
    Assert.Equal(2, result.Inserted);
    Assert.Equal(1, result.Failed);
    Assert.Equal("Inserted 2 out of 3 successfully.", result.Message);

    var error = Assert.Single(result.Errors);
    Assert.Equal(2, error.Row);
    Assert.Equal("X", error.Data.GetProperty("psgcCode").GetString());

    Assert.Equal(2, (await SavedCitiesAsync(_ncr.Id)).Count);
  }

  [Fact]
  public async Task BulkCreateAsync_RowIsNotAnObject_IsReportedAndNothingElseBreaks()
  {
    // Act
    var result = await _service.BulkCreateAsync(_ncr.Id, Rows("42", """{"name":"Manila"}"""));

    // Assert
    Assert.NotNull(result);
    Assert.Equal(1, result.Inserted);
    var error = Assert.Single(result.Errors);
    Assert.Equal(1, error.Row);
    Assert.Equal("Row must be a JSON object.", Assert.Single(error.Errors));
  }

  [Fact]
  public async Task BulkCreateAsync_EveryRowInvalid_InsertsNothingAndReportsRowsInOrder()
  {
    // Act
    var result = await _service.BulkCreateAsync(_ncr.Id, Rows(
      """{"psgcCode":"A"}""",
      """{"name":""}"""));

    // Assert
    Assert.NotNull(result);
    Assert.Equal(2, result.Total);
    Assert.Equal(0, result.Inserted);
    Assert.Equal(2, result.Failed);
    Assert.Equal(new[] { 1, 2 }, result.Errors.Select(e => e.Row));
    Assert.Empty(await SavedCitiesAsync(_ncr.Id));
  }

  [Fact]
  public async Task BulkCreateAsync_NameAlreadyInTheRegion_IsReportedAndNotInserted()
  {
    // Arrange
    await SeedCityAsync(_ncr, "Manila");

    // Act: surrounding spaces are trimmed before the check
    var result = await _service.BulkCreateAsync(_ncr.Id, Rows("""{"name":" Manila "}"""));

    // Assert
    Assert.NotNull(result);
    Assert.Equal(0, result.Inserted);
    var error = Assert.Single(result.Errors);
    Assert.Equal(1, error.Row);
    Assert.Equal("A city named 'Manila' already exists in NCR.", Assert.Single(error.Errors));
    Assert.Single(await SavedCitiesAsync(_ncr.Id));
  }

  [Fact]
  public async Task BulkCreateAsync_SameNameTwiceInTheSamePaste_InsertsTheFirstAndReportsTheSecond()
  {
    // Act
    var result = await _service.BulkCreateAsync(_ncr.Id, Rows(
      """{"name":"Quezon City"}""",
      """{"name":"Quezon City"}"""));

    // Assert
    Assert.NotNull(result);
    Assert.Equal(1, result.Inserted);
    var error = Assert.Single(result.Errors);
    Assert.Equal(2, error.Row);
    Assert.Single(await SavedCitiesAsync(_ncr.Id));
  }

  [Fact]
  public async Task BulkCreateAsync_SameNameInAnotherRegion_IsAllowed()
  {
    // Arrange: city names only have to be unique within one region
    await SeedCityAsync(_calabarzon, "San Jose");

    // Act
    var result = await _service.BulkCreateAsync(_ncr.Id, Rows("""{"name":"San Jose"}"""));

    // Assert
    Assert.NotNull(result);
    Assert.Equal(1, result.Inserted);
    Assert.Empty(result.Errors);
  }

  [Fact]
  public async Task BulkCreateAsync_SameNameWithDifferentCase_IsNotADuplicate()
  {
    // Arrange: matching is exact (ordinal), like the database index. If bulk upload ever
    // moves to NormalizedName, this is the test to flip.
    await SeedCityAsync(_ncr, "Manila");

    // Act
    var result = await _service.BulkCreateAsync(_ncr.Id, Rows("""{"name":"manila"}"""));

    // Assert
    Assert.NotNull(result);
    Assert.Equal(1, result.Inserted);
  }
}