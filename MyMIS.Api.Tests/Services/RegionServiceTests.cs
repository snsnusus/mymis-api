using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;
using MyMIS.Api.Services;

namespace MyMIS.Api.Tests.Services;

public class RegionServiceTests : IDisposable
{
  private readonly AppDbContext _context;
  private readonly RegionService _service;

  public RegionServiceTests()
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

  [Fact]
  public async Task CreateAsync_ValidRegion_PersistsAndReturnsCorrectData()
  {
    // Arrange
    var dto = new RegionCreateDto
    {
      Name = "Metro Manila (NCR)",
      PsgcCode = "130000000"
    };

    // Act
    var result = await _service.CreateAsync(dto);

    // Assert
    Assert.NotNull(result);
    Assert.True(result.Id > 0);
    Assert.Equal("Metro Manila (NCR)", result.Name);
    Assert.Equal("130000000", result.PsgcCode);

    var savedRegion = await _context.Regions.FirstAsync(r => r.Id == result.Id);
    Assert.Equal(result.Name, savedRegion.Name);
  }

  [Fact]
  public async Task CreateAsync_NullPsgcCode_PersistsAsNull()
  {
    // Arrange
    var dto = new RegionCreateDto
    {
      Name = "Region Without Code",
      PsgcCode = null
    };

    // Act
    var result = await _service.CreateAsync(dto);

    // Assert
    Assert.NotNull(result);
    Assert.Null(result.PsgcCode);
  }

  [Fact]
  public async Task GetAllAsync_ReturnsAllRegions()
  {
    // Arrange
    _context.Regions.AddRange(
        new Region { Name = "Metro Manila (NCR)", PsgcCode = "130000000" },
        new Region { Name = "Calabarzon", PsgcCode = "40000000" }
    );
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetAllAsync();

    // Assert
    Assert.Equal(2, result.Count);
    Assert.Contains(result, r => r.Name == "Metro Manila (NCR)");
    Assert.Contains(result, r => r.Name == "Calabarzon");
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
  public async Task GetByIdAsync_ExistingId_ReturnsMatchingRegion()
  {
    // Arrange
    var region = new Region { Name = "Calabarzon", PsgcCode = "40000000" };
    _context.Regions.Add(region);
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetByIdAsync(region.Id);

    // Assert
    Assert.NotNull(result);
    Assert.Equal("Calabarzon", result.Name);
  }

  [Fact]
  public async Task UpdateAsync_ValidUpdate_PersistsChanges()
  {
    // Arrange
    var region = new Region { Name = "Old Name", PsgcCode = "111111111" };
    _context.Regions.Add(region);
    await _context.SaveChangesAsync();

    var dto = new RegionCreateDto
    {
      Name = "New Name",
      PsgcCode = "222222222"
    };

    // Act
    var result = await _service.UpdateAsync(region.Id, dto);

    // Assert
    Assert.NotNull(result);
    Assert.Equal("New Name", result.Name);
    Assert.Equal("222222222", result.PsgcCode);

    var savedRegion = await _context.Regions.FirstAsync(r => r.Id == region.Id);
    Assert.Equal("New Name", savedRegion.Name);
  }

  [Fact]
  public async Task UpdateAsync_NonExistentId_ReturnsNull()
  {
    // Arrange
    var dto = new RegionCreateDto { Name = "Doesn't Matter" };

    // Act
    var result = await _service.UpdateAsync(999, dto);

    // Assert
    Assert.Null(result);
  }

  [Fact]
  public async Task DeleteAsync_ExistingRegion_RemovesRowEntirely()
  {
    // Arrange
    var region = new Region { Name = "To Be Deleted" };
    _context.Regions.Add(region);
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.DeleteAsync(region.Id);

    // Assert
    Assert.True(result);
    Assert.False(await _context.Regions.AnyAsync(r => r.Id == region.Id));
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