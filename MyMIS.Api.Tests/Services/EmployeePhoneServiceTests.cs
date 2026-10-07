using Amazon.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;
using MyMIS.Api.Models;
using MyMIS.Api.Services;
using MyMIS.Api.Tests.TestData;

namespace MyMIS.Api.Tests.Services;

public class EmployeePhoneServiceTests : IDisposable
{
  private readonly AppDbContext _context;
  private readonly EmployeePhoneService _service;

  // Seeded once per test in the constructor.
  private readonly Employee _juan;
  private readonly Employee _maria;

  public EmployeePhoneServiceTests()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options;

    _context = new AppDbContext(options);
    _service = new EmployeePhoneService(_context);

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

  private static EmployeePhoneCreateDto NewDto(
      string number = "+639171234567",
      string countryCode = "PH",
      ContactOwnership ownership = ContactOwnership.Personal,
      bool isPrimary = false
    ) => new()
    {
      Ownership = ownership,
      Phone = new PhoneDto
      {
        CountryCode = countryCode,
        Number = number,
      },
      IsPrimary = isPrimary,
    };

  private async Task<EmployeePhone> SeedPhoneAsync(
    Employee employee,
    string number,
    bool isPrimary,
    DateTime createdAt,
    PhoneLineType lineType = PhoneLineType.Mobile,
    ContactOwnership ownership = ContactOwnership.Personal,
    string countryCode = "PH")
  {
    var phone = new EmployeePhone
    {
      EmployeeId = employee.Id,
      Ownership = ownership,
      LineType = lineType,
      Phone = new Phone
      {
        CountryCode = countryCode,
        Number = number,
      },
      IsPrimary = isPrimary,
      CreatedAt = createdAt,
      UpdatedAt = createdAt,
    };
    _context.EmployeePhones.Add(phone);
    await _context.SaveChangesAsync();
    return phone;
  }

  private Task<List<EmployeePhone>> SavedPhonesAsync(int employeeId) =>
    _context.EmployeePhones
      .AsNoTracking()
      .Where(ep => ep.EmployeeId == employeeId)
      .ToListAsync();

  // Fixed UTC dates for seeding, so "older" and "newer" are explicit and never
  // depend on when the test runs. Day(1) = 2026-01-01, Day(2) = 2026-01-02, etc.
  private static DateTime Day(int day) => new(2026, 1, day, 0, 0, 0, DateTimeKind.Utc);

  // ---------- CreateAsync ----------

  [Fact]
  public async Task CreateAsync_EmployeeDoesNotExist_ReturnsNotFound()
  {
    // Act
    var result = await _service.CreateAsync(999_999, NewDto());

    // Assert
    Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
  }

  [Fact]
  public async Task CreateAsync_FirstPhoneNotMarkedPrimary_StillBecomesPrimary()
  {
    // Act
    var result = await _service.CreateAsync(_juan.Id, NewDto(isPrimary: false));

    // Assert
    Assert.True(result.IsSuccess);
    Assert.True(result.Value!.IsPrimary);
  }

  [Fact]
  public async Task CreateAsync_SecondPhoneMarkedPrimary_TakesOverAsOnlyPrimary()
  {
    // Arrange
    var first = await SeedPhoneAsync(_juan, number: "+639191234567", isPrimary: true, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    // Act
    var result = await _service.CreateAsync(_juan.Id, NewDto(isPrimary: true));

    // Assert
    Assert.True(result.IsSuccess);

    var saved = await SavedPhonesAsync(_juan.Id);
    var primary = Assert.Single(saved, ep => ep.IsPrimary);
    Assert.Equal(result.Value!.Id, primary.Id);
    Assert.False(saved.Single(ep => ep.Id == first.Id).IsPrimary);
  }

  [Fact]
  public async Task CreateAsync_LandlineNumber_DetectsLandline()
  {
    // Act
    var result = await _service.CreateAsync(_juan.Id, NewDto("+63281234567"));

    // Assert
    Assert.True(result.IsSuccess);
    Assert.Equal(PhoneLineType.Landline, result.Value!.LineType);
  }

  [Fact]
  public async Task CreateAsync_NumberInvalidForCountry_ReturnsValidationAndSavesNothing()
  {
    // Act
    var result = await _service.CreateAsync(_juan.Id, NewDto(countryCode: "US"));

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);

    var saved = await SavedPhonesAsync(_juan.Id);
    Assert.Empty(saved);
  }

