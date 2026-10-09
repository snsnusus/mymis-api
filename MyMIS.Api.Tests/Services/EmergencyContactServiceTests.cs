using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;
using MyMIS.Api.Models;
using MyMIS.Api.Services;
using MyMIS.Api.Tests.TestData;

namespace MyMIS.Api.Tests.Services;

public class EmergencyContactServiceTests : IDisposable
{
  private readonly AppDbContext _context;
  private readonly EmergencyContactService _service;

  // Seeded once per test in the constructor.
  private readonly Employee _juan;
  private readonly Employee _maria;
  private readonly Barangay _barangay;

  public EmergencyContactServiceTests()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options;

    _context = new AppDbContext(options);
    _service = new EmergencyContactService(_context);

    var region = new Region { Name = "NCR" };
    var city = new City { Name = "Quezon City", Region = region };
    _barangay = new Barangay { Name = "Bagong Pag-asa", ZipCode = "1105", City = city };

    _juan = TestEmployees.New("Juan", "Cruz", "EMP-001");
    _maria = TestEmployees.New("Maria", "Reyes", "EMP-002");

    _context.Barangays.Add(_barangay);
    _context.Employees.AddRange(_juan, _maria);
    _context.SaveChanges();
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

  private EmergencyContactCreateDto NewDto(
    string firstName = "Ana",
    string lastName = "Santos",
    EmergencyContactRelationship relationship = EmergencyContactRelationship.Spouse,
    string phoneNumber = "+639171234567",
    bool isPrimary = false,
    int? barangayId = null) => new()
    {
      FirstName = firstName,
      LastName = lastName,
      Relationship = relationship,
      Phone = new PhoneDto { CountryCode = "PH", Number = phoneNumber },
      Address = new AddressDto
      {
        AddressLine1 = "123 Mabini St.",
        BarangayId = barangayId ?? _barangay.Id,
        PostalCode = "1105",
      },
      IsPrimary = isPrimary,
    };

  // Inserts straight into the database so a test's starting state doesn't depend on CreateAsync.
  private async Task<EmergencyContact> SeedContactAsync(
    Employee employee,
    string firstName,
    bool isPrimary,
    DateTime createdAt,
    bool withAddress = true,
    DateTime? deletedAt = null)
  {
    var contact = new EmergencyContact
    {
      EmployeeId = employee.Id,
      FirstName = firstName,
      LastName = "Santos",
      Relationship = EmergencyContactRelationship.Parent,
      Phone = new Phone { CountryCode = "PH", Number = "+639171234567" },
      Address = withAddress
        ? new Address { AddressLine1 = "Seeded St.", BarangayId = _barangay.Id, PostalCode = "1105" }
        : null,
      IsPrimary = isPrimary,
      CreatedAt = createdAt,
      UpdatedAt = createdAt,
      DeletedAt = deletedAt,
    };

    _context.EmergencyContacts.Add(contact);
    await _context.SaveChangesAsync();
    return contact;
  }

  // Live (not soft-deleted) contacts, read without tracking.
  private Task<List<EmergencyContact>> SavedContactsAsync(int employeeId) =>
    _context.EmergencyContacts
      .AsNoTracking()
      .Where(c => c.EmployeeId == employeeId)
      .ToListAsync();

  // Bypasses the soft-delete query filter, so deleted rows are visible.
  private Task<List<EmergencyContact>> AllContactsIncludingDeletedAsync(int employeeId) =>
    _context.EmergencyContacts
      .IgnoreQueryFilters()
      .AsNoTracking()
      .Where(c => c.EmployeeId == employeeId)
      .ToListAsync();

  // ---------------- reads: GetAllAsync ----------------

  [Fact]
  public async Task GetAllAsync_EmployeeDoesNotExist_ReturnsNull()
  {
    Assert.Null(await _service.GetAllAsync(9999));
  }

