using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;
using MyMIS.Api.Models;
using MyMIS.Api.Services;

namespace MyMIS.Api.Tests.Services;

public class EmployeeAddressServiceTests : IDisposable
{
  private readonly AppDbContext _context;
  private readonly EmployeeAddressService _service;

  // Seeded once per test in the constructor.
  private readonly Employee _juan;
  private readonly Employee _maria;
  private readonly Barangay _barangay;

  public EmployeeAddressServiceTests()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options;

    _context = new AppDbContext(options);
    _service = new EmployeeAddressService(_context);

    var region = new Region { Name = "NCR" };
    var city = new City { Name = "Quezon City", Region = region };
    _barangay = new Barangay { Name = "Bagong Pag-asa", ZipCode = "1105", City = city };

    _juan = NewEmployee("Juan", "Cruz", "EMP-001");
    _maria = NewEmployee("Maria", "Reyes", "EMP-002");

    _context.Barangays.Add(_barangay);
    _context.Employees.AddRange(_juan, _maria);
    _context.SaveChanges();
  }

  public void Dispose()
  {
    _context.Dispose();
    GC.SuppressFinalize(this);
  }

  private static Employee NewEmployee(string firstName, string lastName, string employeeCode) => new()
  {
    FirstName = firstName,
    LastName = lastName,
    Gender = "MALE",
    MaritalStatus = "SINGLE",
    EmployeeCode = employeeCode,
    Username = employeeCode.ToLowerInvariant(),
    PasswordHash = "irrelevant-for-this-test",
  };

  private EmployeeAddressCreateDto NewDto(
      AddressType type = AddressType.Present,
      bool isPrimary = false,
      int? barangayId = null) => new()
      {
        Type = type,
        Address = new AddressDto
        {
          AddressLine1 = "123 Mabini St.",
          BarangayId = barangayId ?? _barangay.Id,
          PostalCode = "1105",
        },
        IsPrimary = isPrimary,
      };

  // Seeds an address directly, bypassing the service, so a test's starting
  // state doesn't depend on CreateAsync behaving correctly.
  private async Task<EmployeeAddress> SeedAddressAsync(
      Employee employee,
      bool isPrimary,
      DateTime createdAt)
  {
    var address = new EmployeeAddress
    {
      EmployeeId = employee.Id,
      Type = AddressType.Present,
      Address = new Address
      {
        AddressLine1 = "Seeded St.",
        BarangayId = _barangay.Id,
        PostalCode = "1105",
      },
      IsPrimary = isPrimary,
      CreatedAt = createdAt,
      UpdatedAt = createdAt,
    };
    _context.EmployeeAddresses.Add(address);
    await _context.SaveChangesAsync();
    return address;
  }

  private Task<List<EmployeeAddress>> SavedAddressesAsync(int employeeId) =>
      _context.EmployeeAddresses
          .AsNoTracking()
          .Where(a => a.EmployeeId == employeeId)
          .ToListAsync();

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
  public async Task CreateAsync_FirstAddressNotMarkedPrimary_StillBecomesPrimary()
  {
    // Act
    var result = await _service.CreateAsync(_juan.Id, NewDto(isPrimary: false));

    // Assert
    Assert.True(result.IsSuccess);
    Assert.True(result.Value!.IsPrimary);
  }

  [Fact]
  public async Task CreateAsync_SecondAddressMarkedPrimary_TakesOverAsOnlyPrimary()
  {
    // Arrange
    var first = await SeedAddressAsync(_juan, isPrimary: true, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    // Act
    var result = await _service.CreateAsync(_juan.Id, NewDto(AddressType.Permanent, isPrimary: true));

    // Assert
    Assert.True(result.IsSuccess);

    var saved = await SavedAddressesAsync(_juan.Id);
    var primary = Assert.Single(saved, a => a.IsPrimary);
    Assert.Equal(result.Value!.Id, primary.Id);
    Assert.False(saved.Single(a => a.Id == first.Id).IsPrimary);
  }

  [Fact]
  public async Task CreateAsync_BarangayDoesNotExist_ReturnsValidationAndSavesNothing()
  {
    // Act
    var result = await _service.CreateAsync(_juan.Id, NewDto(barangayId: 999_999));

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
    Assert.Empty(await SavedAddressesAsync(_juan.Id));
  }

  [Fact]
  public async Task CreateAsync_ValidAddress_ReturnsFullLocationChain()
  {
    // Act
    var result = await _service.CreateAsync(_juan.Id, NewDto());

    // Assert
    var address = result.Value!.Address;
    Assert.Equal("Bagong Pag-asa", address.Barangay.Name);
    Assert.Equal("Quezon City", address.City.Name);
    Assert.Equal("NCR", address.Region.Name);
  }

  // ---------- UpdateAsync ----------

  [Fact]
  public async Task UpdateAsync_AddressBelongsToAnotherEmployee_ReturnsNotFound()
  {
    // Arrange: Maria's address, updated through Juan's id
    var mariasAddress = await SeedAddressAsync(_maria, isPrimary: true, DateTime.UtcNow);

    // Act
    var result = await _service.UpdateAsync(_juan.Id, mariasAddress.Id, NewDto(isPrimary: true));

    // Assert
    Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
  }

  [Fact]
  public async Task UpdateAsync_UnmarkingThePrimary_ReturnsValidation()
  {
    // Arrange
    var primary = await SeedAddressAsync(_juan, isPrimary: true, DateTime.UtcNow);

    // Act
    var result = await _service.UpdateAsync(_juan.Id, primary.Id, NewDto(isPrimary: false));

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
  }

  [Fact]
  public async Task UpdateAsync_ValidChange_SavesNewTypeAndAddress()
  {
    // Arrange
    var primary = await SeedAddressAsync(_juan, isPrimary: true, DateTime.UtcNow);
    var dto = NewDto(AddressType.Permanent, isPrimary: true);
    dto.Address!.AddressLine1 = "  456 Rizal Ave.  ";

    // Act
    var result = await _service.UpdateAsync(_juan.Id, primary.Id, dto);

    // Assert
    Assert.True(result.IsSuccess);
    var saved = Assert.Single(await SavedAddressesAsync(_juan.Id));
    Assert.Equal(AddressType.Permanent, saved.Type);
    Assert.Equal("456 Rizal Ave.", saved.Address.AddressLine1);
  }

  // ---------- DeleteAsync ----------

  [Fact]
  public async Task DeleteAsync_AddressDoesNotExist_ReturnsNotFound()
  {
    // Act
    var result = await _service.DeleteAsync(_juan.Id, 999_999);

    // Assert
    Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
  }

  [Fact]
  public async Task DeleteAsync_LastAddress_ReturnsValidationAndKeepsIt()
  {
    // Arrange
    var only = await SeedAddressAsync(_juan, isPrimary: true, DateTime.UtcNow);

    // Act
    var result = await _service.DeleteAsync(_juan.Id, only.Id);

    // Assert
    Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
    Assert.Single(await SavedAddressesAsync(_juan.Id));
  }

  [Fact]
  public async Task DeleteAsync_PrimaryAddress_PromotesOldestRemaining()
  {
    // Arrange: primary, then an older and a newer non-primary
    var primary = await SeedAddressAsync(_juan, isPrimary: true, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
    var oldest = await SeedAddressAsync(_juan, isPrimary: false, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    await SeedAddressAsync(_juan, isPrimary: false, new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc));

    // Act
    var result = await _service.DeleteAsync(_juan.Id, primary.Id);

    // Assert
    Assert.True(result.IsSuccess);
    var remaining = await SavedAddressesAsync(_juan.Id);
    Assert.Equal(2, remaining.Count);
    var newPrimary = Assert.Single(remaining, a => a.IsPrimary);
    Assert.Equal(oldest.Id, newPrimary.Id);
  }

  [Fact]
  public async Task DeleteAsync_ExistingAddress_SoftDeletesTheRow()
  {
    // Arrange
    await SeedAddressAsync(_juan, isPrimary: true, DateTime.UtcNow);
    var second = await SeedAddressAsync(_juan, isPrimary: false, DateTime.UtcNow);

    // Act
    await _service.DeleteAsync(_juan.Id, second.Id);

    // Assert: hidden from normal queries, but still in the table
    Assert.DoesNotContain(await SavedAddressesAsync(_juan.Id), a => a.Id == second.Id);

    var row = await _context.EmployeeAddresses
        .IgnoreQueryFilters()
        .AsNoTracking()
        .SingleAsync(a => a.Id == second.Id);
    Assert.NotNull(row.DeletedAt);
    Assert.False(row.IsPrimary);
  }

  // ---------- GetAllAsync ----------

  [Fact]
  public async Task GetAllAsync_EmployeeDoesNotExist_ReturnsNull()
  {
    Assert.Null(await _service.GetAllAsync(999_999));
  }

  [Fact]
  public async Task GetAllAsync_EmployeeWithoutAddresses_ReturnsEmptyList()
  {
    var result = await _service.GetAllAsync(_juan.Id);

    Assert.NotNull(result);
    Assert.Empty(result);
  }

  [Fact]
  public async Task GetAllAsync_SeveralAddresses_ListsPrimaryFirstAndOnlyThatEmployees()
  {
    // Arrange
    await SeedAddressAsync(_juan, isPrimary: false, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    var primary = await SeedAddressAsync(_juan, isPrimary: true, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
    await SeedAddressAsync(_maria, isPrimary: true, DateTime.UtcNow);

    // Act
    var result = await _service.GetAllAsync(_juan.Id);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(2, result.Count);
    Assert.Equal(primary.Id, result[0].Id);
  }

  // ---------- Helpers for POST /api/Employees ----------

  [Fact]
  public void NewEntitiesForNewEmployee_NoneMarkedPrimary_MakesFirstPrimary()
  {
    // Act
    var entities = EmployeeAddressService.NewEntitiesForNewEmployee(
        [NewDto(AddressType.Present), NewDto(AddressType.Permanent)]);

    // Assert
    Assert.True(entities[0].IsPrimary);
    Assert.False(entities[1].IsPrimary);
  }

  [Fact]
  public void NewEntitiesForNewEmployee_SecondMarkedPrimary_MakesOnlySecondPrimary()
  {
    // Act
    var entities = EmployeeAddressService.NewEntitiesForNewEmployee(
        [NewDto(AddressType.Present), NewDto(AddressType.Permanent, isPrimary: true)]);

    // Assert
    Assert.False(entities[0].IsPrimary);
    Assert.True(entities[1].IsPrimary);
  }

  [Fact]
  public async Task ValidateForNewEmployeeAsync_TwoMarkedPrimary_ReturnsError()
  {
    var error = await _service.ValidateForNewEmployeeAsync(
        [NewDto(isPrimary: true), NewDto(isPrimary: true)]);

    Assert.NotNull(error);
  }

  [Fact]
  public async Task ValidateForNewEmployeeAsync_UnknownBarangay_ReturnsError()
  {
    var error = await _service.ValidateForNewEmployeeAsync(
        [NewDto(), NewDto(barangayId: 999_999)]);

    Assert.NotNull(error);
  }

  [Fact]
  public async Task ValidateForNewEmployeeAsync_TwoAddressesSameBarangay_ReturnsNoError()
  {
    var error = await _service.ValidateForNewEmployeeAsync([NewDto(), NewDto()]);

    Assert.Null(error);
  }
}