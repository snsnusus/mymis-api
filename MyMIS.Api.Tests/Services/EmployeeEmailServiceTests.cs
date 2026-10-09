using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;
using MyMIS.Api.Models;
using MyMIS.Api.Services;
using MyMIS.Api.Tests.TestData;

namespace MyMIS.Api.Tests.Services;

public class EmployeeEmailServiceTests : IDisposable
{
  private readonly AppDbContext _context;
  private readonly EmployeeEmailService _service;
  private readonly Employee _juan;
  private readonly Employee _maria;


  public EmployeeEmailServiceTests()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options;

    _context = new AppDbContext(options);
    _service = new EmployeeEmailService(_context);

    _juan = TestEmployees.New("Juan", "Cruz", "EMP-001");
    _maria = TestEmployees.New("Maria", "Reyes", "EMP-002");

    _context.Employees.AddRange(_juan, _maria);
    _context.SaveChanges();
  }

  public void Dispose()
  {
    _context.Dispose();
    GC.SuppressFinalize(this);
  }


  // Fixed UTC dates so "older" and "newer" never depend on the clock. Day(1) = 2026-01-01.
  private static DateTime Day(int day) =>
    new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(day - 1);

  public static EmployeeEmailCreateDto NewDto(
    string email = "juan@example.com",
    ContactOwnership ownership = ContactOwnership.Personal,
    bool isPrimary = false
  ) => new()
  {
    Ownership = ownership,
    Email = email,
    IsPrimary = isPrimary,
  };

  public Task<List<EmployeeEmail>> SavedEmailsAsync(int employeeId) =>
    _context.EmployeeEmails
      .AsNoTracking()
      .Where(a => a.EmployeeId == employeeId)
      .ToListAsync();

  // Inserts straight into the database so a test's starting state doesn't depend on CreateAsync.
  private async Task<EmployeeEmail> SeedEmailAsync(
    Employee employee,
    string email,
    bool isPrimary,
    DateTime createdAt,
    ContactOwnership ownership = ContactOwnership.Personal,
    DateTime? deletedAt = null)
  {
    var entity = new EmployeeEmail
    {
      EmployeeId = employee.Id,
      Ownership = ownership,
      Email = email,
      IsPrimary = isPrimary,
      CreatedAt = createdAt,
      UpdatedAt = createdAt,
      DeletedAt = deletedAt,
    };

    _context.EmployeeEmails.Add(entity);
    await _context.SaveChangesAsync();
    return entity;
  }

  // Bypasses the soft-delete query filter, so deleted rows are visible.
  private Task<List<EmployeeEmail>> AllEmailsIncludingDeletedAsync(int employeeId) =>
    _context.EmployeeEmails
      .IgnoreQueryFilters()
      .AsNoTracking()
      .Where(ee => ee.EmployeeId == employeeId)
      .ToListAsync();

  [Fact]
  public async Task CreateAsync_EmployeeDoesNotExist_ReturnsNotFound()
  {
    // Arrange
    var employeeId = 9999;

    // Act
    var result = await _service.CreateAsync(employeeId, NewDto());

    // Assert
    Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
    var saved = await SavedEmailsAsync(employeeId);

    Assert.Empty(saved);
  }

  [Fact]
  public async Task CreateAsync_FirstEmailNotMarkedPrimary_StillBecomesPrimary()
  {
    // Act
    var result = await _service.CreateAsync(_juan.Id, NewDto(isPrimary: false));

    // Assert
    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Value);
    Assert.True(result.Value.IsPrimary);

    var savedEmail = Assert.Single(await SavedEmailsAsync(_juan.Id));
    Assert.True(savedEmail.IsPrimary);
  }

  [Fact]
  public async Task CreateAsync_EmailWithWhitespaceAndUppercase_IsStoredNormalized()
  {
    // Act
    var result = await _service.CreateAsync(_juan.Id, NewDto("  JDoe@Example.COM  "));

    // Assert
    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Value);
    Assert.Equal("jdoe@example.com", result.Value.Email);

    var savedEmail = Assert.Single(await SavedEmailsAsync(_juan.Id));
    Assert.Equal("jdoe@example.com", savedEmail.Email);
  }

  [Fact]
  public async Task CreateAsync_NewEmailMarkedPrimary_TakesOverFromExistingPrimary()
  {
    // Arrange
    var seed = await SeedEmailAsync(_juan, email: "juan@example.com", isPrimary: true, ownership: ContactOwnership.Personal, createdAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    // Act
    var result = await _service.CreateAsync(_juan.Id, NewDto("juan.work@example.com", isPrimary: true));

    // Assert
    Assert.True(result.IsSuccess);

    var saved = await SavedEmailsAsync(_juan.Id);
    Assert.Equal(2, saved.Count);
    Assert.False(saved.Single(e => e.Id == seed.Id).IsPrimary);

    var savedEmail = Assert.Single(saved, e => e.IsPrimary);
    Assert.Equal("juan.work@example.com", savedEmail.Email);
  }

  [Fact]
  public async Task CreateAsync_NewEmailNotMarkedPrimary_KeepsExistingPrimary()
  {
    // Arrange
    var seed = await SeedEmailAsync(_juan, email: "juan@example.com", isPrimary: true, ownership: ContactOwnership.Personal, createdAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    // Act
    var result = await _service.CreateAsync(_juan.Id, NewDto("juan.work@example.com", isPrimary: false));

    // Assert
    Assert.True(result.IsSuccess);

    var saved = await SavedEmailsAsync(_juan.Id);
    Assert.Equal(2, saved.Count);

    var savedEmail = Assert.Single(saved, e => e.IsPrimary);
    Assert.Equal("juan@example.com", savedEmail.Email);
  }

  [Fact]
  public async Task CreateAsync_InvalidEmail_ReturnsValidationAndSavesNothing()
  {
    // Act
    var result = await _service.CreateAsync(_juan.Id, NewDto("jdoe@gmail"));

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
    Assert.Empty(await SavedEmailsAsync(_juan.Id));
  }

  [Fact]
  public async Task CreateAsync_EmailUsedByAnotherEmployee_ReturnsConflict()
  {
    // Arrange
    await SeedEmailAsync(_maria, "shared@example.com", isPrimary: true, createdAt: Day(1));

    // Act
    var result = await _service.CreateAsync(_juan.Id, NewDto("shared@example.com"));

    // Assert
    Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
    Assert.Empty(await SavedEmailsAsync(_juan.Id));
  }

  [Fact]
  public async Task CreateAsync_SameEmailWithDifferentCaseAndWhitespace_ReturnsConflict()
  {
    // Arrange
    await SeedEmailAsync(_maria, "shared@example.com", isPrimary: true, createdAt: Day(1));

    // Act: normalization must happen BEFORE the duplicate check
    var result = await _service.CreateAsync(_juan.Id, NewDto("  SHARED@Example.COM  "));

    // Assert
    Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
    Assert.Empty(await SavedEmailsAsync(_juan.Id));
  }

  [Fact]
  public async Task CreateAsync_EmailOfSoftDeletedRow_IsReusable()
  {
    // Arrange: Maria's old address was soft-deleted, so it's free again
    await SeedEmailAsync(_maria, "old@example.com", isPrimary: false, createdAt: Day(1), deletedAt: Day(2));

    // Act
    var result = await _service.CreateAsync(_juan.Id, NewDto("old@example.com"));

    // Assert
    Assert.True(result.IsSuccess);
    var saved = Assert.Single(await SavedEmailsAsync(_juan.Id));
    Assert.Equal("old@example.com", saved.Email);
  }

  [Fact]
  public async Task CreateAsync_Ownership_IsSaved()
  {
    // Act
    var result = await _service.CreateAsync(_juan.Id, NewDto(ownership: ContactOwnership.Corporate));

    // Assert
    Assert.True(result.IsSuccess);
    var saved = Assert.Single(await SavedEmailsAsync(_juan.Id));
    Assert.Equal(ContactOwnership.Corporate, saved.Ownership);
  }

  // ---------------- UpdateAsync ----------------
  // Note: when the seeded email is primary, the update DTO must also say isPrimary: true,
  // otherwise the "can't un-mark the primary" rule fires first and hides what the test is about.

  [Fact]
  public async Task UpdateAsync_EmailDoesNotExist_ReturnsNotFound()
  {
    // Act
    var result = await _service.UpdateAsync(_juan.Id, 9999, NewDto(isPrimary: true));

    // Assert
    Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
  }

  [Fact]
  public async Task UpdateAsync_EmailBelongsToAnotherEmployee_ReturnsNotFoundAndLeavesItUnchanged()
  {
    // Arrange
    var mariaEmail = await SeedEmailAsync(_maria, "maria@example.com", isPrimary: true, createdAt: Day(1));

    // Act: Juan tries to edit Maria's email
    var result = await _service.UpdateAsync(_juan.Id, mariaEmail.Id, NewDto("hijacked@example.com", isPrimary: true));

    // Assert
    Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
    var saved = Assert.Single(await SavedEmailsAsync(_maria.Id));
    Assert.Equal("maria@example.com", saved.Email);
  }

  [Fact]
  public async Task UpdateAsync_UnmarkingThePrimary_ReturnsValidationAndKeepsItPrimary()
  {
    // Arrange
    var seed = await SeedEmailAsync(_juan, "juan@example.com", isPrimary: true, createdAt: Day(1));

    // Act
    var result = await _service.UpdateAsync(_juan.Id, seed.Id, NewDto("juan@example.com", isPrimary: false));

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
    var saved = Assert.Single(await SavedEmailsAsync(_juan.Id));
    Assert.True(saved.IsPrimary);
  }

  [Fact]
  public async Task UpdateAsync_InvalidEmail_ReturnsValidationAndKeepsOldEmail()
  {
    // Arrange
    var seed = await SeedEmailAsync(_juan, "juan@example.com", isPrimary: true, createdAt: Day(1));

    // Act
    var result = await _service.UpdateAsync(_juan.Id, seed.Id, NewDto("jdoe@gmail", isPrimary: true));

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
    var saved = Assert.Single(await SavedEmailsAsync(_juan.Id));
    Assert.Equal("juan@example.com", saved.Email);
  }

  [Fact]
  public async Task UpdateAsync_EmailHeldByAnotherEmployee_ReturnsConflictAndKeepsOldEmail()
  {
    // Arrange
    var seed = await SeedEmailAsync(_juan, "juan@example.com", isPrimary: true, createdAt: Day(1));
    await SeedEmailAsync(_maria, "maria@example.com", isPrimary: true, createdAt: Day(1));

    // Act
    var result = await _service.UpdateAsync(_juan.Id, seed.Id, NewDto("maria@example.com", isPrimary: true));

    // Assert
    Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
    var saved = Assert.Single(await SavedEmailsAsync(_juan.Id));
    Assert.Equal("juan@example.com", saved.Email);
  }

  [Fact]
  public async Task UpdateAsync_KeepingItsOwnAddress_IsNotAConflict()
  {
    // Arrange
    var seed = await SeedEmailAsync(_juan, "juan@example.com", isPrimary: true, createdAt: Day(1));

    // Act: same address, only the ownership changes (proves excludeEmailId works)
    var result = await _service.UpdateAsync(
      _juan.Id, seed.Id, NewDto("juan@example.com", ownership: ContactOwnership.Corporate, isPrimary: true));

    // Assert
    Assert.True(result.IsSuccess);
    var saved = Assert.Single(await SavedEmailsAsync(_juan.Id));
    Assert.Equal(ContactOwnership.Corporate, saved.Ownership);
  }

  [Fact]
  public async Task UpdateAsync_NonPrimaryBecomingPrimary_TakesOverFromTheOldPrimary()
  {
    // Arrange
    var first = await SeedEmailAsync(_juan, "juan@example.com", isPrimary: true, createdAt: Day(1));
    var second = await SeedEmailAsync(_juan, "juan.work@example.com", isPrimary: false, createdAt: Day(2));

    // Act
    var result = await _service.UpdateAsync(_juan.Id, second.Id, NewDto("juan.work@example.com", isPrimary: true));

    // Assert
    Assert.True(result.IsSuccess);
    var saved = await SavedEmailsAsync(_juan.Id);
    Assert.Equal(2, saved.Count);
    Assert.False(saved.Single(e => e.Id == first.Id).IsPrimary);
    var primary = Assert.Single(saved, e => e.IsPrimary);
    Assert.Equal(second.Id, primary.Id);
  }

  [Fact]
  public async Task UpdateAsync_EmailWithWhitespaceAndUppercase_IsStoredNormalized()
  {
    // Arrange
    var seed = await SeedEmailAsync(_juan, "juan@example.com", isPrimary: true, createdAt: Day(1));

    // Act
    var result = await _service.UpdateAsync(_juan.Id, seed.Id, NewDto("  NEW@Example.COM  ", isPrimary: true));

    // Assert
    Assert.True(result.IsSuccess);
    var saved = Assert.Single(await SavedEmailsAsync(_juan.Id));
    Assert.Equal("new@example.com", saved.Email);
  }

  [Fact]
  public async Task UpdateAsync_ValidChange_SavesFieldsAndMovesUpdatedAtButNotCreatedAt()
  {
    // Arrange
    var seed = await SeedEmailAsync(_juan, "juan@example.com", isPrimary: true, createdAt: Day(1));

    // Act
    var result = await _service.UpdateAsync(
      _juan.Id, seed.Id, NewDto("juan.new@example.com", ownership: ContactOwnership.Corporate, isPrimary: true));

    // Assert
    Assert.True(result.IsSuccess);
    var saved = Assert.Single(await SavedEmailsAsync(_juan.Id));
    Assert.Equal("juan.new@example.com", saved.Email);
    Assert.Equal(ContactOwnership.Corporate, saved.Ownership);
    Assert.True(saved.UpdatedAt > Day(1));
    Assert.Equal(Day(1), saved.CreatedAt);
  }

  // ---------------- DeleteAsync ----------------

  [Fact]
  public async Task DeleteAsync_EmailDoesNotExist_ReturnsNotFound()
  {
    // Act
    var result = await _service.DeleteAsync(_juan.Id, 9999);

    // Assert
    Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
  }

  [Fact]
  public async Task DeleteAsync_EmailBelongsToAnotherEmployee_ReturnsNotFoundAndKeepsIt()
  {
    // Arrange
    var mariaEmail = await SeedEmailAsync(_maria, "maria@example.com", isPrimary: true, createdAt: Day(1));

    // Act
    var result = await _service.DeleteAsync(_juan.Id, mariaEmail.Id);

    // Assert
    Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
    Assert.Single(await SavedEmailsAsync(_maria.Id));
  }

  [Fact]
  public async Task DeleteAsync_LastRemainingEmail_ReturnsValidationAndKeepsIt()
  {
    // Arrange
    var only = await SeedEmailAsync(_juan, "juan@example.com", isPrimary: true, createdAt: Day(1));

    // Act
    var result = await _service.DeleteAsync(_juan.Id, only.Id);

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
    Assert.Single(await SavedEmailsAsync(_juan.Id));
  }

  [Fact]
  public async Task DeleteAsync_NonPrimaryEmail_SoftDeletesItAndLeavesThePrimaryAlone()
  {
    // Arrange
    var primary = await SeedEmailAsync(_juan, "juan@example.com", isPrimary: true, createdAt: Day(1));
    var other = await SeedEmailAsync(_juan, "juan.work@example.com", isPrimary: false, createdAt: Day(2));

    // Act
    var result = await _service.DeleteAsync(_juan.Id, other.Id);

    // Assert: normal reads only see the primary, still primary
    Assert.True(result.IsSuccess);
    var live = Assert.Single(await SavedEmailsAsync(_juan.Id));
    Assert.Equal(primary.Id, live.Id);
    Assert.True(live.IsPrimary);

    // The deleted row still exists, marked deleted
    var all = await AllEmailsIncludingDeletedAsync(_juan.Id);
    Assert.Equal(2, all.Count);
    var deleted = all.Single(e => e.Id == other.Id);
    Assert.NotNull(deleted.DeletedAt);
    Assert.False(deleted.IsPrimary);
  }

  [Fact]
  public async Task DeleteAsync_PrimaryEmail_PromotesTheOldestRemaining()
  {
    // Arrange: seeded out of order on purpose; "c" (Day 3) is the oldest of the remaining two
    var primary = await SeedEmailAsync(_juan, "a@example.com", isPrimary: true, createdAt: Day(2));
    await SeedEmailAsync(_juan, "b@example.com", isPrimary: false, createdAt: Day(5));
    var oldest = await SeedEmailAsync(_juan, "c@example.com", isPrimary: false, createdAt: Day(3));

    // Act
    var result = await _service.DeleteAsync(_juan.Id, primary.Id);

    // Assert
    Assert.True(result.IsSuccess);
    var saved = await SavedEmailsAsync(_juan.Id);
    Assert.Equal(2, saved.Count);
    var newPrimary = Assert.Single(saved, e => e.IsPrimary);
    Assert.Equal(oldest.Id, newPrimary.Id);
  }

  // ---------------- reads ----------------

  [Fact]
  public async Task GetAllAsync_EmployeeDoesNotExist_ReturnsNull()
  {
    Assert.Null(await _service.GetAllAsync(9999));
  }

  [Fact]
  public async Task GetAllAsync_EmployeeWithNoEmails_ReturnsEmptyList()
  {
    // Act
    var result = await _service.GetAllAsync(_juan.Id);

    // Assert: empty list (employee exists), not null (employee missing)
    Assert.NotNull(result);
    Assert.Empty(result);
  }

  [Fact]
  public async Task GetAllAsync_SeveralEmails_PrimaryFirstThenOldest()
  {
    // Arrange: seeded in scrambled order
    await SeedEmailAsync(_juan, "b@example.com", isPrimary: false, createdAt: Day(3));
    await SeedEmailAsync(_juan, "a@example.com", isPrimary: true, createdAt: Day(5));
    await SeedEmailAsync(_juan, "c@example.com", isPrimary: false, createdAt: Day(1));

    // Act
    var result = await _service.GetAllAsync(_juan.Id);

    // Assert: primary first, then oldest first
    Assert.NotNull(result);
    Assert.Equal(
      new[] { "a@example.com", "c@example.com", "b@example.com" },
      result.Select(e => e.Email));
  }

  [Fact]
  public async Task GetAllAsync_ReturnsOnlyThatEmployeesLiveEmails()
  {
    // Arrange
    await SeedEmailAsync(_juan, "juan@example.com", isPrimary: true, createdAt: Day(1));
    await SeedEmailAsync(_juan, "deleted@example.com", isPrimary: false, createdAt: Day(2), deletedAt: Day(3));
    await SeedEmailAsync(_maria, "maria@example.com", isPrimary: true, createdAt: Day(1));

    // Act
    var result = await _service.GetAllAsync(_juan.Id);

    // Assert
    Assert.NotNull(result);
    var only = Assert.Single(result);
    Assert.Equal("juan@example.com", only.Email);
  }

  [Fact]
  public async Task GetByIdAsync_ExistingEmail_ReturnsIt()
  {
    // Arrange
    var seed = await SeedEmailAsync(_juan, "juan@example.com", isPrimary: true, createdAt: Day(1));

    // Act
    var result = await _service.GetByIdAsync(_juan.Id, seed.Id);

    // Assert
    Assert.NotNull(result);
    Assert.Equal("juan@example.com", result.Email);
    Assert.True(result.IsPrimary);
  }

  [Fact]
  public async Task GetByIdAsync_NonexistentId_ReturnsNull()
  {
    Assert.Null(await _service.GetByIdAsync(_juan.Id, 9999));
  }

  [Fact]
  public async Task GetByIdAsync_EmailOfAnotherEmployee_ReturnsNull()
  {
    // Arrange
    var mariaEmail = await SeedEmailAsync(_maria, "maria@example.com", isPrimary: true, createdAt: Day(1));

    // Act + Assert: the id exists, but not for Juan
    Assert.Null(await _service.GetByIdAsync(_juan.Id, mariaEmail.Id));
  }

  [Fact]
  public async Task GetByIdAsync_SoftDeletedEmail_ReturnsNull()
  {
    // Arrange
    var deleted = await SeedEmailAsync(_juan, "gone@example.com", isPrimary: false, createdAt: Day(1), deletedAt: Day(2));

    // Act + Assert
    Assert.Null(await _service.GetByIdAsync(_juan.Id, deleted.Id));
  }

  // ---------------- ValidateForNewEmployeeAsync ----------------

  [Fact]
  public async Task ValidateForNewEmployeeAsync_MoreThanOnePrimary_ReturnsValidation()
  {
    // Arrange
    List<EmployeeEmailCreateDto> dtos =
    [
      NewDto("a@example.com", isPrimary: true),
    NewDto("b@example.com", isPrimary: true),
  ];

    // Act
    var result = await _service.ValidateForNewEmployeeAsync(dtos);

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
    Assert.Equal("Only one email can be marked as primary.", result.ErrorMessage);
  }

  [Fact]
  public async Task ValidateForNewEmployeeAsync_InvalidEmail_ReturnsValidationNamingItsPosition()
  {
    // Arrange
    List<EmployeeEmailCreateDto> dtos =
    [
      NewDto("a@example.com"),
    NewDto("jdoe@gmail"),
  ];

    // Act
    var result = await _service.ValidateForNewEmployeeAsync(dtos);

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
    Assert.Equal("Email address 2 is not valid.", result.ErrorMessage);
  }

  [Fact]
  public async Task ValidateForNewEmployeeAsync_SameEmailTwiceInTheRequest_ReturnsValidation()
  {
    // Arrange: same address once normalized
    List<EmployeeEmailCreateDto> dtos =
    [
      NewDto("a@example.com"),
    NewDto("  A@Example.COM  "),
  ];

    // Act
    var result = await _service.ValidateForNewEmployeeAsync(dtos);

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
    Assert.Equal("Email address 2 repeats an email entered above.", result.ErrorMessage);
  }

  [Fact]
  public async Task ValidateForNewEmployeeAsync_EmailAlreadySaved_ReturnsConflict()
  {
    // Arrange
    await SeedEmailAsync(_maria, "taken@example.com", isPrimary: true, createdAt: Day(1));
    List<EmployeeEmailCreateDto> dtos = [NewDto("taken@example.com")];

    // Act
    var result = await _service.ValidateForNewEmployeeAsync(dtos);

    // Assert
    Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
  }

  [Fact]
  public async Task ValidateForNewEmployeeAsync_ValidList_Succeeds()
  {
    // Arrange
    List<EmployeeEmailCreateDto> dtos =
    [
      NewDto("a@example.com", isPrimary: true),
    NewDto("b@example.com"),
  ];

    // Act
    var result = await _service.ValidateForNewEmployeeAsync(dtos);

    // Assert
    Assert.True(result.IsSuccess);
  }

  // ---------------- NewEntitiesForNewEmployee (static, no database) ----------------

  [Fact]
  public void NewEntitiesForNewEmployee_NoneMarkedPrimary_FirstBecomesPrimary()
  {
    // Arrange
    List<EmployeeEmailCreateDto> dtos = [NewDto("a@example.com"), NewDto("b@example.com")];

    // Act
    var entities = EmployeeEmailService.NewEntitiesForNewEmployee(dtos);

    // Assert
    Assert.True(entities[0].IsPrimary);
    Assert.False(entities[1].IsPrimary);
  }

  [Fact]
  public void NewEntitiesForNewEmployee_SecondMarkedPrimary_OnlySecondIsPrimary()
  {
    // Arrange
    List<EmployeeEmailCreateDto> dtos = [NewDto("a@example.com"), NewDto("b@example.com", isPrimary: true)];

    // Act
    var entities = EmployeeEmailService.NewEntitiesForNewEmployee(dtos);

    // Assert
    Assert.False(entities[0].IsPrimary);
    Assert.True(entities[1].IsPrimary);
  }

  [Fact]
  public void NewEntitiesForNewEmployee_EmailsAreTrimmedAndLowercased_AndOwnershipKept()
  {
    // Arrange
    List<EmployeeEmailCreateDto> dtos = [NewDto("  JDoe@Example.COM  ", ownership: ContactOwnership.Corporate)];

    // Act
    var entities = EmployeeEmailService.NewEntitiesForNewEmployee(dtos);

    // Assert
    var entity = Assert.Single(entities);
    Assert.Equal("jdoe@example.com", entity.Email);
    Assert.Equal(ContactOwnership.Corporate, entity.Ownership);
  }

  [Fact]
  public void NewEntitiesForNewEmployee_InvalidEmail_Throws()
  {
    // Arrange: callers must run ValidateForNewEmployeeAsync first; this is the safety net
    List<EmployeeEmailCreateDto> dtos = [NewDto("jdoe@gmail")];

    // Act + Assert
    Assert.Throws<InvalidOperationException>(
      () => EmployeeEmailService.NewEntitiesForNewEmployee(dtos));
  }
}