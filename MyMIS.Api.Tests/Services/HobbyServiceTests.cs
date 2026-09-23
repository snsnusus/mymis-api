using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.Services;
using MyMIS.Api.Models;

namespace MyMIS.Api.Tests.Services;

public class HobbyServiceTests : IDisposable
{
  private readonly AppDbContext _context;
  private readonly HobbyService _service;

  public HobbyServiceTests()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    _context = new AppDbContext(options);
    _service = new HobbyService(_context);
  }

  public void Dispose()
  {
    _context.Dispose();
    GC.SuppressFinalize(this);
  }

  [Fact]
  public async Task GetOrCreateHobbyAsync_BrandNewName_CreatesTitleCasedHobbyAndSavesIt()
  {
    // Arrange
    var hobbyName = "reading a book";

    // Act
    var result = await _service.GetOrCreateHobbyAsync(hobbyName);

    // Assert
    Assert.NotNull(result);
    Assert.True(result.Id > 0);
    Assert.Equal("Reading A Book", result.Name);
    Assert.Equal("READING A BOOK", result.NormalizedName);

    var savedHobby = await _context.Hobbies.FindAsync(result.Id);
    Assert.NotNull(savedHobby);
    Assert.Equal("Reading A Book", savedHobby.Name);
    Assert.Equal("READING A BOOK", savedHobby.NormalizedName);
  }

  [Fact]
  public async Task GetOrCreateHobbyAsync_ExistingHobbyDifferentCasing_DoesntCreateDuplicate()
  {
    // Arrange
    var existingHobby = new Hobby { Name = "Reading", NormalizedName = "READING" };
    _context.Hobbies.Add(existingHobby);
    await _context.SaveChangesAsync();

    var sameHobby = "ReAdiNg";

    // Act
    var result = await _service.GetOrCreateHobbyAsync(sameHobby);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(existingHobby.Id, result.Id);

    var savedHobbyCount = await _context.Hobbies.CountAsync();
    Assert.Equal(1, savedHobbyCount);
  }

  [Fact]
  public async Task GetOrCreateHobbyAsync_ExistingHobbySameCasing_DoesntCreateDuplicate()
  {
    // Arrange
    var existingHobby = new Hobby { Name = "Reading", NormalizedName = "READING" };
    _context.Hobbies.Add(existingHobby);
    await _context.SaveChangesAsync();

    var sameHobby = "Reading";

    // Act
    var result = await _service.GetOrCreateHobbyAsync(sameHobby);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(existingHobby.Id, result.Id);

    var savedHobbyCount = await _context.Hobbies.CountAsync();
    Assert.Equal(1, savedHobbyCount);
  }

  [Fact]
  public async Task GetOrCreateHobbyAsync_WhiteSpaceOnlyInput_ThrowsArgumentException()
  {
    // Arrange
    var whitespaceOnlyName = "   ";

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ArgumentException>(
        async () => await _service.GetOrCreateHobbyAsync(whitespaceOnlyName));

    Assert.Equal("hobbyName", exception.ParamName);
  }

  [Fact]
  public async Task GetOrCreateHobbyAsync_NameWithLeadingAndTrailingWhitespace_GetsStrippedAndSaved()
  {
    // Arrange
    var hobbyName = "   Reading   ";

    // Act
    var result = await _service.GetOrCreateHobbyAsync(hobbyName);

    // Assert
    Assert.NotNull(result);
    Assert.True(result.Id > 0);
    Assert.Equal("Reading", result.Name);
    Assert.Equal("READING", result.NormalizedName);

    var savedHobby = await _context.Hobbies.FindAsync(result.Id);
    Assert.NotNull(savedHobby);
    Assert.Equal("Reading", savedHobby.Name);
    Assert.Equal("READING", savedHobby.NormalizedName);
  }
}