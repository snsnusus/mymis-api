using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.Models;
using MyMIS.Api.Services;

namespace MyMIS.Api.Tests.Services;

public class BarangayServiceBulkCreateTests : IDisposable
{
  private readonly AppDbContext _context;
  private readonly BarangayService _service;
  private readonly City _quezonCity;
  private readonly City _manila;

  public BarangayServiceBulkCreateTests()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    _context = new AppDbContext(options);
    _service = new BarangayService(_context);

    var region = new Region { Name = "NCR" };
    _quezonCity = new City { Name = "Quezon City", Region = region };
    _manila = new City { Name = "Manila", Region = region };
    _context.Cities.AddRange(_quezonCity, _manila);
    _context.SaveChanges();
  }

  public void Dispose()
  {
    _context.Dispose();
    GC.SuppressFinalize(this);
  }

  private static JsonElement Json(string json)
  {
    using var document = JsonDocument.Parse(json);
    return document.RootElement.Clone();
  }

  private static List<JsonElement> Rows(params string[] json) => [.. json.Select(Json)];

  private async Task SeedBarangayAsync(City city, string name)
  {
    _context.Barangays.Add(new Barangay { Name = name, CityId = city.Id });
    await _context.SaveChangesAsync();
  }

  private Task<List<Barangay>> SavedBarangaysAsync(int cityId) =>
    _context.Barangays.AsNoTracking().Where(b => b.CityId == cityId).ToListAsync();

  [Fact]
  public async Task BulkCreateAsync_CityDoesNotExist_ReturnsNull()
  {
    // Act
    var result = await _service.BulkCreateAsync(9999, Rows("""{"name":"Bagong Pag-asa"}"""));

    // Assert
    Assert.Null(result);
  }

  [Fact]
  public async Task BulkCreateAsync_AllRowsValid_InsertsThemTrimmedWithBlankCodesAsNull()
  {
    // Act
    var result = await _service.BulkCreateAsync(_quezonCity.Id, Rows(
      """{"name":"  Bagong Pag-asa  ","psgcCode":" 137404001 ","zipCode":" 1105 "}""",
      """{"name":"Batasan Hills","psgcCode":"","zipCode":"   "}"""));

    // Assert
    Assert.NotNull(result);
    Assert.Equal(2, result.Inserted);
    Assert.Empty(result.Errors);

    var saved = (await SavedBarangaysAsync(_quezonCity.Id)).OrderBy(b => b.Name).ToList();
    Assert.Equal(new[] { "Bagong Pag-asa", "Batasan Hills" }, saved.Select(b => b.Name));
    Assert.Equal("137404001", saved[0].PsgcCode);
    Assert.Equal("1105", saved[0].ZipCode);
    Assert.Null(saved[1].PsgcCode);
    Assert.Null(saved[1].ZipCode);
  }

  [Fact]
  public async Task BulkCreateAsync_ZipCodeTooLong_SkipsTheRowAndReportsIt()
  {
    // Act: row 2's zip code is 11 characters, the limit is 10
    var result = await _service.BulkCreateAsync(_quezonCity.Id, Rows(
      """{"name":"Bagong Pag-asa"}""",
      """{"name":"Batasan Hills","zipCode":"12345678901"}"""));

    // Assert
    Assert.NotNull(result);
    Assert.Equal(1, result.Inserted);
    var error = Assert.Single(result.Errors);
    Assert.Equal(2, error.Row);
    Assert.Contains("ZipCode", Assert.Single(error.Errors));
    Assert.Single(await SavedBarangaysAsync(_quezonCity.Id));
  }

  [Fact]
  public async Task BulkCreateAsync_NameAlreadyInTheCity_IsReportedAndNotInserted()
  {
    // Arrange
    await SeedBarangayAsync(_quezonCity, "Bagong Pag-asa");

    // Act
    var result = await _service.BulkCreateAsync(_quezonCity.Id, Rows("""{"name":" Bagong Pag-asa "}"""));

    // Assert
    Assert.NotNull(result);
    Assert.Equal(0, result.Inserted);
    var error = Assert.Single(result.Errors);
    Assert.Equal(1, error.Row);
    Assert.Single(await SavedBarangaysAsync(_quezonCity.Id));
  }

  [Fact]
  public async Task BulkCreateAsync_SameNameInAnotherCity_IsAllowed()
  {
    // Arrange: barangay names repeat across cities
    await SeedBarangayAsync(_manila, "Poblacion");

    // Act
    var result = await _service.BulkCreateAsync(_quezonCity.Id, Rows("""{"name":"Poblacion"}"""));

    // Assert
    Assert.NotNull(result);
    Assert.Equal(1, result.Inserted);
    Assert.Empty(result.Errors);
  }

  [Fact]
  public async Task BulkCreateAsync_SameNameTwiceInTheSamePaste_InsertsTheFirstAndReportsTheSecond()
  {
    // Act
    var result = await _service.BulkCreateAsync(_quezonCity.Id, Rows(
      """{"name":"Poblacion"}""",
      """{"name":"Poblacion"}"""));

    // Assert
    Assert.NotNull(result);
    Assert.Equal(1, result.Inserted);
    var error = Assert.Single(result.Errors);
    Assert.Equal(2, error.Row);
  }
}