  [Fact]
  public async Task CreateAsync_MobileUsedByAnotherEmployee_ReturnsConflict()
  {
    // Arrange
    await SeedPhoneAsync(_maria, number: "+639171234567", isPrimary: true, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    // Act
    var result = await _service.CreateAsync(_juan.Id, NewDto("+639171234567"));

    // Assert
    Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);

    var saved = await SavedPhonesAsync(_juan.Id);
    Assert.Empty(saved);
  }

  [Fact]
  public async Task CreateAsync_LandlineUsedByAnotherEmployee_IsAllowed()
  {
    // Arrange
    await SeedPhoneAsync(_maria, number: "+63281234567", isPrimary: false, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), lineType: PhoneLineType.Landline);

    // Act
    var result = await _service.CreateAsync(_juan.Id, NewDto(number: "+63281234567"));

    // Assert
    Assert.True(result.IsSuccess);
    Assert.Equal(PhoneLineType.Landline, result.Value!.LineType);

    var saved = await SavedPhonesAsync(_juan.Id);

    Assert.Single(saved);
  }

  [Fact]
  public async Task CreateAsync_MobileOnlyOnSoftDeletedPhone_IsAllowed()
  {
    // Arrange
    var deletedPhone = await SeedPhoneAsync(_maria, number: "+639191234567", isPrimary: true, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    deletedPhone.DeletedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    await _context.SaveChangesAsync();

    // Act
    var result = await _service.CreateAsync(_juan.Id, NewDto(number: "+639191234567"));

    // Assert
    Assert.True(result.IsSuccess);

    var juanSaved = await SavedPhonesAsync(_juan.Id);

    Assert.Single(juanSaved);
  }

  // ---------- UpdateAsync ----------

  [Fact]
  public async Task UpdateAsync_PhoneBelongsToAnotherEmployee_ReturnsNotFound()
  {
    // Arrange: the phone exists, but it's Maria's
    var mariaPhone = await SeedPhoneAsync(_maria, number: "+639181234567", isPrimary: true, Day(1));

    // Act: Juan tries to edit it through his own employee id
    var result = await _service.UpdateAsync(_juan.Id, mariaPhone.Id, NewDto(isPrimary: true));

    // Assert: 404, not 403, so we don't reveal that the phone exists
    Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
  }

  [Fact]
  public async Task UpdateAsync_UnmarkingThePrimary_ReturnsValidationAndKeepsItPrimary()
  {
    // Arrange
    var primary = await SeedPhoneAsync(_juan, number: "+639171234567", isPrimary: true, Day(1));

    // Act: same number, but isPrimary flipped to false
    var result = await _service.UpdateAsync(_juan.Id, primary.Id, NewDto(number: "+639171234567", isPrimary: false));

    // Assert: rejected, and the row is still the primary
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
    var saved = Assert.Single(await SavedPhonesAsync(_juan.Id));
    Assert.True(saved.IsPrimary);
  }

  [Fact]
  public async Task UpdateAsync_KeepingOwnMobile_IsNotAConflict()
  {
    // Arrange
    var phone = await SeedPhoneAsync(_juan, number: "+639171234567", isPrimary: true, Day(1));

    // Act: only the ownership changes; the number stays the same.
    // Without excludePhoneId, the duplicate check would find this very row
    // and wrongly report the number as taken.
    var result = await _service.UpdateAsync(
        _juan.Id, phone.Id,
        NewDto(number: "+639171234567", ownership: ContactOwnership.Corporate, isPrimary: true));

    // Assert
    Assert.True(result.IsSuccess);
    Assert.Equal(ContactOwnership.Corporate, result.Value!.Ownership);
  }

  [Fact]
  public async Task UpdateAsync_MobileUsedByAnotherEmployee_ReturnsConflictAndKeepsOldNumber()
  {
    // Arrange
    await SeedPhoneAsync(_maria, number: "+639181234567", isPrimary: true, Day(1));
    var juanPhone = await SeedPhoneAsync(_juan, number: "+639171234567", isPrimary: true, Day(1));

    // Act: Juan tries to switch to Maria's mobile
    var result = await _service.UpdateAsync(_juan.Id, juanPhone.Id, NewDto(number: "+639181234567", isPrimary: true));

    // Assert: rejected, and Juan's number is unchanged
    Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
    var saved = Assert.Single(await SavedPhonesAsync(_juan.Id));
    Assert.Equal("+639171234567", saved.Phone.Number);
  }

  [Fact]
  public async Task UpdateAsync_MarkingSecondaryAsPrimary_TakesOverAsOnlyPrimary()
  {
    // Arrange: an existing primary, plus a secondary we'll promote
    var oldPrimary = await SeedPhoneAsync(_juan, number: "+639171234567", isPrimary: true, Day(1));
    var secondary = await SeedPhoneAsync(_juan, number: "+639181234567", isPrimary: false, Day(2));

    // Act
    var result = await _service.UpdateAsync(_juan.Id, secondary.Id, NewDto(number: "+639181234567", isPrimary: true));

    // Assert: exactly one primary, and it's the one we promoted
    Assert.True(result.IsSuccess);
    var saved = await SavedPhonesAsync(_juan.Id);
    var primary = Assert.Single(saved, p => p.IsPrimary);
    Assert.Equal(secondary.Id, primary.Id);
    Assert.False(saved.Single(p => p.Id == oldPrimary.Id).IsPrimary);
  }

  [Fact]
  public async Task UpdateAsync_MobileChangedToLandline_RedetectsLineType()
  {
    // Arrange: starts as a mobile
    var phone = await SeedPhoneAsync(_juan, number: "+639171234567", isPrimary: true, Day(1));

    // Act: change it to a Manila landline
    var result = await _service.UpdateAsync(_juan.Id, phone.Id, NewDto(number: "+63281234567", isPrimary: true));

    // Assert: LineType is recalculated from the new number, not kept from the old row
    Assert.True(result.IsSuccess);
    Assert.Equal(PhoneLineType.Landline, result.Value!.LineType);
  }

  // ---------- DeleteAsync ----------

  [Fact]
  public async Task DeleteAsync_PhoneDoesNotExist_ReturnsNotFound()
  {
    // Act
    var result = await _service.DeleteAsync(_juan.Id, 999_999);

    // Assert
    Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
  }

  [Fact]
  public async Task DeleteAsync_PhoneBelongsToAnotherEmployee_ReturnsNotFoundAndKeepsIt()
  {
    // Arrange: Maria has two phones, so the "last phone" rule can't be the reason it fails
    await SeedPhoneAsync(_maria, number: "+639181234567", isPrimary: true, Day(1));
    var mariaSecond = await SeedPhoneAsync(_maria, number: "+639191234567", isPrimary: false, Day(2));

    // Act: Juan tries to delete Maria's phone
    var result = await _service.DeleteAsync(_juan.Id, mariaSecond.Id);

    // Assert
    Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
    Assert.Equal(2, (await SavedPhonesAsync(_maria.Id)).Count);
  }

  [Fact]
  public async Task DeleteAsync_LastPhone_ReturnsValidationAndKeepsIt()
  {
    // Arrange
    var only = await SeedPhoneAsync(_juan, number: "+639171234567", isPrimary: true, Day(1));

    // Act
    var result = await _service.DeleteAsync(_juan.Id, only.Id);

    // Assert: the "at least one phone" rule blocks it
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
    Assert.Single(await SavedPhonesAsync(_juan.Id));
  }

  [Fact]
  public async Task DeleteAsync_PrimaryPhone_PromotesOldestRemaining()
  {
    // Arrange: the primary is in the middle; one older and one newer secondary
    var primary = await SeedPhoneAsync(_juan, number: "+639171234567", isPrimary: true, Day(2));
    var oldest = await SeedPhoneAsync(_juan, number: "+639181234567", isPrimary: false, Day(1));
    await SeedPhoneAsync(_juan, number: "+639191234567", isPrimary: false, Day(3));

    // Act
    var result = await _service.DeleteAsync(_juan.Id, primary.Id);

    // Assert: two remain, and the OLDEST one (by CreatedAt) is now primary
    Assert.True(result.IsSuccess);
    var remaining = await SavedPhonesAsync(_juan.Id);
    Assert.Equal(2, remaining.Count);
    var newPrimary = Assert.Single(remaining, p => p.IsPrimary);
    Assert.Equal(oldest.Id, newPrimary.Id);
  }

  [Fact]
  public async Task DeleteAsync_ExistingPhone_SoftDeletesTheRow()
  {
    // Arrange
    await SeedPhoneAsync(_juan, number: "+639171234567", isPrimary: true, Day(1));
    var second = await SeedPhoneAsync(_juan, number: "+639181234567", isPrimary: false, Day(2));

    // Act
    await _service.DeleteAsync(_juan.Id, second.Id);

    // Assert: hidden from normal queries...
    Assert.DoesNotContain(await SavedPhonesAsync(_juan.Id), p => p.Id == second.Id);

    // ...but still in the table, with DeletedAt set.
    // IgnoreQueryFilters() turns off the soft-delete filter for this one query.
    var row = await _context.EmployeePhones
        .IgnoreQueryFilters()
        .AsNoTracking()
        .SingleAsync(p => p.Id == second.Id);
    Assert.NotNull(row.DeletedAt);
    Assert.False(row.IsPrimary);
  }

  // ---------- GetAllAsync / GetByIdAsync ----------

  [Fact]
  public async Task GetAllAsync_EmployeeDoesNotExist_ReturnsNull()
  {
    // null (not an empty list) is how the controller tells 404 apart from "no phones yet"
    Assert.Null(await _service.GetAllAsync(999_999));
  }

  [Fact]
  public async Task GetAllAsync_EmployeeWithoutPhones_ReturnsEmptyList()
  {
    // Act
    var result = await _service.GetAllAsync(_juan.Id);

    // Assert
    Assert.NotNull(result);
    Assert.Empty(result);
  }

  [Fact]
  public async Task GetAllAsync_SeveralPhones_ListsPrimaryFirstAndSkipsDeletedAndOtherEmployees()
  {
    // Arrange: an older secondary, a newer primary, a soft-deleted one, and one of Maria's
    var olderSecondary = await SeedPhoneAsync(_juan, number: "+639171234567", isPrimary: false, Day(1));
    var primary = await SeedPhoneAsync(_juan, number: "+639181234567", isPrimary: true, Day(2));
    var deleted = await SeedPhoneAsync(_juan, number: "+639191234567", isPrimary: false, Day(3));
    deleted.DeletedAt = Day(4);
    await _context.SaveChangesAsync();
    await SeedPhoneAsync(_maria, number: "+639201234567", isPrimary: true, Day(1));

    // Act
    var result = await _service.GetAllAsync(_juan.Id);

    // Assert: only Juan's two live phones, primary first even though it's newer
    Assert.NotNull(result);
    Assert.Equal(2, result.Count);
    Assert.Equal(primary.Id, result[0].Id);
    Assert.Equal(olderSecondary.Id, result[1].Id);
  }

  [Fact]
  public async Task GetByIdAsync_PhoneBelongsToAnotherEmployee_ReturnsNull()
  {
    // Arrange
    var mariaPhone = await SeedPhoneAsync(_maria, number: "+639181234567", isPrimary: true, Day(1));

    // Act + Assert: looking it up under Juan's id finds nothing
    Assert.Null(await _service.GetByIdAsync(_juan.Id, mariaPhone.Id));
  }

  [Fact]
  public async Task GetByIdAsync_ExistingPhone_ReturnsDisplayFormats()
  {
    // Arrange
    var phone = await SeedPhoneAsync(_juan, number: "+639171234567", isPrimary: true, Day(1));

    // Act
    var result = await _service.GetByIdAsync(_juan.Id, phone.Id);

    // Assert: the stored E.164 number is expanded into the display formats
    Assert.NotNull(result);
    Assert.Equal("+639171234567", result.Phone.International);
    Assert.Equal("+63", result.Phone.DialCode);
    Assert.Equal("09171234567", result.Phone.Local);
  }

  // ---------- Helpers for POST /api/Employees ----------

  [Fact]
  public void NewEntitiesForNewEmployee_NoneMarkedPrimary_MakesFirstPrimary()
  {
    // Act: plain method, no database, so no async
    var entities = EmployeePhoneService.NewEntitiesForNewEmployee(
        [NewDto("+639171234567"), NewDto("+639181234567")]);

    // Assert
    Assert.True(entities[0].IsPrimary);
    Assert.False(entities[1].IsPrimary);
  }

  [Fact]
  public void NewEntitiesForNewEmployee_SecondMarkedPrimary_MakesOnlySecondPrimary()
  {
    // Act
    var entities = EmployeePhoneService.NewEntitiesForNewEmployee(
        [NewDto("+639171234567"), NewDto("+639181234567", isPrimary: true)]);

    // Assert
    Assert.False(entities[0].IsPrimary);
    Assert.True(entities[1].IsPrimary);
  }

  [Fact]
  public void NewEntitiesForNewEmployee_MobileAndLandline_DetectsEachLineType()
  {
    // Act
    var entities = EmployeePhoneService.NewEntitiesForNewEmployee(
        [NewDto("+639171234567"), NewDto("+63281234567")]);

    // Assert
    Assert.Equal(PhoneLineType.Mobile, entities[0].LineType);
    Assert.Equal(PhoneLineType.Landline, entities[1].LineType);
  }

  [Fact]
  public void NewEntitiesForNewEmployee_UnvalidatedInvalidNumber_Throws()
  {
    // Calling this without running ValidateForNewEmployeeAsync first is a
    // programming error, so it throws rather than returning a result.
    // Assert.Throws<T> passes only if the lambda throws exactly that type,
    // like Jest's expect(() => ...).toThrow(InvalidOperationError).
    Assert.Throws<InvalidOperationException>(() =>
        EmployeePhoneService.NewEntitiesForNewEmployee([NewDto(countryCode: "US")]));
  }

  [Fact]
  public async Task ValidateForNewEmployeeAsync_TwoMarkedPrimary_ReturnsValidation()
  {
    // Act
    var result = await _service.ValidateForNewEmployeeAsync(
        [NewDto("+639171234567", isPrimary: true), NewDto("+639181234567", isPrimary: true)]);

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
  }

  [Fact]
  public async Task ValidateForNewEmployeeAsync_NumberInvalidForCountry_ReturnsValidation()
  {
    // Act: the second phone is a PH number sent with country "US"
    var result = await _service.ValidateForNewEmployeeAsync(
        [NewDto("+639171234567"), NewDto("+639181234567", countryCode: "US")]);

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
  }

  [Fact]
  public async Task ValidateForNewEmployeeAsync_SameMobileTwice_ReturnsValidation()
  {
    // Act: a repeat inside the request is the client's mistake (400),
    // not a clash with someone else's data (409)
    var result = await _service.ValidateForNewEmployeeAsync(
        [NewDto("+639171234567"), NewDto("+639171234567")]);

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
  }

  [Fact]
  public async Task ValidateForNewEmployeeAsync_SameLandlineTwice_ReturnsSuccess()
  {
    // Act: landlines may repeat, e.g. office trunk line entered twice
    var result = await _service.ValidateForNewEmployeeAsync(
        [NewDto("+63281234567"), NewDto("+63281234567")]);

    // Assert
    Assert.True(result.IsSuccess);
  }

  [Fact]
  public async Task ValidateForNewEmployeeAsync_MobileUsedByExistingEmployee_ReturnsConflict()
  {
    // Arrange
    await SeedPhoneAsync(_maria, number: "+639181234567", isPrimary: true, Day(1));

    // Act
    var result = await _service.ValidateForNewEmployeeAsync(
        [NewDto("+639171234567"), NewDto("+639181234567")]);

    // Assert
    Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
  }

  [Fact]
  public async Task ValidateForNewEmployeeAsync_MobileOnlyOnSoftDeletedPhone_ReturnsSuccess()
  {
    // Arrange
    var deletedPhone = await SeedPhoneAsync(_maria, number: "+639181234567", isPrimary: true, Day(1));
    deletedPhone.DeletedAt = Day(2);
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.ValidateForNewEmployeeAsync([NewDto("+639181234567")]);

    // Assert
    Assert.True(result.IsSuccess);
  }
}