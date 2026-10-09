using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;
using MyMIS.Api.Models;
using MyMIS.Api.Services;

namespace MyMIS.Api.Tests.Services;

public class HmoPlanServiceTests : IDisposable
{
  private readonly AppDbContext _context;
  private readonly HmoPlanService _service;

  public HmoPlanServiceTests()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    _context = new AppDbContext(options);
    _service = new HmoPlanService(_context);
  }

  public void Dispose()
  {
    _context.Dispose();
    GC.SuppressFinalize(this);
  }

  // ---------------- helpers ----------------

  private static DateTime Day(int day) =>
    new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(day - 1);

  private static HmoPlanCreateDto NewPlanDto(int providerId, string name = "Gold Plus") => new()
  {
    HmoProviderId = providerId,
    Name = name,
    Tier = HmoPlanTier.Executive,
    RoomType = HmoRoomType.RegularPrivate,
    MaximumBenefitLimit = 500_000m,
    PremiumFrequency = HmoPremiumFrequency.Annual,
    PremiumCost = 30_000m,
    EmployerSubsidyPercentage = 100m,
  };

  private async Task<HmoProvider> SeedProviderAsync(string code, string name)
  {
    var provider = new HmoProvider
    {
      Code = code,
      Name = name,
      NormalizedName = name.Trim().ToUpperInvariant(),
      ContractStartDate = new DateOnly(2026, 1, 1),
      ContractEndDate = new DateOnly(2026, 12, 31),
      CreatedAt = Day(1),
      UpdatedAt = Day(1),
    };

    _context.HmoProviders.Add(provider);
    await _context.SaveChangesAsync();
    return provider;
  }

  // Inserts straight into the database. NormalizedName must be set here, because
  // the duplicate-name check reads that column.
  private async Task<HmoPlan> SeedPlanAsync(
    HmoProvider provider,
    string name,
    DateTime? createdAt = null,
    bool pecCovered = false,
    decimal? pecLimit = null,
    List<HmoPlanCoverage>? coverages = null)
  {
    var when = createdAt ?? Day(1);

    var plan = new HmoPlan
    {
      HmoProviderId = provider.Id,
      Name = name,
      NormalizedName = name.Trim().ToUpperInvariant(),
      Tier = HmoPlanTier.Executive,
      RoomType = HmoRoomType.RegularPrivate,
      MaximumBenefitLimit = 500_000m,
      PremiumFrequency = HmoPremiumFrequency.Annual,
      PremiumCost = 30_000m,
      EmployerSubsidyPercentage = 100m,
      PecCovered = pecCovered,
      PecLimit = pecLimit,
      CreatedAt = when,
      UpdatedAt = when,
      Coverages = coverages ?? [],
    };

    _context.HmoPlans.Add(plan);
    await _context.SaveChangesAsync();
    return plan;
  }

  private Task<HmoPlan> SavedPlanAsync(int id) =>
    _context.HmoPlans.AsNoTracking().FirstAsync(p => p.Id == id);

  private Task<List<HmoPlanCoverage>> SavedCoveragesAsync(int planId) =>
    _context.HmoPlanCoverages
      .AsNoTracking()
      .Where(c => c.HmoPlanId == planId)
      .OrderBy(c => c.SortOrder)
      .ToListAsync();

  // ---------------- GetByProviderAsync ----------------

  [Fact]
  public async Task GetByProviderAsync_ProviderDoesNotExist_ReturnsNull()
  {
    Assert.Null(await _service.GetByProviderAsync(9999));
  }

  [Fact]
  public async Task GetByProviderAsync_ProviderWithNoPlans_ReturnsEmptyList()
  {
    // Arrange
    var provider = await SeedProviderAsync("MAXI", "Maxicare");

    // Act
    var result = await _service.GetByProviderAsync(provider.Id);

    // Assert: empty list (provider exists), not null (provider missing)
    Assert.NotNull(result);
    Assert.Empty(result);
  }

  [Fact]
  public async Task GetByProviderAsync_SeveralPlans_ReturnsOnlyThatProvidersPlansOrderedByName()
  {
    // Arrange: seeded out of order on purpose
    var maxicare = await SeedProviderAsync("MAXI", "Maxicare");
    var medicard = await SeedProviderAsync("MEDI", "Medicard");
    await SeedPlanAsync(maxicare, "Silver");
    await SeedPlanAsync(maxicare, "Gold");
    await SeedPlanAsync(medicard, "Platinum");

    // Act
    var result = await _service.GetByProviderAsync(maxicare.Id);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(new[] { "Gold", "Silver" }, result.Select(p => p.Name));
  }

  // ---------------- GetByIdAsync / ExistsAsync ----------------

  [Fact]
  public async Task GetByIdAsync_ExistingPlan_ReturnsItWithProviderName()
  {
    // Arrange
    var provider = await SeedProviderAsync("MAXI", "Maxicare");
    var plan = await SeedPlanAsync(provider, "Gold");

    // Act
    var result = await _service.GetByIdAsync(plan.Id);

    // Assert
    Assert.NotNull(result);
    Assert.Equal("Gold", result.Name);
    Assert.Equal(provider.Id, result.HmoProviderId);
    Assert.Equal("Maxicare", result.HmoProviderName);
  }

  [Fact]
  public async Task GetByIdAsync_NonExistentId_ReturnsNull()
  {
    Assert.Null(await _service.GetByIdAsync(9999));
  }

  [Fact]
  public async Task GetByIdAsync_CoverageSeededOutOfOrder_IsGroupedInSortOrder()
  {
    // Arrange: inserted scrambled; SortOrder decides the order
    var provider = await SeedProviderAsync("MAXI", "Maxicare");
    var plan = await SeedPlanAsync(provider, "Gold", coverages:
    [
      new HmoPlanCoverage { Category = "Outpatient", Name = "Consultation", SortOrder = 1 },
      new HmoPlanCoverage { Category = "Hospitalization", Name = "ICU", SortOrder = 2 },
      new HmoPlanCoverage { Category = "Hospitalization", Name = "Room", SortOrder = 0 },
    ]);

    // Act
    var result = await _service.GetByIdAsync(plan.Id);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(new[] { "Hospitalization", "Outpatient" }, result.CoverageGroups.Select(g => g.Category));
    Assert.Equal(new[] { "Room", "ICU" }, result.CoverageGroups[0].Items.Select(i => i.Name));
  }

  [Fact]
  public async Task ExistsAsync_ExistingAndMissingPlan_ReturnTrueAndFalse()
  {
    // Arrange
    var provider = await SeedProviderAsync("MAXI", "Maxicare");
    var plan = await SeedPlanAsync(provider, "Gold");

    // Act + Assert
    Assert.True(await _service.ExistsAsync(plan.Id));
    Assert.False(await _service.ExistsAsync(9999));
  }

  // ---------------- NameExistsAsync ----------------

  [Fact]
  public async Task NameExistsAsync_SameNameWithDifferentCaseAndWhitespace_ReturnsTrue()
  {
    // Arrange
    var provider = await SeedProviderAsync("MAXI", "Maxicare");
    await SeedPlanAsync(provider, "Gold Plus");

    // Act + Assert
    Assert.True(await _service.NameExistsAsync(provider.Id, "  gold PLUS "));
  }

  [Fact]
  public async Task NameExistsAsync_SameNameUnderAnotherProvider_ReturnsFalse()
  {
    // Arrange: plan names only have to be unique within one provider
    var maxicare = await SeedProviderAsync("MAXI", "Maxicare");
    var medicard = await SeedProviderAsync("MEDI", "Medicard");
    await SeedPlanAsync(maxicare, "Gold Plus");

    // Act + Assert
    Assert.False(await _service.NameExistsAsync(medicard.Id, "Gold Plus"));
  }

  [Fact]
  public async Task NameExistsAsync_NameOfTheExcludedPlan_ReturnsFalse()
  {
    // Arrange
    var provider = await SeedProviderAsync("MAXI", "Maxicare");
    var plan = await SeedPlanAsync(provider, "Gold Plus");

    // Act + Assert
    Assert.False(await _service.NameExistsAsync(provider.Id, "Gold Plus", excludeId: plan.Id));
  }

  [Fact]
  public async Task NameExistsAsync_NameOfADifferentPlanThanTheExcludedOne_ReturnsTrue()
  {
    // Arrange
    var provider = await SeedProviderAsync("MAXI", "Maxicare");
    await SeedPlanAsync(provider, "Gold Plus");
    var silver = await SeedPlanAsync(provider, "Silver");

    // Act + Assert: Silver is being renamed to "Gold Plus"
    Assert.True(await _service.NameExistsAsync(provider.Id, "Gold Plus", excludeId: silver.Id));
  }

  // ---------------- CreateAsync ----------------

  [Fact]
  public async Task CreateAsync_ProviderDoesNotExist_ReturnsValidationAndSavesNothing()
  {
    // Act
    var result = await _service.CreateAsync(NewPlanDto(9999));

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
    Assert.Empty(await _context.HmoPlans.AsNoTracking().ToListAsync());
  }

  [Fact]
  public async Task CreateAsync_ValidPlan_PersistsAndReturnsCorrectData()
  {
    // Arrange
    var provider = await SeedProviderAsync("MAXI", "Maxicare");
    var dto = NewPlanDto(provider.Id, "  Gold Plus  ");

    // Act
    var result = await _service.CreateAsync(dto);

    // Assert
    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Value);
    Assert.True(result.Value.Id > 0);
    Assert.Equal("Gold Plus", result.Value.Name);
    Assert.Equal("Maxicare", result.Value.HmoProviderName);
    Assert.Equal(result.Value.CreatedAt, result.Value.UpdatedAt);

    var saved = await SavedPlanAsync(result.Value.Id);
    Assert.Equal("Gold Plus", saved.Name);
    Assert.Equal("GOLD PLUS", saved.NormalizedName);
    Assert.Equal(HmoPlanTier.Executive, saved.Tier);
    Assert.Equal(500_000m, saved.MaximumBenefitLimit);
    Assert.Equal(30_000m, saved.PremiumCost);
  }

  [Fact]
  public async Task CreateAsync_SwitchesOff_SaveTheDependentValuesAsNull()
  {
    // Arrange: values are filled in, but the switches that own them are off
    var provider = await SeedProviderAsync("MAXI", "Maxicare");
    var dto = NewPlanDto(provider.Id);
    dto.PecCovered = false;
    dto.PecLimit = 5_000m;
    dto.AllowDependents = false;
    dto.DependentPremiumCost = 10_000m;
    dto.DependentSubsidyPercentage = 50m;

    // Act
    var result = await _service.CreateAsync(dto);

    // Assert
    Assert.True(result.IsSuccess);
    var saved = await SavedPlanAsync(result.Value!.Id);
    Assert.Null(saved.PecLimit);
    Assert.Null(saved.DependentPremiumCost);
    Assert.Null(saved.DependentSubsidyPercentage);
  }

  [Fact]
  public async Task CreateAsync_SwitchesOn_KeepTheDependentValues()
  {
    // Arrange
    var provider = await SeedProviderAsync("MAXI", "Maxicare");
    var dto = NewPlanDto(provider.Id);
    dto.PecCovered = true;
    dto.PecLimit = 5_000m;
    dto.AllowDependents = true;
    dto.DependentPremiumCost = 10_000m;
    dto.DependentSubsidyPercentage = 50m;

    // Act
    var result = await _service.CreateAsync(dto);

    // Assert
    Assert.True(result.IsSuccess);
    var saved = await SavedPlanAsync(result.Value!.Id);
    Assert.Equal(5_000m, saved.PecLimit);
    Assert.Equal(10_000m, saved.DependentPremiumCost);
    Assert.Equal(50m, saved.DependentSubsidyPercentage);
  }

  [Fact]
  public async Task CreateAsync_CoverageGroups_AreNumberedTrimmedAndBlankNotesBecomeNull()
  {
    // Arrange
    var provider = await SeedProviderAsync("MAXI", "Maxicare");
    var dto = NewPlanDto(provider.Id);
    dto.CoverageGroups =
    [
      new HmoPlanCoverageGroupDto
      {
        Category = "  Hospitalization  ",
        Items =
        [
          new HmoPlanCoverageItemDto { Name = "  Room  ", LimitAmount = 1_000m, Notes = "  per day  " },
          new HmoPlanCoverageItemDto { Name = "ICU", LimitAmount = null, Notes = "   " },
        ],
      },
      new HmoPlanCoverageGroupDto
      {
        Category = "Outpatient",
        Items = [new HmoPlanCoverageItemDto { Name = "Consultation" }],
      },
    ];

    // Act
    var result = await _service.CreateAsync(dto);

    // Assert: SortOrder runs 0, 1, 2 across both groups
    Assert.True(result.IsSuccess);
    var saved = await SavedCoveragesAsync(result.Value!.Id);
    Assert.Equal(new[] { 0, 1, 2 }, saved.Select(c => c.SortOrder));
    Assert.Equal(new[] { "Hospitalization", "Hospitalization", "Outpatient" }, saved.Select(c => c.Category));
    Assert.Equal(new[] { "Room", "ICU", "Consultation" }, saved.Select(c => c.Name));
    Assert.Equal("per day", saved[0].Notes);
    Assert.Null(saved[1].Notes);
    Assert.Equal(1_000m, saved[0].LimitAmount);
  }

  // ---------------- UpdateAsync ----------------

  [Fact]
  public async Task UpdateAsync_PlanDoesNotExist_ReturnsNotFound()
  {
    // Arrange
    var provider = await SeedProviderAsync("MAXI", "Maxicare");

    // Act
    var result = await _service.UpdateAsync(9999, NewPlanDto(provider.Id));

    // Assert
    Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
  }

  [Fact]
  public async Task UpdateAsync_ChangingTheProvider_ReturnsValidationAndLeavesThePlanUnchanged()
  {
    // Arrange
    var maxicare = await SeedProviderAsync("MAXI", "Maxicare");
    var medicard = await SeedProviderAsync("MEDI", "Medicard");
    var plan = await SeedPlanAsync(maxicare, "Gold");

    // Act: try to move the plan to another provider
    var result = await _service.UpdateAsync(plan.Id, NewPlanDto(medicard.Id, "Moved"));

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
    var saved = await SavedPlanAsync(plan.Id);
    Assert.Equal(maxicare.Id, saved.HmoProviderId);
    Assert.Equal("Gold", saved.Name);
  }

  [Fact]
  public async Task UpdateAsync_ValidUpdate_PersistsChangesAndRefreshesNormalizedName()
  {
    // Arrange
    var provider = await SeedProviderAsync("MAXI", "Maxicare");
    var plan = await SeedPlanAsync(provider, "Gold");
    var dto = NewPlanDto(provider.Id, "  Platinum  ");
    dto.Tier = HmoPlanTier.Managerial;
    dto.PremiumCost = 45_000m;

    // Act
    var result = await _service.UpdateAsync(plan.Id, dto);

    // Assert
    Assert.True(result.IsSuccess);
    var saved = await SavedPlanAsync(plan.Id);
    Assert.Equal("Platinum", saved.Name);
    Assert.Equal("PLATINUM", saved.NormalizedName);
    Assert.Equal(HmoPlanTier.Managerial, saved.Tier);
    Assert.Equal(45_000m, saved.PremiumCost);
  }

  [Fact]
  public async Task UpdateAsync_TurningASwitchOff_ClearsItsValue()
  {
    // Arrange
    var provider = await SeedProviderAsync("MAXI", "Maxicare");
    var plan = await SeedPlanAsync(provider, "Gold", pecCovered: true, pecLimit: 5_000m);
    var dto = NewPlanDto(provider.Id, "Gold");
    dto.PecCovered = false;
    dto.PecLimit = 5_000m;

    // Act
    var result = await _service.UpdateAsync(plan.Id, dto);

    // Assert
    Assert.True(result.IsSuccess);
    var saved = await SavedPlanAsync(plan.Id);
    Assert.False(saved.PecCovered);
    Assert.Null(saved.PecLimit);
  }

  [Fact]
  public async Task UpdateAsync_CoverageGroups_AreFullyReplacedWithNoLeftoverRows()
  {
    // Arrange
    var provider = await SeedProviderAsync("MAXI", "Maxicare");
    var plan = await SeedPlanAsync(provider, "Gold", coverages:
    [
      new HmoPlanCoverage { Category = "Old", Name = "Old item 1", SortOrder = 0 },
      new HmoPlanCoverage { Category = "Old", Name = "Old item 2", SortOrder = 1 },
    ]);

    var dto = NewPlanDto(provider.Id, "Gold");
    dto.CoverageGroups =
    [
      new HmoPlanCoverageGroupDto
      {
        Category = "New",
        Items = [new HmoPlanCoverageItemDto { Name = "New item" }],
      },
    ];

    // Act
    var result = await _service.UpdateAsync(plan.Id, dto);

    // Assert: exactly the new row remains
    Assert.True(result.IsSuccess);
    var saved = Assert.Single(await SavedCoveragesAsync(plan.Id));
    Assert.Equal("New item", saved.Name);
    Assert.Equal(0, saved.SortOrder);
  }

  [Fact]
  public async Task UpdateAsync_ValidUpdate_MovesUpdatedAtButNotCreatedAt()
  {
    // Arrange
    var provider = await SeedProviderAsync("MAXI", "Maxicare");
    var plan = await SeedPlanAsync(provider, "Gold", createdAt: Day(1));

    // Act
    var result = await _service.UpdateAsync(plan.Id, NewPlanDto(provider.Id, "Gold"));

    // Assert
    Assert.True(result.IsSuccess);
    var saved = await SavedPlanAsync(plan.Id);
    Assert.True(saved.UpdatedAt > Day(1));
    Assert.Equal(Day(1), saved.CreatedAt);
  }
}