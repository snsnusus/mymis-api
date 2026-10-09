using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Helpers;
using MyMIS.Api.IntegrationTests.Infrastructure;
using MyMIS.Api.Models;
using Npgsql;
using static MyMIS.Api.IntegrationTests.Infrastructure.TestData;

namespace MyMIS.Api.IntegrationTests;

// These talk to Postgres directly, with no HTTP in between. They check that the database itself
// enforces the rules (indexes and foreign keys), which the InMemory unit tests could never do.
[Collection(IntegrationCollection.Name)]
public class DatabaseConstraintTests(ApiFactory factory)
{
  // ---------------- helpers ----------------

  // Runs the action, expects Postgres to reject it as a unique violation, and returns the index's name.
  private static async Task<string?> UniqueViolationOfAsync(Func<Task> action)
  {
    var exception = await Assert.ThrowsAsync<DbUpdateException>(action);
    return exception.GetUniqueViolationConstraint();
  }

  private static async Task AssertForeignKeyViolationAsync(Func<Task> action)
  {
    var exception = await Assert.ThrowsAsync<DbUpdateException>(action);
    var postgres = Assert.IsType<PostgresException>(exception.InnerException);
    Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, postgres.SqlState);
  }

  // All timestamps are UTC: Npgsql refuses to save any other kind to a timestamptz column.
  private static EmployeeEmail NewEmail(int employeeId, string email, bool isPrimary, DateTime? deletedAt = null) => new()
  {
    EmployeeId = employeeId,
    Ownership = ContactOwnership.Personal,
    Email = email,
    IsPrimary = isPrimary,
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow,
    DeletedAt = deletedAt,
  };

  private static EmployeePhone NewPhone(
    int employeeId, string number, PhoneLineType lineType, bool isPrimary, DateTime? deletedAt = null) => new()
    {
      EmployeeId = employeeId,
      Ownership = ContactOwnership.Personal,
      LineType = lineType,
      Phone = new Phone { CountryCode = "PH", Number = number },
      IsPrimary = isPrimary,
      CreatedAt = DateTime.UtcNow,
      UpdatedAt = DateTime.UtcNow,
      DeletedAt = deletedAt,
    };

  private static EmergencyContact NewContact(
    int employeeId, bool isPrimary, DateTime? deletedAt = null, int? barangayId = null) => new()
    {
      EmployeeId = employeeId,
      FirstName = "Ana",
      LastName = "Santos",
      Relationship = EmergencyContactRelationship.Parent,
      Phone = new Phone { CountryCode = "PH", Number = "+639171234567" },
      Address = barangayId is null
      ? null
      : new Address { AddressLine1 = "123 Mabini St.", BarangayId = barangayId.Value, PostalCode = "1105" },
      IsPrimary = isPrimary,
      CreatedAt = DateTime.UtcNow,
      UpdatedAt = DateTime.UtcNow,
      DeletedAt = deletedAt,
    };

  // A unique mobile-looking number: the database doesn't validate the format, only the uniqueness.
  private static string NewNumber() => $"+6391{Random.Shared.Next(10_000_000, 99_999_999)}";

  private Task AddEmailAsync(EmployeeEmail email) =>
    factory.ExecuteDbAsync(async db =>
    {
      db.EmployeeEmails.Add(email);
      await db.SaveChangesAsync();
    });

  private Task AddPhoneAsync(EmployeePhone phone) =>
    factory.ExecuteDbAsync(async db =>
    {
      db.EmployeePhones.Add(phone);
      await db.SaveChangesAsync();
    });

  private Task AddContactAsync(EmergencyContact contact) =>
    factory.ExecuteDbAsync(async db =>
    {
      db.EmergencyContacts.Add(contact);
      await db.SaveChangesAsync();
    });

  // ---------------- emails ----------------

  [Fact]
  public async Task EmployeeEmails_TwoPrimaryEmailsForOneEmployee_AreRejected()
  {
    // Arrange
    var employee = await factory.SeedEmployeeAsync();
    await AddEmailAsync(NewEmail(employee.Id, $"{Unique()}@example.com", isPrimary: true));

    // Act
    var constraint = await UniqueViolationOfAsync(
      () => AddEmailAsync(NewEmail(employee.Id, $"{Unique()}@example.com", isPrimary: true)));

    // Assert
    Assert.Equal("IX_EmployeeEmails_EmployeeId_Primary", constraint);
  }

  [Fact]
  public async Task EmployeeEmails_SoftDeletedPrimary_DoesNotBlockANewPrimary()
  {
    // Arrange: the index only covers rows where IsPrimary is true AND DeletedAt IS NULL
    var employee = await factory.SeedEmployeeAsync();
    await AddEmailAsync(NewEmail(employee.Id, $"{Unique()}@example.com", isPrimary: true, deletedAt: DateTime.UtcNow));

    // Act
    await AddEmailAsync(NewEmail(employee.Id, $"{Unique()}@example.com", isPrimary: true));

    // Assert: one live primary exists, and the deleted one is still stored
    var counts = await factory.QueryDbAsync(async db => (
      Live: await db.EmployeeEmails.CountAsync(e => e.EmployeeId == employee.Id && e.IsPrimary),
      All: await db.EmployeeEmails.IgnoreQueryFilters().CountAsync(e => e.EmployeeId == employee.Id)));

    Assert.Equal(1, counts.Live);
    Assert.Equal(2, counts.All);
  }

  [Fact]
  public async Task EmployeeEmails_SameAddressForTwoEmployees_IsRejected()
  {
    // Arrange
    var first = await factory.SeedEmployeeAsync();
    var second = await factory.SeedEmployeeAsync();
    var email = $"{Unique()}@example.com";
    await AddEmailAsync(NewEmail(first.Id, email, isPrimary: true));

    // Act
    var constraint = await UniqueViolationOfAsync(() => AddEmailAsync(NewEmail(second.Id, email, isPrimary: true)));

    // Assert
    Assert.Equal("IX_EmployeeEmails_Email", constraint);
  }

  [Fact]
  public async Task EmployeeEmails_AddressOfASoftDeletedEmail_CanBeReused()
  {
    // Arrange
    var first = await factory.SeedEmployeeAsync();
    var second = await factory.SeedEmployeeAsync();
    var email = $"{Unique()}@example.com";
    await AddEmailAsync(NewEmail(first.Id, email, isPrimary: true, deletedAt: DateTime.UtcNow));

    // Act + Assert: no exception
    await AddEmailAsync(NewEmail(second.Id, email, isPrimary: true));
  }

  // ---------------- phones ----------------

  [Fact]
  public async Task EmployeePhones_SameMobileForTwoEmployees_IsRejected()
  {
    // Arrange
    var first = await factory.SeedEmployeeAsync();
    var second = await factory.SeedEmployeeAsync();
    var number = NewNumber();
    await AddPhoneAsync(NewPhone(first.Id, number, PhoneLineType.Mobile, isPrimary: true));

    // Act
    var constraint = await UniqueViolationOfAsync(
      () => AddPhoneAsync(NewPhone(second.Id, number, PhoneLineType.Mobile, isPrimary: true)));

    // Assert
    Assert.Equal("IX_EmployeePhones_Phone_Number_Mobile", constraint);
  }

  [Fact]
  public async Task EmployeePhones_SameLandlineForTwoEmployees_IsAllowed()
  {
    // Arrange: only mobile numbers must be unique, because a landline is often shared
    var first = await factory.SeedEmployeeAsync();
    var second = await factory.SeedEmployeeAsync();
    var number = $"+6328{Random.Shared.Next(1_000_000, 9_999_999)}";
    await AddPhoneAsync(NewPhone(first.Id, number, PhoneLineType.Landline, isPrimary: true));

    // Act + Assert: no exception
    await AddPhoneAsync(NewPhone(second.Id, number, PhoneLineType.Landline, isPrimary: true));
  }

  [Fact]
  public async Task EmployeePhones_MobileOfASoftDeletedPhone_CanBeReused()
  {
    // Arrange
    var first = await factory.SeedEmployeeAsync();
    var second = await factory.SeedEmployeeAsync();
    var number = NewNumber();
    await AddPhoneAsync(NewPhone(first.Id, number, PhoneLineType.Mobile, isPrimary: true, deletedAt: DateTime.UtcNow));

    // Act + Assert: no exception
    await AddPhoneAsync(NewPhone(second.Id, number, PhoneLineType.Mobile, isPrimary: true));
  }

  [Fact]
  public async Task EmployeePhones_TwoPrimaryPhonesForOneEmployee_AreRejected()
  {
    // Arrange
    var employee = await factory.SeedEmployeeAsync();
    await AddPhoneAsync(NewPhone(employee.Id, NewNumber(), PhoneLineType.Mobile, isPrimary: true));

    // Act
    var constraint = await UniqueViolationOfAsync(
      () => AddPhoneAsync(NewPhone(employee.Id, NewNumber(), PhoneLineType.Mobile, isPrimary: true)));

    // Assert
    Assert.Equal("IX_EmployeePhones_EmployeeId_Primary", constraint);
  }

  // ---------------- emergency contacts ----------------

  [Fact]
  public async Task EmergencyContacts_TwoPrimaryContactsForOneEmployee_AreRejected()
  {
    // Arrange
    var employee = await factory.SeedEmployeeAsync();
    await AddContactAsync(NewContact(employee.Id, isPrimary: true));

    // Act
    var constraint = await UniqueViolationOfAsync(() => AddContactAsync(NewContact(employee.Id, isPrimary: true)));

    // Assert
    Assert.Equal("IX_EmergencyContacts_EmployeeId_Primary", constraint);
  }

  [Fact]
  public async Task EmergencyContacts_SoftDeletedPrimary_DoesNotBlockANewPrimary()
  {
    // Arrange
    var employee = await factory.SeedEmployeeAsync();
    await AddContactAsync(NewContact(employee.Id, isPrimary: true, deletedAt: DateTime.UtcNow));

    // Act + Assert: no exception
    await AddContactAsync(NewContact(employee.Id, isPrimary: true));
  }

  // ---------------- employees ----------------

  [Fact]
  public async Task Employees_DuplicateUsername_IsRejected()
  {
    // Arrange
    var existing = await factory.SeedEmployeeAsync();

    // Act
    var constraint = await UniqueViolationOfAsync(() => factory.ExecuteDbAsync(async db =>
    {
      db.Employees.Add(new Employee
      {
        FirstName = "Test",
        LastName = "Duplicate",
        Gender = "MALE",
        MaritalStatus = "SINGLE",
        EmployeeCode = $"IT-{Unique()}",
        Username = existing.Username,
        PasswordHash = "irrelevant",
      });
      await db.SaveChangesAsync();
    }));

    // Assert
    Assert.Equal("IX_Employees_Username", constraint);
  }

  [Fact]
  public async Task Employees_DuplicateEmployeeCode_IsRejected()
  {
    // Arrange
    var existing = await factory.SeedEmployeeAsync();

    // Act
    var constraint = await UniqueViolationOfAsync(() => factory.ExecuteDbAsync(async db =>
    {
      db.Employees.Add(new Employee
      {
        FirstName = "Test",
        LastName = "Duplicate",
        Gender = "MALE",
        MaritalStatus = "SINGLE",
        EmployeeCode = existing.EmployeeCode,
        Username = $"it{Unique()}",
        PasswordHash = "irrelevant",
      });
      await db.SaveChangesAsync();
    }));

    // Assert: this index name is not in UniqueConstraints, so through the API it would be a generic 409.
    // That is acceptable because employee codes are generated by the server, never typed by a user.
    Assert.Equal("IX_Employees_EmployeeCode", constraint);
  }

  // ---------------- foreign keys ----------------

  [Fact]
  public async Task Regions_DeletingOneThatStillHasACity_IsBlockedByRestrict()
  {
    // Arrange
    var region = await factory.SeedRegionAsync();
    await factory.SeedCityAsync(region);

    // Act + Assert
    await AssertForeignKeyViolationAsync(() => factory.ExecuteDbAsync(async db =>
    {
      db.Regions.Remove((await db.Regions.FindAsync(region.Id))!);
      await db.SaveChangesAsync();
    }));
  }

  [Fact]
  public async Task Cities_DeletingOneThatStillHasABarangay_IsBlockedByRestrict()
  {
    // Arrange
    var city = await factory.SeedCityAsync(await factory.SeedRegionAsync());
    await factory.SeedBarangayAsync(city);

    // Act + Assert
    await AssertForeignKeyViolationAsync(() => factory.ExecuteDbAsync(async db =>
    {
      db.Cities.Remove((await db.Cities.FindAsync(city.Id))!);
      await db.SaveChangesAsync();
    }));
  }

  [Fact]
  public async Task Departments_DeletingOneThatStillHasAPosition_IsBlockedByRestrict()
  {
    // Arrange
    var department = await factory.SeedDepartmentAsync();
    await factory.SeedPositionAsync(department);

    // Act + Assert
    await AssertForeignKeyViolationAsync(() => factory.ExecuteDbAsync(async db =>
    {
      db.Departments.Remove((await db.Departments.FindAsync(department.Id))!);
      await db.SaveChangesAsync();
    }));
  }

  [Fact]
  public async Task Barangays_DeletingOneUsedByAnEmergencyContactAddress_IsBlockedByRestrict()
  {
    // Arrange: the address is an owned type, and its foreign key to Barangay is Restrict too
    var barangay = await factory.SeedBarangayAsync(await factory.SeedCityAsync(await factory.SeedRegionAsync()));
    var employee = await factory.SeedEmployeeAsync();
    await AddContactAsync(NewContact(employee.Id, isPrimary: true, barangayId: barangay.Id));

    // Act + Assert
    await AssertForeignKeyViolationAsync(() => factory.ExecuteDbAsync(async db =>
    {
      db.Barangays.Remove((await db.Barangays.FindAsync(barangay.Id))!);
      await db.SaveChangesAsync();
    }));
  }

  [Fact]
  public async Task Positions_DeletingOneAnEmployeeHolds_SetsTheEmployeesPositionToNull()
  {
    // Arrange
    var position = await factory.SeedPositionAsync(await factory.SeedDepartmentAsync());
    var employee = await factory.SeedEmployeeAsync();

    await factory.ExecuteDbAsync(async db =>
      await db.Employees
        .Where(e => e.Id == employee.Id)
        .ExecuteUpdateAsync(set => set.SetProperty(e => e.PositionId, position.Id)));

    // Act
    await factory.ExecuteDbAsync(async db =>
    {
      db.Positions.Remove((await db.Positions.FindAsync(position.Id))!);
      await db.SaveChangesAsync();
    });

    // Assert: the employee survives, only the link is cleared
    var positionId = await factory.QueryDbAsync(async db =>
      (await db.Employees.AsNoTracking().SingleAsync(e => e.Id == employee.Id)).PositionId);

    Assert.Null(positionId);
  }

  [Fact]
  public async Task Employees_HardDeletingOne_CascadesToTheirEmails()
  {
    // Arrange: employees are normally soft-deleted, so this is the database rule behind the scenes
    var employee = await factory.SeedEmployeeAsync();
    await AddEmailAsync(NewEmail(employee.Id, $"{Unique()}@example.com", isPrimary: true));

    // Act: ExecuteDelete skips the change tracker, so the database's own ON DELETE CASCADE does the work
    await factory.ExecuteDbAsync(async db =>
      await db.Employees.IgnoreQueryFilters().Where(e => e.Id == employee.Id).ExecuteDeleteAsync());

    // Assert
    var remaining = await factory.QueryDbAsync(db =>
      db.EmployeeEmails.IgnoreQueryFilters().CountAsync(e => e.EmployeeId == employee.Id));

    Assert.Equal(0, remaining);
  }
}