using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;
using MyMIS.Api.Services;

namespace MyMIS.Api.Tests.Services;

public class CityServiceTests : IDisposable
{
  private readonly AppDbContext _context;
  private readonly CityService _service;

  public CityServiceTests()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    _context = new AppDbContext(options);
    _service = new CityService(_context);
  }

  public void Dispose()
  {
    _context.Dispose();
    GC.SuppressFinalize(this);
  }

  [Fact]
  public async Task CreateAsync_ValidCity_PersistsAndReturnsCorrectData()
  {
    // Arrange
    var region = new Region { Name = "Metro Manila (NCR)", PsgcCode = "130000000" };
    _context.Regions.Add(region);
    await _context.SaveChangesAsync();

    var dto = new CityCreateDto
    {
      Name = "Pasig",
      PsgcCode = "137403000",
      RegionId = region.Id
    };

    // Act
    var result = await _service.CreateAsync(dto);

    // Assert
    Assert.NotNull(result);
    Assert.True(result.Id > 0);
    Assert.Equal("Pasig", result.Name);
    Assert.Equal("137403000", result.PsgcCode);
    Assert.Equal(region.Id, result.RegionId);
    Assert.Equal("Metro Manila (NCR)", result.RegionName);

    var savedCity = await _context.Cities.FirstAsync(c => c.Id == result.Id);
    Assert.Equal(result.Name, savedCity.Name);
  }

  [Fact]
  public async Task CreateAsync_ReturnsCorrectRegionName()
  {
    // Arrange
    var region1 = new Region { Name = "Metro Manila (NCR)" };
    var region2 = new Region { Name = "Calabarzon" };
    _context.Regions.AddRange(region1, region2);
    await _context.SaveChangesAsync();

    var city1Dto = new CityCreateDto { Name = "Pasig", RegionId = region1.Id };
    var city2Dto = new CityCreateDto { Name = "Antipolo", RegionId = region2.Id };

    // Act
    var result1 = await _service.CreateAsync(city1Dto);
    var result2 = await _service.CreateAsync(city2Dto);

    // Assert
    Assert.Equal("Metro Manila (NCR)", result1.RegionName);
    Assert.Equal("Calabarzon", result2.RegionName);
  }

  [Fact]
  public async Task GetAllAsync_NoFilter_ReturnsAllCities()
  {
    // Arrange
    var region = new Region { Name = "Metro Manila (NCR)" };
    _context.Regions.Add(region);
    await _context.SaveChangesAsync();

    _context.Cities.AddRange(
        new City { Name = "Pasig", RegionId = region.Id },
        new City { Name = "Marikina", RegionId = region.Id }
    );
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetAllAsync(regionId: null);

    // Assert
    Assert.Equal(2, result.Count);
  }

  [Fact]
  public async Task GetAllAsync_WithRegionIdFilter_ReturnsOnlyMatchingCities()
  {
    // Arrange
    var region1 = new Region { Name = "Metro Manila (NCR)" };
    var region2 = new Region { Name = "Calabarzon" };
    _context.Regions.AddRange(region1, region2);
    await _context.SaveChangesAsync();

    _context.Cities.AddRange(
        new City { Name = "Pasig", RegionId = region1.Id },
        new City { Name = "Marikina", RegionId = region1.Id },
        new City { Name = "Antipolo", RegionId = region2.Id }
    );
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetAllAsync(regionId: region1.Id);

    // Assert
    Assert.Equal(2, result.Count);
    Assert.All(result, c => Assert.Equal(region1.Id, c.RegionId));
    Assert.DoesNotContain(result, c => c.Name == "Antipolo");
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
  public async Task GetByIdAsync_ExistingId_ReturnsMatchingCity()
  {
    // Arrange
    var region = new Region { Name = "Metro Manila (NCR)" };
    _context.Regions.Add(region);
    await _context.SaveChangesAsync();

    var city = new City { Name = "Pasig", RegionId = region.Id };
    _context.Cities.Add(city);
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetByIdAsync(city.Id);

    // Assert
    Assert.NotNull(result);
    Assert.Equal("Pasig", result.Name);
    Assert.Equal("Metro Manila (NCR)", result.RegionName);
  }

  [Fact]
  public async Task UpdateAsync_ValidUpdate_PersistsChanges()
  {
    // Arrange
    var region1 = new Region { Name = "Metro Manila (NCR)" };
    var region2 = new Region { Name = "Calabarzon" };
    _context.Regions.AddRange(region1, region2);
    await _context.SaveChangesAsync();

    var city = new City { Name = "Old Name", PsgcCode = "111111111", RegionId = region1.Id };
    _context.Cities.Add(city);
    await _context.SaveChangesAsync();

    var dto = new CityCreateDto
    {
      Name = "New Name",
      PsgcCode = "222222222",
      RegionId = region2.Id // genuine reassignment to a different region
    };

    // Act
    var result = await _service.UpdateAsync(city.Id, dto);

    // Assert
    Assert.NotNull(result);
    Assert.Equal("New Name", result.Name);
    Assert.Equal("222222222", result.PsgcCode);
    Assert.Equal(region2.Id, result.RegionId);
    Assert.Equal("Calabarzon", result.RegionName);
  }

  [Fact]
  public async Task UpdateAsync_NonExistentId_ReturnsNull()
  {
    // Arrange
    var dto = new CityCreateDto { Name = "Doesn't Matter", RegionId = 1 };

    // Act
    var result = await _service.UpdateAsync(999, dto);

    // Assert
    Assert.Null(result);
  }

  [Fact]
  public async Task DeleteAsync_ExistingCity_RemovesRowEntirely()
  {
    // Arrange
    var region = new Region { Name = "Metro Manila (NCR)" };
    _context.Regions.Add(region);
    await _context.SaveChangesAsync();

    var city = new City { Name = "To Be Deleted", RegionId = region.Id };
    _context.Cities.Add(city);
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.DeleteAsync(city.Id);

    // Assert
    Assert.True(result);
    Assert.False(await _context.Cities.AnyAsync(c => c.Id == city.Id));
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