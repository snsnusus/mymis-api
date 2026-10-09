using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;
using MyMIS.Api.Services;

namespace MyMIS.Api.Tests.Services;

public class OfficeServiceTests : IDisposable
{
  private readonly AppDbContext _context;
  private readonly OfficeService _service;

  public OfficeServiceTests()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    _context = new AppDbContext(options);
    _service = new OfficeService(_context);
  }

  public void Dispose()
  {
    _context.Dispose();
    GC.SuppressFinalize(this);
  }

  // ---------------- helpers ----------------

  private static OfficeCreateDto NewDto(
    string name = "Manila Office",
    string city = "Manila",
    string countryCode = "PH",
    string? address = null) => new()
    {
      Name = name,
      City = city,
      CountryCode = countryCode,
      Address = address,
    };

  // Inserts straight into the database. NormalizedName must be set here, because
  // NameExistsAsync compares that column, not Name.
  private async Task<Office> SeedOfficeAsync(
    string name,
    string city = "Manila",
    string countryCode = "PH",
    string? address = null)
  {
    var office = new Office
    {
      Name = name,
      NormalizedName = name.Trim().ToUpperInvariant(),
      City = city,
      CountryCode = countryCode,
      Address = address,
    };

    _context.Offices.Add(office);
    await _context.SaveChangesAsync();
    return office;
  }

  private Task<Office> SavedOfficeAsync(int id) =>
    _context.Offices.AsNoTracking().FirstAsync(o => o.Id == id);

  // ---------------- CreateAsync ----------------

  [Fact]
  public async Task CreateAsync_ValidOffice_PersistsAndReturnsCorrectData()
  {
    // Arrange
    var dto = NewDto("Manila Office", "Manila", "PH", "123 Ayala Avenue");

    // Act
    var result = await _service.CreateAsync(dto);

    // Assert: returned data
    Assert.True(result.Id > 0);
    Assert.Equal("Manila Office", result.Name);
    Assert.Equal("Manila", result.City);
    Assert.Equal("PH", result.CountryCode);
    Assert.Equal("123 Ayala Avenue", result.Address);

    // Assert: saved row
    var saved = await SavedOfficeAsync(result.Id);
    Assert.Equal("Manila Office", saved.Name);
    Assert.Equal("123 Ayala Avenue", saved.Address);
  }

  [Fact]
  public async Task CreateAsync_FieldsWithWhitespaceAndLowercaseCode_AreStoredNormalized()
  {
    // Arrange
    var dto = NewDto("  Manila Office  ", "  Manila  ", " ph ", "  ");

    // Act
    var result = await _service.CreateAsync(dto);

    // Assert
    var saved = await SavedOfficeAsync(result.Id);
    Assert.Equal("Manila Office", saved.Name);
    Assert.Equal("MANILA OFFICE", saved.NormalizedName);
    Assert.Equal("Manila", saved.City);
    Assert.Equal("PH", saved.CountryCode);
    Assert.Null(saved.Address);
  }

  [Fact]
  public async Task CreateAsync_AddressWithSurroundingWhitespace_IsTrimmed()
  {
    // Act
    var result = await _service.CreateAsync(NewDto(address: "  123 Ayala Avenue  "));

    // Assert
    var saved = await SavedOfficeAsync(result.Id);
    Assert.Equal("123 Ayala Avenue", saved.Address);
  }

  // ---------------- GetAllAsync ----------------

  [Fact]
  public async Task GetAllAsync_NoOffices_ReturnsEmptyList()
  {
    // Act
    var result = await _service.GetAllAsync();

    // Assert
    Assert.Empty(result);
  }

  [Fact]
  public async Task GetAllAsync_SeveralOffices_ReturnsThemOrderedByName()
  {
    // Arrange: seeded out of order on purpose
    await SeedOfficeAsync("Sydney Office", "Sydney", "AU");
    await SeedOfficeAsync("Manila Office");
    await SeedOfficeAsync("New York Office", "New York", "US");

    // Act
    var result = await _service.GetAllAsync();

    // Assert
    Assert.Equal(
      new[] { "Manila Office", "New York Office", "Sydney Office" },
      result.Select(o => o.Name));
  }

  // ---------------- GetByIdAsync ----------------

  [Fact]
  public async Task GetByIdAsync_ExistingOffice_ReturnsMatchingOffice()
  {
    // Arrange
    var seed = await SeedOfficeAsync("Manila Office", "Manila", "PH", "123 Ayala Avenue");

    // Act
    var result = await _service.GetByIdAsync(seed.Id);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(seed.Id, result.Id);
    Assert.Equal("Manila Office", result.Name);
    Assert.Equal("Manila", result.City);
    Assert.Equal("PH", result.CountryCode);
    Assert.Equal("123 Ayala Avenue", result.Address);
  }

  [Fact]
  public async Task GetByIdAsync_NonExistentId_ReturnsNull()
  {
    // Act
    var result = await _service.GetByIdAsync(9999);

    // Assert
    Assert.Null(result);
  }

  // ---------------- UpdateAsync ----------------

  [Fact]
  public async Task UpdateAsync_ValidUpdate_PersistsChangesAndRefreshesNormalizedName()
  {
    // Arrange
    var seed = await SeedOfficeAsync("Manila Office", "Manila", "PH", "123 Ayala Avenue");

    // Act: every field changes, with whitespace and a lowercase country code
    var result = await _service.UpdateAsync(
      seed.Id,
      NewDto("  Sydney Office  ", "  Sydney  ", " au ", "  1 George Street  "));

    // Assert: returned data
    Assert.NotNull(result);
    Assert.Equal(seed.Id, result.Id);
    Assert.Equal("Sydney Office", result.Name);

    // Assert: saved row, including the column the duplicate check reads
    var saved = await SavedOfficeAsync(seed.Id);
    Assert.Equal("Sydney Office", saved.Name);
    Assert.Equal("SYDNEY OFFICE", saved.NormalizedName);
    Assert.Equal("Sydney", saved.City);
    Assert.Equal("AU", saved.CountryCode);
    Assert.Equal("1 George Street", saved.Address);
  }

  [Fact]
  public async Task UpdateAsync_BlankAddress_ClearsItToNull()
  {
    // Arrange
    var seed = await SeedOfficeAsync("Manila Office", address: "123 Ayala Avenue");

    // Act
    var result = await _service.UpdateAsync(seed.Id, NewDto(address: "   "));

    // Assert
    Assert.NotNull(result);
    var saved = await SavedOfficeAsync(seed.Id);
    Assert.Null(saved.Address);
  }

  [Fact]
  public async Task UpdateAsync_NonExistentId_ReturnsNull()
  {
    // Act
    var result = await _service.UpdateAsync(9999, NewDto());

    // Assert
    Assert.Null(result);
  }

  // ---------------- DeleteAsync ----------------

  [Fact]
  public async Task DeleteAsync_ExistingOffice_RemovesTheRowEntirely()
  {
    // Arrange
    var seed = await SeedOfficeAsync("Manila Office");

    // Act
    var deleted = await _service.DeleteAsync(seed.Id);

    // Assert: hard delete, no soft-delete column
    Assert.True(deleted);
    Assert.Null(await _service.GetByIdAsync(seed.Id));
    Assert.False(await _context.Offices.AsNoTracking().AnyAsync(o => o.Id == seed.Id));
  }

  [Fact]
  public async Task DeleteAsync_NonExistentId_ReturnsFalse()
  {
    // Act
    var result = await _service.DeleteAsync(9999);

    // Assert
    Assert.False(result);
  }

  // ---------------- NameExistsAsync ----------------

  [Fact]
  public async Task NameExistsAsync_SameNameWithDifferentCaseAndWhitespace_ReturnsTrue()
  {
    // Arrange
    await SeedOfficeAsync("Manila Office");

    // Act
    var exists = await _service.NameExistsAsync("  manila OFFICE ");

    // Assert
    Assert.True(exists);
  }

  [Fact]
  public async Task NameExistsAsync_NewName_ReturnsFalse()
  {
    // Arrange
    await SeedOfficeAsync("Manila Office");

    // Act
    var exists = await _service.NameExistsAsync("Cebu Office");

    // Assert
    Assert.False(exists);
  }

  [Fact]
  public async Task NameExistsAsync_NameOfTheExcludedOffice_ReturnsFalse()
  {
    // Arrange: renaming office X to its own current name is not a conflict
    var seed = await SeedOfficeAsync("Manila Office");

    // Act
    var exists = await _service.NameExistsAsync("Manila Office", excludeId: seed.Id);

    // Assert
    Assert.False(exists);
  }

  [Fact]
  public async Task NameExistsAsync_NameOfADifferentOfficeThanTheExcludedOne_ReturnsTrue()
  {
    // Arrange
    await SeedOfficeAsync("Manila Office");
    var cebu = await SeedOfficeAsync("Cebu Office", "Cebu");

    // Act: Cebu is being renamed to "Manila Office", which another office already uses
    var exists = await _service.NameExistsAsync("Manila Office", excludeId: cebu.Id);

    // Assert
    Assert.True(exists);
  }
}