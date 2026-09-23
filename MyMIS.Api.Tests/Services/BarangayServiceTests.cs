using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;
using MyMIS.Api.Services;

namespace MyMIS.Api.Tests.Services;

public class BarangayServiceTests : IDisposable
{
  private readonly AppDbContext _context;
  private readonly BarangayService _service;

  public BarangayServiceTests()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    _context = new AppDbContext(options);
    _service = new BarangayService(_context);
  }

  public void Dispose()
  {
    _context.Dispose();
    GC.SuppressFinalize(this);
  }

  // Shared scaffolding: a Barangay can't exist without a City, and a City
  // can't exist without a Region, so most tests below need this chain
  // just to set up their Arrange step.
  private async Task<City> SeedCityAsync(string cityName = "Pasig", string regionName = "Metro Manila (NCR)")
  {
    var region = new Region { Name = regionName };
    _context.Regions.Add(region);
    await _context.SaveChangesAsync();

    var city = new City { Name = cityName, RegionId = region.Id };
    _context.Cities.Add(city);
    await _context.SaveChangesAsync();

    return city;
  }

  [Fact]
  public async Task CreateAsync_ValidBarangay_PersistsAndReturnsCorrectData()
  {
    // Arrange
    var city = await SeedCityAsync();

    var dto = new BarangayCreateDto
    {
      Name = "Kapitolyo",
      PsgcCode = "137403009",
      ZipCode = "1603",
      CityId = city.Id
    };

    // Act
    var result = await _service.CreateAsync(dto);

    // Assert
    Assert.NotNull(result);
    Assert.True(result.Id > 0);
    Assert.Equal("Kapitolyo", result.Name);
    Assert.Equal("137403009", result.PsgcCode);
    Assert.Equal("1603", result.ZipCode);
    Assert.Equal(city.Id, result.CityId);
    Assert.Equal("Pasig", result.CityName);

    var savedBarangay = await _context.Barangays.FirstAsync(b => b.Id == result.Id);
    Assert.Equal(result.Name, savedBarangay.Name);
  }

  [Fact]
  public async Task CreateAsync_ReturnsCorrectCityName()
  {
    // Arrange
    var city1 = await SeedCityAsync("Pasig", "Metro Manila (NCR)");
    var city2 = await SeedCityAsync("Antipolo", "Calabarzon");

    var barangay1Dto = new BarangayCreateDto { Name = "Kapitolyo", CityId = city1.Id };
    var barangay2Dto = new BarangayCreateDto { Name = "San Roque", CityId = city2.Id };

    // Act
    var result1 = await _service.CreateAsync(barangay1Dto);
    var result2 = await _service.CreateAsync(barangay2Dto);

    // Assert
    Assert.Equal("Pasig", result1.CityName);
    Assert.Equal("Antipolo", result2.CityName);
  }

  [Fact]
  public async Task GetAllAsync_NoFilter_ReturnsAllBarangays()
  {
    // Arrange
    var city = await SeedCityAsync();
    _context.Barangays.AddRange(
        new Barangay { Name = "Kapitolyo", CityId = city.Id },
        new Barangay { Name = "Oranbo", CityId = city.Id }
    );
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetAllAsync(cityId: null);

    // Assert
    Assert.Equal(2, result.Count);
  }

  [Fact]
  public async Task GetAllAsync_WithCityIdFilter_ReturnsOnlyMatchingBarangays()
  {
    // Arrange
    var city1 = await SeedCityAsync("Pasig", "Metro Manila (NCR)");
    var city2 = await SeedCityAsync("Antipolo", "Calabarzon");

    _context.Barangays.AddRange(
        new Barangay { Name = "Kapitolyo", CityId = city1.Id },
        new Barangay { Name = "Oranbo", CityId = city1.Id },
        new Barangay { Name = "San Roque", CityId = city2.Id }
    );
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetAllAsync(cityId: city1.Id);

    // Assert
    Assert.Equal(2, result.Count);
    Assert.All(result, b => Assert.Equal(city1.Id, b.CityId));
    Assert.DoesNotContain(result, b => b.Name == "San Roque");
  }

  [Fact]
  public async Task GetByIdAsync_NonExistentId_ReturnsNull()
  {
    // Act
    var result = await _service.GetByIdAsync(999);

    // Assert
    Assert.Null(result);
  }

  [Fact]
  public async Task GetByIdAsync_ExistingId_ReturnsMatchingBarangay()
  {
    // Arrange
    var city = await SeedCityAsync();
    var barangay = new Barangay { Name = "Kapitolyo", CityId = city.Id, ZipCode = "1603" };
    _context.Barangays.Add(barangay);
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetByIdAsync(barangay.Id);

    // Assert
    Assert.NotNull(result);
    Assert.Equal("Kapitolyo", result.Name);
    Assert.Equal("1603", result.ZipCode);
    Assert.Equal("Pasig", result.CityName);
  }

  [Fact]
  public async Task UpdateAsync_ValidUpdate_PersistsChanges()
  {
    // Arrange
    var city1 = await SeedCityAsync("Pasig", "Metro Manila (NCR)");
    var city2 = await SeedCityAsync("Antipolo", "Calabarzon");

    var barangay = new Barangay { Name = "Old Name", PsgcCode = "111111111", ZipCode = "1000", CityId = city1.Id };
    _context.Barangays.Add(barangay);
    await _context.SaveChangesAsync();

    var dto = new BarangayCreateDto
    {
      Name = "New Name",
      PsgcCode = "222222222",
      ZipCode = "2000",
      CityId = city2.Id // genuine reassignment to a different city
    };

    // Act
    var result = await _service.UpdateAsync(barangay.Id, dto);

    // Assert
    Assert.NotNull(result);
    Assert.Equal("New Name", result.Name);
    Assert.Equal("222222222", result.PsgcCode);
    Assert.Equal("2000", result.ZipCode);
    Assert.Equal(city2.Id, result.CityId);
    Assert.Equal("Antipolo", result.CityName);
  }

  [Fact]
  public async Task UpdateAsync_NonExistentId_ReturnsNull()
  {
    // Arrange
    var dto = new BarangayCreateDto { Name = "Doesn't Matter", CityId = 1 };

    // Act
    var result = await _service.UpdateAsync(999, dto);

    // Assert
    Assert.Null(result);
  }

  [Fact]
  public async Task DeleteAsync_ExistingBarangay_RemovesRowEntirely()
  {
    // Arrange
    var city = await SeedCityAsync();
    var barangay = new Barangay { Name = "To Be Deleted", CityId = city.Id };
    _context.Barangays.Add(barangay);
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.DeleteAsync(barangay.Id);

    // Assert
    Assert.True(result);
    Assert.False(await _context.Barangays.AnyAsync(b => b.Id == barangay.Id));
  }

  [Fact]
  public async Task DeleteAsync_NonExistentId_ReturnsFalse()
  {
    // Act
    var result = await _service.DeleteAsync(999);

    // Assert
    Assert.False(result);
  }
}