  [Fact]
  public async Task GetAllAsync_EmployeeWithNoContacts_ReturnsEmptyList()
  {
    // Act
    var result = await _service.GetAllAsync(_juan.Id);

    // Assert: empty list (employee exists), not null (employee missing)
    Assert.NotNull(result);
    Assert.Empty(result);
  }

  [Fact]
  public async Task GetAllAsync_SeveralContacts_PrimaryFirstThenOldest()
  {
    // Arrange: seeded in scrambled order
    await SeedContactAsync(_juan, "B", isPrimary: false, createdAt: Day(3));
    await SeedContactAsync(_juan, "A", isPrimary: true, createdAt: Day(5));
    await SeedContactAsync(_juan, "C", isPrimary: false, createdAt: Day(1));

    // Act
    var result = await _service.GetAllAsync(_juan.Id);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(new[] { "A", "C", "B" }, result.Select(c => c.FirstName));
  }

  [Fact]
  public async Task GetAllAsync_ReturnsOnlyThatEmployeesLiveContacts()
  {
    // Arrange
    await SeedContactAsync(_juan, "Live", isPrimary: true, createdAt: Day(1));
    await SeedContactAsync(_juan, "Deleted", isPrimary: false, createdAt: Day(2), deletedAt: Day(3));
    await SeedContactAsync(_maria, "Other", isPrimary: true, createdAt: Day(1));

    // Act
    var result = await _service.GetAllAsync(_juan.Id);

    // Assert
    Assert.NotNull(result);
    var only = Assert.Single(result);
    Assert.Equal("Live", only.FirstName);
  }

  [Fact]
  public async Task GetAllAsync_ContactWithAddress_MapsTheFullLocationChain()
  {
    // Arrange
    await SeedContactAsync(_juan, "Ana", isPrimary: true, createdAt: Day(1));

    // Act
    var result = await _service.GetAllAsync(_juan.Id);

    // Assert
    Assert.NotNull(result);
    var address = Assert.Single(result).Address;
    Assert.NotNull(address);
    Assert.Equal("Bagong Pag-asa", address.Barangay.Name);
    Assert.Equal("Quezon City", address.City.Name);
    Assert.Equal("NCR", address.Region.Name);
  }

  // ---------------- reads: GetByIdAsync ----------------

  [Fact]
  public async Task GetByIdAsync_ExistingContact_ReturnsIt()
  {
    // Arrange
    var seed = await SeedContactAsync(_juan, "Ana", isPrimary: true, createdAt: Day(1));

    // Act
    var result = await _service.GetByIdAsync(_juan.Id, seed.Id);

    // Assert
    Assert.NotNull(result);
    Assert.Equal("Ana", result.FirstName);
    Assert.True(result.IsPrimary);
  }

  [Fact]
  public async Task GetByIdAsync_NonexistentId_ReturnsNull()
  {
    Assert.Null(await _service.GetByIdAsync(_juan.Id, 9999));
  }

  [Fact]
  public async Task GetByIdAsync_ContactOfAnotherEmployee_ReturnsNull()
  {
    // Arrange
    var mariasContact = await SeedContactAsync(_maria, "Other", isPrimary: true, createdAt: Day(1));

    // Act + Assert: the id exists, but not for Juan
    Assert.Null(await _service.GetByIdAsync(_juan.Id, mariasContact.Id));
  }

  [Fact]
  public async Task GetByIdAsync_SoftDeletedContact_ReturnsNull()
  {
    // Arrange
    var deleted = await SeedContactAsync(_juan, "Gone", isPrimary: false, createdAt: Day(1), deletedAt: Day(2));

    // Act + Assert
    Assert.Null(await _service.GetByIdAsync(_juan.Id, deleted.Id));
  }

  // ---------------- CreateAsync ----------------

  [Fact]
  public async Task CreateAsync_EmployeeDoesNotExist_ReturnsNotFound()
  {
    // Act
    var result = await _service.CreateAsync(9999, NewDto());

    // Assert
    Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
    Assert.Empty(await SavedContactsAsync(9999));
  }

