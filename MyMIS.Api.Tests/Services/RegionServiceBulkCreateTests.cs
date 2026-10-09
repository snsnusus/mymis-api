using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.Models;
using MyMIS.Api.Services;

namespace MyMIS.Api.Tests.Services;

public class RegionServiceBulkCreateTests : IDisposable
{
  private readonly AppDbContext _context;
  private readonly RegionService _service;

  public RegionServiceBulkCreateTests()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    _context = new AppDbContext(options);
    _service = new RegionService(_context);
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

  [Fact]
  public async Task BulkCreateAsync_AllRowsValid_InsertsThemTrimmedWithBlankPsgcCodeAsNull()
  {
    // Act
    var result = await _service.BulkCreateAsync(Rows(
      """{"name":"  NCR  ","psgcCode":"130000000"}""",
      """{"name":"Calabarzon","psgcCode":"   "}"""));

    // Assert
    Assert.Equal(2, result.Total);
    Assert.Equal(2, result.Inserted);
    Assert.Empty(result.Errors);

    var saved = (await _context.Regions.AsNoTracking().ToListAsync()).OrderBy(r => r.Name).ToList();
    Assert.Equal(new[] { "Calabarzon", "NCR" }, saved.Select(r => r.Name));
    Assert.Null(saved[0].PsgcCode);
    Assert.Equal("130000000", saved[1].PsgcCode);
  }

  [Fact]
  public async Task BulkCreateAsync_InvalidRow_IsSkippedAndReportedWithItsRowNumber()
  {
    // Act
    var result = await _service.BulkCreateAsync(Rows(
      """{"name":"NCR"}""",
      """{"psgcCode":"X"}"""));

    // Assert
    Assert.Equal(2, result.Total);
    Assert.Equal(1, result.Inserted);
    Assert.Equal(1, result.Failed);
    Assert.Equal("Inserted 1 out of 2 successfully.", result.Message);
    var error = Assert.Single(result.Errors);
    Assert.Equal(2, error.Row);
  }

  [Fact]
  public async Task BulkCreateAsync_NameAlreadyExists_IsReportedAndNotInserted()
  {
    // Arrange: Regions has no unique index, but duplicates would make every dropdown ambiguous
    _context.Regions.Add(new Region { Name = "NCR" });
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.BulkCreateAsync(Rows("""{"name":" NCR "}"""));

    // Assert
    Assert.Equal(0, result.Inserted);
    var error = Assert.Single(result.Errors);
    Assert.Equal("A region named 'NCR' already exists.", Assert.Single(error.Errors));
    Assert.Single(await _context.Regions.AsNoTracking().ToListAsync());
  }

  [Fact]
  public async Task BulkCreateAsync_SameNameTwiceInTheSamePaste_InsertsTheFirstAndReportsTheSecond()
  {
    // Act
    var result = await _service.BulkCreateAsync(Rows(
      """{"name":"NCR"}""",
      """{"name":"NCR"}"""));

    // Assert
    Assert.Equal(1, result.Inserted);
    var error = Assert.Single(result.Errors);
    Assert.Equal(2, error.Row);
  }

  [Fact]
  public async Task BulkCreateAsync_EveryRowInvalid_InsertsNothing()
  {
    // Act
    var result = await _service.BulkCreateAsync(Rows("""{"psgcCode":"A"}""", "42"));

    // Assert
    Assert.Equal(0, result.Inserted);
    Assert.Equal(new[] { 1, 2 }, result.Errors.Select(e => e.Row));
    Assert.Empty(await _context.Regions.AsNoTracking().ToListAsync());
  }
}