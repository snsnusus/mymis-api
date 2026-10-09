using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;
using MyMIS.Api.Services;

namespace MyMIS.Api.Tests.Services;

public class HmoProviderServiceTests : IDisposable
{
  private readonly AppDbContext _context;
  private readonly HmoProviderService _service;

  public HmoProviderServiceTests()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    _context = new AppDbContext(options);
    _service = new HmoProviderService(_context);
  }

  public void Dispose()
  {
    _context.Dispose();
    GC.SuppressFinalize(this);
  }

  // ---------------- helpers ----------------

  // Fixed UTC dates so "older" and "newer" never depend on the clock. Day(1) = 2026-01-01.
  private static DateTime Day(int day) =>
    new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(day - 1);

  private static HmoProviderCreateDto NewCreateDto(string code = "MAXI", string name = "Maxicare") => new()
  {
    Code = code,
    Name = name,
    ContractStartDate = new DateOnly(2026, 1, 1),
    ContractEndDate = new DateOnly(2026, 12, 31),
  };

  private static HmoProviderUpdateDto NewUpdateDto(string code = "MAXI", string name = "Maxicare") => new()
  {
    Code = code,
    Name = name,
    ContractStartDate = new DateOnly(2026, 1, 1),
    ContractEndDate = new DateOnly(2026, 12, 31),
  };

  private static HmoPlanFieldsDto NewPlanFields(string name) => new()
  {
    Name = name,
    Tier = HmoPlanTier.Executive,
    RoomType = HmoRoomType.RegularPrivate,
    MaximumBenefitLimit = 500_000m,
    PremiumFrequency = HmoPremiumFrequency.Annual,
    PremiumCost = 30_000m,
    EmployerSubsidyPercentage = 100m,
  };

  // Inserts straight into the database. NormalizedName must be set here, because
  // the duplicate-name check reads that column.
  private async Task<HmoProvider> SeedProviderAsync(string code, string name, DateTime? createdAt = null)
  {
    var when = createdAt ?? Day(1);

    var provider = new HmoProvider
    {
      Code = code,
      Name = name,
      NormalizedName = name.Trim().ToUpperInvariant(),
      ContractStartDate = new DateOnly(2026, 1, 1),
      ContractEndDate = new DateOnly(2026, 12, 31),
      CreatedAt = when,
      UpdatedAt = when,
    };

    _context.HmoProviders.Add(provider);
    await _context.SaveChangesAsync();
    return provider;
  }

  private Task<HmoProvider> SavedProviderAsync(int id) =>
    _context.HmoProviders.AsNoTracking().FirstAsync(p => p.Id == id);

  // ---------------- CreateAsync ----------------

  [Fact]
  public async Task CreateAsync_ValidProvider_PersistsAndReturnsCorrectData()
  {
    // Arrange
    var dto = NewCreateDto("MAXI", "Maxicare");
    dto.AccountManagerName = "Ana Santos";
    dto.Hotline = "02-8888-1234";
    dto.SupportEmail = "support@maxicare.example";
    dto.WebsiteUrl = "https://maxicare.example";
    dto.IsActive = false;

    // Act
    var result = await _service.CreateAsync(dto);

    // Assert: returned data
    Assert.True(result.Id > 0);
    Assert.Equal("MAXI", result.Code);
    Assert.Equal("Maxicare", result.Name);
    Assert.Equal("Ana Santos", result.AccountManagerName);
    Assert.Equal("02-8888-1234", result.Hotline);
    Assert.Equal("support@maxicare.example", result.SupportEmail);
    Assert.Equal("https://maxicare.example", result.WebsiteUrl);
    Assert.Equal(new DateOnly(2026, 1, 1), result.ContractStartDate);
    Assert.Equal(new DateOnly(2026, 12, 31), result.ContractEndDate);
    Assert.False(result.IsActive);

    // Assert: saved row
    var saved = await SavedProviderAsync(result.Id);
    Assert.Equal("Maxicare", saved.Name);
    Assert.False(saved.IsActive);
  }

  [Fact]
  public async Task CreateAsync_FieldsWithWhitespaceAndBlanks_AreStoredNormalized()
  {
    // Arrange
    var dto = NewCreateDto("  maxi  ", "  Maxicare  ");
    dto.AccountManagerName = "   ";
    dto.Hotline = " 02-8888-1234 ";
    dto.SupportEmail = "";
    dto.WebsiteUrl = null;

    // Act
    var result = await _service.CreateAsync(dto);

    // Assert
    var saved = await SavedProviderAsync(result.Id);
    Assert.Equal("MAXI", saved.Code);
    Assert.Equal("Maxicare", saved.Name);
    Assert.Equal("MAXICARE", saved.NormalizedName);
    Assert.Null(saved.AccountManagerName);
    Assert.Equal("02-8888-1234", saved.Hotline);
    Assert.Null(saved.SupportEmail);
    Assert.Null(saved.WebsiteUrl);
  }

  [Fact]
  public async Task CreateAsync_ValidProvider_SetsEqualRecentTimestamps()
  {
    // Act
    var result = await _service.CreateAsync(NewCreateDto());

    // Assert
    Assert.Equal(result.CreatedAt, result.UpdatedAt);
    Assert.True(result.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
  }

  [Fact]
  public async Task CreateAsync_WithNestedPlans_SavesThemUnderTheProviderWithTheirCoverage()
  {
    // Arrange
    var gold = NewPlanFields("Gold");
    gold.CoverageGroups =
    [
      new HmoPlanCoverageGroupDto
      {
        Category = "Hospitalization",
        Items = [new HmoPlanCoverageItemDto { Name = "Room and board" }],
      },
    ];

    var dto = NewCreateDto();
    dto.Plans = [gold, NewPlanFields("Silver")];

    // Act
    var result = await _service.CreateAsync(dto);

    // Assert
    var plans = await _context.HmoPlans
        .AsNoTracking()
        .Where(p => p.HmoProviderId == result.Id)
        .OrderBy(p => p.Name)
        .ToListAsync();

    Assert.Equal(new[] { "Gold", "Silver" }, plans.Select(p => p.Name));
    Assert.Single(await _context.HmoPlanCoverages.AsNoTracking().ToListAsync());
  }

  // ---------------- reads ----------------

  [Fact]
  public async Task GetAllAsync_NoProviders_ReturnsEmptyList()
  {
    Assert.Empty(await _service.GetAllAsync());
  }

  [Fact]
  public async Task GetAllAsync_SeveralProviders_ReturnsThemOrderedByName()
  {
    // Arrange: seeded out of order on purpose
    await SeedProviderAsync("MEDI", "Medicard");
    await SeedProviderAsync("MAXI", "Maxicare");
    await SeedProviderAsync("INTE", "Intellicare");

    // Act
    var result = await _service.GetAllAsync();

    // Assert
    Assert.Equal(
      new[] { "Intellicare", "Maxicare", "Medicard" },
      result.Select(p => p.Name));
  }

  [Fact]
  public async Task GetByIdAsync_ExistingProvider_ReturnsIt()
  {
    // Arrange
    var seed = await SeedProviderAsync("MAXI", "Maxicare");

    // Act
    var result = await _service.GetByIdAsync(seed.Id);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(seed.Id, result.Id);
    Assert.Equal("MAXI", result.Code);
    Assert.Equal("Maxicare", result.Name);
  }

  [Fact]
  public async Task GetByIdAsync_NonExistentId_ReturnsNull()
  {
    Assert.Null(await _service.GetByIdAsync(9999));
  }

  // ---------------- UpdateAsync ----------------

  [Fact]
  public async Task UpdateAsync_NonExistentId_ReturnsNull()
  {
    Assert.Null(await _service.UpdateAsync(9999, NewUpdateDto()));
  }

  [Fact]
  public async Task UpdateAsync_ValidUpdate_PersistsChangesAndRefreshesNormalizedName()
  {
    // Arrange
    var seed = await SeedProviderAsync("MAXI", "Maxicare");
    var dto = NewUpdateDto("  medi  ", "  Medicard  ");
    dto.AccountManagerName = "  Bea Cruz  ";
    dto.Hotline = "   ";
    dto.ContractEndDate = new DateOnly(2027, 6, 30);
    dto.IsActive = false;

    // Act
    var result = await _service.UpdateAsync(seed.Id, dto);

    // Assert
    Assert.NotNull(result);
    var saved = await SavedProviderAsync(seed.Id);
    Assert.Equal("MEDI", saved.Code);
    Assert.Equal("Medicard", saved.Name);
    Assert.Equal("MEDICARD", saved.NormalizedName);
    Assert.Equal("Bea Cruz", saved.AccountManagerName);
    Assert.Null(saved.Hotline);
    Assert.Equal(new DateOnly(2027, 6, 30), saved.ContractEndDate);
    Assert.False(saved.IsActive);
  }

  [Fact]
  public async Task UpdateAsync_ValidUpdate_MovesUpdatedAtButNotCreatedAt()
  {
    // Arrange
    var seed = await SeedProviderAsync("MAXI", "Maxicare", createdAt: Day(1));

    // Act
    var result = await _service.UpdateAsync(seed.Id, NewUpdateDto());

    // Assert
    Assert.NotNull(result);
    var saved = await SavedProviderAsync(seed.Id);
    Assert.True(saved.UpdatedAt > Day(1));
    Assert.Equal(Day(1), saved.CreatedAt);
  }

  // ---------------- CodeExistsAsync ----------------

  [Fact]
  public async Task CodeExistsAsync_SameCodeWithDifferentCaseAndWhitespace_ReturnsTrue()
  {
    // Arrange
    await SeedProviderAsync("MAXI", "Maxicare");

    // Act + Assert
    Assert.True(await _service.CodeExistsAsync("  maxi "));
  }

  [Fact]
  public async Task CodeExistsAsync_NewCode_ReturnsFalse()
  {
    // Arrange
    await SeedProviderAsync("MAXI", "Maxicare");

    // Act + Assert
    Assert.False(await _service.CodeExistsAsync("MEDI"));
  }

  [Fact]
  public async Task CodeExistsAsync_CodeOfTheExcludedProvider_ReturnsFalse()
  {
    // Arrange: updating a provider that keeps its own code is not a conflict
    var seed = await SeedProviderAsync("MAXI", "Maxicare");

    // Act + Assert
    Assert.False(await _service.CodeExistsAsync("MAXI", excludeId: seed.Id));
  }

  [Fact]
  public async Task CodeExistsAsync_CodeOfADifferentProviderThanTheExcludedOne_ReturnsTrue()
  {
    // Arrange
    await SeedProviderAsync("MAXI", "Maxicare");
    var medicard = await SeedProviderAsync("MEDI", "Medicard");

    // Act + Assert: Medicard is being changed to Maxicare's code
    Assert.True(await _service.CodeExistsAsync("MAXI", excludeId: medicard.Id));
  }

  // ---------------- NameExistsAsync ----------------

  [Fact]
  public async Task NameExistsAsync_SameNameWithDifferentCaseAndWhitespace_ReturnsTrue()
  {
    // Arrange
    await SeedProviderAsync("MAXI", "Maxicare");

    // Act + Assert
    Assert.True(await _service.NameExistsAsync("  maxiCARE "));
  }

  [Fact]
  public async Task NameExistsAsync_NewName_ReturnsFalse()
  {
    // Arrange
    await SeedProviderAsync("MAXI", "Maxicare");

    // Act + Assert
    Assert.False(await _service.NameExistsAsync("Medicard"));
  }

  [Fact]
  public async Task NameExistsAsync_NameOfTheExcludedProvider_ReturnsFalse()
  {
    // Arrange
    var seed = await SeedProviderAsync("MAXI", "Maxicare");

    // Act + Assert
    Assert.False(await _service.NameExistsAsync("Maxicare", excludeId: seed.Id));
  }

  [Fact]
  public async Task NameExistsAsync_NameOfADifferentProviderThanTheExcludedOne_ReturnsTrue()
  {
    // Arrange
    await SeedProviderAsync("MAXI", "Maxicare");
    var medicard = await SeedProviderAsync("MEDI", "Medicard");

    // Act + Assert: Medicard is being renamed to "Maxicare"
    Assert.True(await _service.NameExistsAsync("Maxicare", excludeId: medicard.Id));
  }
}