  [Fact]
  public async Task CreateAsync_FirstContactNotMarkedPrimary_StillBecomesPrimary()
  {
    // Act
    var result = await _service.CreateAsync(_juan.Id, NewDto(isPrimary: false));

    // Assert
    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Value);
    Assert.True(result.Value.IsPrimary);

    var saved = Assert.Single(await SavedContactsAsync(_juan.Id));
    Assert.True(saved.IsPrimary);
  }

  [Fact]
  public async Task CreateAsync_NewContactMarkedPrimary_TakesOverFromExistingPrimary()
  {
    // Arrange
    var first = await SeedContactAsync(_juan, "Old", isPrimary: true, createdAt: Day(1));

    // Act
    var result = await _service.CreateAsync(_juan.Id, NewDto("New", isPrimary: true));

    // Assert
    Assert.True(result.IsSuccess);
    var saved = await SavedContactsAsync(_juan.Id);
    Assert.Equal(2, saved.Count);
    Assert.False(saved.Single(c => c.Id == first.Id).IsPrimary);
    var primary = Assert.Single(saved, c => c.IsPrimary);
    Assert.Equal("New", primary.FirstName);
  }

  [Fact]
  public async Task CreateAsync_NewContactNotMarkedPrimary_KeepsExistingPrimary()
  {
    // Arrange
    await SeedContactAsync(_juan, "Old", isPrimary: true, createdAt: Day(1));

    // Act
    var result = await _service.CreateAsync(_juan.Id, NewDto("New", isPrimary: false));

    // Assert
    Assert.True(result.IsSuccess);
    var saved = await SavedContactsAsync(_juan.Id);
    Assert.Equal(2, saved.Count);
    var primary = Assert.Single(saved, c => c.IsPrimary);
    Assert.Equal("Old", primary.FirstName);
  }

  [Fact]
  public async Task CreateAsync_PhoneInvalidForCountry_ReturnsValidationAndSavesNothing()
  {
    // Act: too short to be a real PH number
    var result = await _service.CreateAsync(_juan.Id, NewDto(phoneNumber: "+63917123"));

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
    Assert.Empty(await SavedContactsAsync(_juan.Id));
  }

  [Fact]
  public async Task CreateAsync_BarangayDoesNotExist_ReturnsValidationAndSavesNothing()
  {
    // Act
    var result = await _service.CreateAsync(_juan.Id, NewDto(barangayId: 999_999));

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
    Assert.Empty(await SavedContactsAsync(_juan.Id));
  }

  [Fact]
  public async Task CreateAsync_FieldsWithWhitespaceAndBlanks_AreStoredNormalized()
  {
    // Arrange
    var dto = NewDto("  Ana  ", "  Santos  ");
    dto.MiddleName = "   ";
    dto.Suffix = " Jr. ";
    dto.Phone = new PhoneDto { CountryCode = "ph", Number = " +639171234567 " };

    // Act
    var result = await _service.CreateAsync(_juan.Id, dto);

    // Assert
    Assert.True(result.IsSuccess);
    var saved = Assert.Single(await SavedContactsAsync(_juan.Id));
    Assert.Equal("Ana", saved.FirstName);
    Assert.Equal("Santos", saved.LastName);
    Assert.Null(saved.MiddleName);
    Assert.Equal("Jr.", saved.Suffix);
    Assert.Equal("PH", saved.Phone.CountryCode);
    Assert.Equal("+639171234567", saved.Phone.Number);
  }

  [Fact]
  public async Task CreateAsync_ValidContact_ReturnsTheFullLocationChain()
  {
    // Act
    var result = await _service.CreateAsync(_juan.Id, NewDto());

    // Assert
    Assert.True(result.IsSuccess);
    var address = result.Value!.Address;
    Assert.NotNull(address);
    Assert.Equal("Bagong Pag-asa", address.Barangay.Name);
    Assert.Equal("Quezon City", address.City.Name);
    Assert.Equal("NCR", address.Region.Name);
  }

  [Fact]
  public async Task CreateAsync_ValidContact_SetsEqualRecentTimestamps()
  {
    // Act
    var result = await _service.CreateAsync(_juan.Id, NewDto());

    // Assert
    Assert.True(result.IsSuccess);
    var saved = Assert.Single(await SavedContactsAsync(_juan.Id));
    Assert.Equal(saved.CreatedAt, saved.UpdatedAt);
    Assert.True(saved.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
  }

  // ---------------- UpdateAsync ----------------
  // Note: when the seeded contact is primary, the update DTO must also say isPrimary: true,
  // otherwise the "can't un-mark the primary" rule fires first and hides what the test is about.

  [Fact]
  public async Task UpdateAsync_ContactBelongsToAnotherEmployee_ReturnsNotFoundAndLeavesItUnchanged()
  {
    // Arrange
    var mariasContact = await SeedContactAsync(_maria, "Other", isPrimary: true, createdAt: Day(1));

    // Act: Juan tries to edit Maria's contact
    var result = await _service.UpdateAsync(_juan.Id, mariasContact.Id, NewDto("Hijacked", isPrimary: true));

    // Assert
    Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
    var saved = Assert.Single(await SavedContactsAsync(_maria.Id));
    Assert.Equal("Other", saved.FirstName);
  }

  [Fact]
  public async Task UpdateAsync_UnmarkingThePrimary_ReturnsValidationAndKeepsItPrimary()
  {
    // Arrange
    var seed = await SeedContactAsync(_juan, "Ana", isPrimary: true, createdAt: Day(1));

    // Act
    var result = await _service.UpdateAsync(_juan.Id, seed.Id, NewDto("Ana", isPrimary: false));

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
    var saved = Assert.Single(await SavedContactsAsync(_juan.Id));
    Assert.True(saved.IsPrimary);
  }

  [Fact]
  public async Task UpdateAsync_PhoneInvalidForCountry_ReturnsValidationAndKeepsOldPhone()
  {
    // Arrange
    var seed = await SeedContactAsync(_juan, "Ana", isPrimary: true, createdAt: Day(1));

    // Act
    var result = await _service.UpdateAsync(_juan.Id, seed.Id, NewDto(phoneNumber: "+63917123", isPrimary: true));

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
    var saved = Assert.Single(await SavedContactsAsync(_juan.Id));
    Assert.Equal("+639171234567", saved.Phone.Number);
  }

  [Fact]
  public async Task UpdateAsync_BarangayDoesNotExist_ReturnsValidationAndKeepsOldAddress()
  {
    // Arrange
    var seed = await SeedContactAsync(_juan, "Ana", isPrimary: true, createdAt: Day(1));

    // Act
    var result = await _service.UpdateAsync(_juan.Id, seed.Id, NewDto(barangayId: 999_999, isPrimary: true));

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
    var saved = Assert.Single(await SavedContactsAsync(_juan.Id));
    Assert.NotNull(saved.Address);
    Assert.Equal("Seeded St.", saved.Address.AddressLine1);
  }

  [Fact]
  public async Task UpdateAsync_NonPrimaryBecomingPrimary_TakesOverFromTheOldPrimary()
  {
    // Arrange
    var first = await SeedContactAsync(_juan, "First", isPrimary: true, createdAt: Day(1));
    var second = await SeedContactAsync(_juan, "Second", isPrimary: false, createdAt: Day(2));

    // Act
    var result = await _service.UpdateAsync(_juan.Id, second.Id, NewDto("Second", isPrimary: true));

    // Assert
    Assert.True(result.IsSuccess);
    var saved = await SavedContactsAsync(_juan.Id);
    Assert.Equal(2, saved.Count);
    Assert.False(saved.Single(c => c.Id == first.Id).IsPrimary);
    var primary = Assert.Single(saved, c => c.IsPrimary);
    Assert.Equal(second.Id, primary.Id);
  }

  [Fact]
  public async Task UpdateAsync_ValidChange_SavesNormalizedFields()
  {
    // Arrange
    var seed = await SeedContactAsync(_juan, "Ana", isPrimary: true, createdAt: Day(1));
    var dto = NewDto(
      "  Bea  ", "  Cruz  ", EmergencyContactRelationship.Sibling, phoneNumber: "+63281234567", isPrimary: true);
    dto.MiddleName = "  Lee  ";
    dto.Suffix = "   ";

    // Act
    var result = await _service.UpdateAsync(_juan.Id, seed.Id, dto);

    // Assert
    Assert.True(result.IsSuccess);
    var saved = Assert.Single(await SavedContactsAsync(_juan.Id));
    Assert.Equal("Bea", saved.FirstName);
    Assert.Equal("Cruz", saved.LastName);
    Assert.Equal("Lee", saved.MiddleName);
    Assert.Null(saved.Suffix);
    Assert.Equal(EmergencyContactRelationship.Sibling, saved.Relationship);
    Assert.Equal("+63281234567", saved.Phone.Number);
  }

  [Fact]
  public async Task UpdateAsync_AddressOmitted_ClearsTheAddress()
  {
    // Arrange
    var seed = await SeedContactAsync(_juan, "Ana", isPrimary: true, createdAt: Day(1), withAddress: true);
    var dto = NewDto(isPrimary: true);
    dto.Address = null;

    // Act
    var result = await _service.UpdateAsync(_juan.Id, seed.Id, dto);

    // Assert
    Assert.True(result.IsSuccess);
    var saved = Assert.Single(await SavedContactsAsync(_juan.Id));
    Assert.Null(saved.Address);
  }

  [Fact]
  public async Task UpdateAsync_ValidChange_MovesUpdatedAtButNotCreatedAt()
  {
    // Arrange
    var seed = await SeedContactAsync(_juan, "Ana", isPrimary: true, createdAt: Day(1));

    // Act
    var result = await _service.UpdateAsync(_juan.Id, seed.Id, NewDto("Bea", isPrimary: true));

    // Assert
    Assert.True(result.IsSuccess);
    var saved = Assert.Single(await SavedContactsAsync(_juan.Id));
    Assert.True(saved.UpdatedAt > Day(1));
    Assert.Equal(Day(1), saved.CreatedAt);
  }

  // ---------------- DeleteAsync ----------------

  [Fact]
  public async Task DeleteAsync_ContactDoesNotExist_ReturnsFalse()
  {
    Assert.False(await _service.DeleteAsync(_juan.Id, 9999));
  }

  [Fact]
  public async Task DeleteAsync_ContactOfAnotherEmployee_ReturnsFalseAndKeepsIt()
  {
    // Arrange
    var mariasContact = await SeedContactAsync(_maria, "Other", isPrimary: true, createdAt: Day(1));

    // Act
    var deleted = await _service.DeleteAsync(_juan.Id, mariasContact.Id);

    // Assert
    Assert.False(deleted);
    Assert.Single(await SavedContactsAsync(_maria.Id));
  }

  [Fact]
  public async Task DeleteAsync_NonPrimaryContact_SoftDeletesItAndLeavesThePrimaryAlone()
  {
    // Arrange
    var primary = await SeedContactAsync(_juan, "Primary", isPrimary: true, createdAt: Day(1));
    var other = await SeedContactAsync(_juan, "Other", isPrimary: false, createdAt: Day(2));

    // Act
    var deleted = await _service.DeleteAsync(_juan.Id, other.Id);

    // Assert: normal reads only see the primary, still primary
    Assert.True(deleted);
    var live = Assert.Single(await SavedContactsAsync(_juan.Id));
    Assert.Equal(primary.Id, live.Id);
    Assert.True(live.IsPrimary);

    // The deleted row still exists, marked deleted
    var all = await AllContactsIncludingDeletedAsync(_juan.Id);
    Assert.Equal(2, all.Count);
    var softDeleted = all.Single(c => c.Id == other.Id);
    Assert.NotNull(softDeleted.DeletedAt);
    Assert.False(softDeleted.IsPrimary);
  }

  [Fact]
  public async Task DeleteAsync_PrimaryContact_PromotesTheOldestRemaining()
  {
    // Arrange: seeded out of order on purpose; "C" (Day 3) is the oldest of the remaining two
    var primary = await SeedContactAsync(_juan, "A", isPrimary: true, createdAt: Day(2));
    await SeedContactAsync(_juan, "B", isPrimary: false, createdAt: Day(5));
    var oldest = await SeedContactAsync(_juan, "C", isPrimary: false, createdAt: Day(3));

    // Act
    var deleted = await _service.DeleteAsync(_juan.Id, primary.Id);

    // Assert
    Assert.True(deleted);
    var saved = await SavedContactsAsync(_juan.Id);
    Assert.Equal(2, saved.Count);
    var newPrimary = Assert.Single(saved, c => c.IsPrimary);
    Assert.Equal(oldest.Id, newPrimary.Id);
  }

  [Fact]
  public async Task DeleteAsync_LastRemainingContact_IsAllowed()
  {
    // Arrange: unlike emails and phones, an employee may end up with no emergency contact
    var only = await SeedContactAsync(_juan, "Only", isPrimary: true, createdAt: Day(1));

    // Act
    var deleted = await _service.DeleteAsync(_juan.Id, only.Id);

    // Assert
    Assert.True(deleted);
    Assert.Empty(await SavedContactsAsync(_juan.Id));
    Assert.Single(await AllContactsIncludingDeletedAsync(_juan.Id));
  }

  // ---------------- ValidateAsync ----------------

  [Fact]
  public async Task ValidateAsync_ValidDto_ReturnsNormalizedPhoneAndNoError()
  {
    // Act
    var (phone, error) = await _service.ValidateAsync(NewDto());

    // Assert
    Assert.Null(error);
    Assert.NotNull(phone);
    Assert.Equal("PH", phone.CountryCode);
    Assert.Equal("+639171234567", phone.Number);
  }

  [Fact]
  public async Task ValidateAsync_PhoneInvalidForCountry_ReturnsError()
  {
    // Act
    var (phone, error) = await _service.ValidateAsync(NewDto(phoneNumber: "+63917123"));

    // Assert
    Assert.Null(phone);
    Assert.Equal("Phone number is not valid for the selected country.", error);
  }

  [Fact]
  public async Task ValidateAsync_UnknownBarangay_ReturnsError()
  {
    // Act
    var (phone, error) = await _service.ValidateAsync(NewDto(barangayId: 999_999));

    // Assert
    Assert.Null(phone);
    Assert.Equal("Barangay 999999 was not found.", error);
  }

  [Fact]
  public async Task ValidateAsync_AddressOmitted_SkipsTheBarangayCheck()
  {
    // Arrange: the DTO attribute requires an address at the API, but the service tolerates its absence
    var dto = NewDto();
    dto.Address = null;

    // Act
    var (phone, error) = await _service.ValidateAsync(dto);

    // Assert
    Assert.Null(error);
    Assert.NotNull(phone);
  }

  // ---------------- NewEntity (static, no database) ----------------

  [Fact]
  public void NewEntity_BuildsAnUnsavedContactFromTheDto()
  {
    // Arrange
    var dto = NewDto("  Ana  ", "  Santos  ", EmergencyContactRelationship.Parent);
    dto.MiddleName = "   ";
    var phone = new Phone { CountryCode = "PH", Number = "+639171234567" };

    // Act
    var contact = EmergencyContactService.NewEntity(dto, phone, isPrimary: true);

    // Assert
    Assert.Equal(0, contact.Id);
    Assert.Equal(0, contact.EmployeeId);
    Assert.Equal("Ana", contact.FirstName);
    Assert.Equal("Santos", contact.LastName);
    Assert.Null(contact.MiddleName);
    Assert.Equal(EmergencyContactRelationship.Parent, contact.Relationship);
    Assert.Same(phone, contact.Phone);
    Assert.NotNull(contact.Address);
    Assert.True(contact.IsPrimary);
    Assert.Equal(contact.CreatedAt, contact.UpdatedAt);
  }
}