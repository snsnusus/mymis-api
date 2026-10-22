using Microsoft.EntityFrameworkCore;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;

namespace MyMIS.Api.Tests.Services.EmployeeServiceTests;

public class DeleteAsyncTests : EmployeeServiceTestBase
{
  [Fact]
  public async Task DeleteAsync_SoftDeletes_RowRemainsButExcludedFromQueries()
  {
    // Arrange
    var createDto = new EmployeeCreateDto
    {
      FirstName = "Carla",
      LastName = "Reyes",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "creyes",
      Password = "SomePassword1"
    };
    var created = await _service.CreateAsync(createDto, 1);

    // Act
    var deleteResult = await _service.DeleteAsync(created.Id);

    // Assert
    Assert.True(deleteResult);

    // Normal query respects the global filter — should NOT find it
    var viaService = await _service.GetByIdAsync(created.Id, includePrivateDetails: false);
    Assert.Null(viaService);

    // Bypassing the filter proves the row still physically exists
    var rawRow = await _context.Employees
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(e => e.Id == created.Id);

    Assert.NotNull(rawRow);
    Assert.NotNull(rawRow.DeletedAt);
  }

  [Fact]
  public async Task DeleteAsync_EmployeeWithRelatedRows_SoftDeletesEveryRowWithTheEmployeesTimestamp()
  {
    // Arrange
    var employee = NewEmployee("Joe", "Smith", "EMP-001");
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    var address = NewAddress(employee.Id, barangayId: FakeBarangayId, isPrimary: true);
    var email = NewEmail(employee.Id, "joe.smith@hotmail.com", isPrimary: true);
    var phone = NewPhone(employee.Id, "+639181234567", isPrimary: true);
    var emergencyContact = NewContact(employee.Id, "Maggy", "Smith", "+639191234567", isPrimary: true, barangayId: FakeBarangayId);
    var hmo = NewHmo(employee.Id, dependentCount: 1);
    var governmentIds = NewGovernmentIds(employee.Id);

    _context.EmployeeAddresses.Add(address);
    _context.EmployeeEmails.Add(email);
    _context.EmployeePhones.Add(phone);
    _context.EmergencyContacts.Add(emergencyContact);
    _context.EmployeeHmos.Add(hmo);
    _context.EmployeeGovernmentIds.Add(governmentIds);

    await _context.SaveChangesAsync();

    // Act
    var result = await _service.DeleteAsync(employee.Id);
    _context.ChangeTracker.Clear();

    // Assert
    Assert.True(result);
    var deletedEmployee = await _context.Employees
      .IgnoreQueryFilters()
      .SingleAsync(e => e.Id == employee.Id);

    Assert.NotNull(deletedEmployee.DeletedAt);
    var expected = deletedEmployee.DeletedAt;

    var deletedEmployeeAddresses = await _context.EmployeeAddresses
      .IgnoreQueryFilters()
      .Where(ea => ea.EmployeeId == employee.Id)
      .ToListAsync();
    Assert.NotEmpty(deletedEmployeeAddresses);
    Assert.All(deletedEmployeeAddresses, a => Assert.Equal(expected, a.DeletedAt));

    var deletedEmployeeEmails = await _context.EmployeeEmails
      .IgnoreQueryFilters()
      .Where(ee => ee.EmployeeId == employee.Id)
      .ToListAsync();
    Assert.NotEmpty(deletedEmployeeEmails);
    Assert.All(deletedEmployeeEmails, e => Assert.Equal(expected, e.DeletedAt));

    var deletedEmployeePhones = await _context.EmployeePhones
      .IgnoreQueryFilters()
      .Where(ep => ep.EmployeeId == employee.Id)
      .ToListAsync();
    Assert.NotEmpty(deletedEmployeePhones);
    Assert.All(deletedEmployeePhones, p => Assert.Equal(expected, p.DeletedAt));

    var deletedEmployeeEmergencyContacts = await _context.EmergencyContacts
      .IgnoreQueryFilters()
      .Where(ec => ec.EmployeeId == employee.Id)
      .ToListAsync();
    Assert.NotEmpty(deletedEmployeeEmergencyContacts);
    Assert.All(deletedEmployeeEmergencyContacts, ec => Assert.Equal(expected, ec.DeletedAt));

    var deletedEmployeeHmo = await _context.EmployeeHmos
      .IgnoreQueryFilters()
      .Include(h => h.Dependents)
      .SingleAsync(h => h.EmployeeId == employee.Id);
    Assert.Equal(expected, deletedEmployeeHmo.DeletedAt);
    Assert.NotEmpty(deletedEmployeeHmo.Dependents);
    Assert.All(deletedEmployeeHmo.Dependents, d => Assert.Equal(expected, d.DeletedAt));

    var deletedEmployeeGovernmentIds = await _context.EmployeeGovernmentIds
      .IgnoreQueryFilters()
      .SingleAsync(g => g.EmployeeId == employee.Id);
    Assert.Equal(expected, deletedEmployeeGovernmentIds.DeletedAt);
  }

  [Fact]
  public async Task DeleteAsync_EmployeeWithPrimaryAndSecondaryRows_KeepsIsPrimaryAsItWas()
  {
    // Arrange
    var employee = NewEmployee("Joe", "Smith", "EMP-001");
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    var primaryAddress = NewAddress(employee.Id, barangayId: FakeBarangayId, isPrimary: true);
    var secondaryAddress = NewAddress(employee.Id, barangayId: FakeBarangayId, isPrimary: false);

    var primaryEmail = NewEmail(employee.Id, "joe.smith@hotmail.com", isPrimary: true);
    var secondaryEmail = NewEmail(employee.Id, "jsmith@gmail.com", isPrimary: false);

    var primaryPhone = NewPhone(employee.Id, "+639171234567", isPrimary: true);
    var secondaryPhone = NewPhone(employee.Id, "+639187654321", isPrimary: false);

    var primaryContact = NewContact(employee.Id, "Dave", "Smith", "+639210001234", isPrimary: true, barangayId: FakeBarangayId);
    var secondaryContact = NewContact(employee.Id, "Karen", "Smith", "+639221110000", isPrimary: false, barangayId: FakeBarangayId);

    _context.EmployeeAddresses.AddRange(primaryAddress, secondaryAddress);
    _context.EmployeeEmails.AddRange(primaryEmail, secondaryEmail);
    _context.EmployeePhones.AddRange(primaryPhone, secondaryPhone);
    _context.EmergencyContacts.AddRange(primaryContact, secondaryContact);

    await _context.SaveChangesAsync();

    // Act
    var result = await _service.DeleteAsync(employee.Id);
    _context.ChangeTracker.Clear();

    // Assert
    Assert.True(result);
    var deletedEmployee = await _context.Employees
      .IgnoreQueryFilters()
      .SingleAsync(e => e.Id == employee.Id);

    Assert.NotNull(deletedEmployee.DeletedAt);
    var expected = deletedEmployee.DeletedAt;

    var deletedPrimaryAddress = await _context.EmployeeAddresses
      .IgnoreQueryFilters()
      .SingleAsync(ea => ea.Id == primaryAddress.Id);
    Assert.True(deletedPrimaryAddress.IsPrimary);
    Assert.Equal(expected, deletedPrimaryAddress.DeletedAt);

    var deletedSecondaryAddress = await _context.EmployeeAddresses
      .IgnoreQueryFilters()
      .SingleAsync(ea => ea.Id == secondaryAddress.Id);
    Assert.False(deletedSecondaryAddress.IsPrimary);
    Assert.Equal(expected, deletedSecondaryAddress.DeletedAt);

    var deletedPrimaryEmail = await _context.EmployeeEmails
      .IgnoreQueryFilters()
      .SingleAsync(ee => ee.Id == primaryEmail.Id);
    Assert.True(deletedPrimaryEmail.IsPrimary);
    Assert.Equal(expected, deletedPrimaryEmail.DeletedAt);

    var deletedSecondaryEmail = await _context.EmployeeEmails
      .IgnoreQueryFilters()
      .SingleAsync(ee => ee.Id == secondaryEmail.Id);
    Assert.False(deletedSecondaryEmail.IsPrimary);
    Assert.Equal(expected, deletedSecondaryEmail.DeletedAt);

    var deletedPrimaryPhone = await _context.EmployeePhones
      .IgnoreQueryFilters()
      .SingleAsync(ep => ep.Id == primaryPhone.Id);
    Assert.True(deletedPrimaryPhone.IsPrimary);
    Assert.Equal(expected, deletedPrimaryPhone.DeletedAt);

    var deletedSecondaryPhone = await _context.EmployeePhones
      .IgnoreQueryFilters()
      .SingleAsync(ep => ep.Id == secondaryPhone.Id);
    Assert.False(deletedSecondaryPhone.IsPrimary);
    Assert.Equal(expected, deletedSecondaryPhone.DeletedAt);

    var deletedPrimaryContact = await _context.EmergencyContacts
      .IgnoreQueryFilters()
      .SingleAsync(ec => ec.Id == primaryContact.Id);
    Assert.True(deletedPrimaryContact.IsPrimary);
    Assert.Equal(expected, deletedPrimaryContact.DeletedAt);

    var deletedSecondaryContact = await _context.EmergencyContacts
      .IgnoreQueryFilters()
      .SingleAsync(ec => ec.Id == secondaryContact.Id);
    Assert.False(deletedSecondaryContact.IsPrimary);
    Assert.Equal(expected, deletedSecondaryContact.DeletedAt);
  }

  [Fact]
  public async Task DeleteAsync_RowDeletedEarlier_KeepsItsOriginalDeletedAt()
  {
    // Arrange
    var employee = NewEmployee("Joe", "Smith", "EMP-001");
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    var livePhone = NewPhone(employee.Id, "+639191234567", isPrimary: true);

    var earlierDeletedAt = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
    var oldPhone = NewPhone(employee.Id, "+639180000000", isPrimary: false);
    oldPhone.DeletedAt = earlierDeletedAt;

    _context.EmployeePhones.AddRange(livePhone, oldPhone);
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.DeleteAsync(employee.Id);
    _context.ChangeTracker.Clear();

    // Assert
    Assert.True(result);
    var deletedEmployee = await _context.Employees
      .IgnoreQueryFilters()
      .SingleAsync(e => e.Id == employee.Id);

    Assert.NotNull(deletedEmployee.DeletedAt);
    var expected = deletedEmployee.DeletedAt;

    var deletedLivePhone = await _context.EmployeePhones
      .IgnoreQueryFilters()
      .SingleAsync(ep => ep.Id == livePhone.Id);
    Assert.Equal(expected, deletedLivePhone.DeletedAt);

    var deletedOldPhone = await _context.EmployeePhones
      .IgnoreQueryFilters()
      .SingleAsync(ep => ep.Id == oldPhone.Id);
    Assert.Equal(earlierDeletedAt, deletedOldPhone.DeletedAt);
  }

  [Fact]
  public async Task DeleteAsync_OtherEmployeesRows_AreNotTouched()
  {
    // Arrange
    var employeeJoe = NewEmployee("Joe", "Smith", "EMP-001");
    var employeeJane = NewEmployee("Jane", "Doe", "EMP-002");

    _context.Employees.AddRange(employeeJoe, employeeJane);
    await _context.SaveChangesAsync();

    // Setup Employee Joe's Details
    var employeeJoeAddress = NewAddress(employeeJoe.Id, barangayId: FakeBarangayId, isPrimary: true);
    var employeeJoeEmail = NewEmail(employeeJoe.Id, "joe.smith@hotmail.com", isPrimary: true);
    var employeeJoePhone = NewPhone(employeeJoe.Id, "+639190001111", isPrimary: true);
    var employeeJoeContact = NewContact(employeeJoe.Id, "Seraphina", "Smith", "+639191110000", isPrimary: true, barangayId: FakeBarangayId);
    var employeeJoeHmo = NewHmo(employeeJoe.Id, dependentCount: 1);
    var employeeJoeGovernmentIds = NewGovernmentIds(employeeJoe.Id, sssNumber: "0000011111", philHealthNumber: "000000111111", pagIbigNumber: "111111000000", tinNumber: "000001111");

    // Setup Employee Jane's Details
    var employeeJaneAddress = NewAddress(employeeJane.Id, barangayId: FakeBarangayId, isPrimary: true);
    var employeeJaneEmail = NewEmail(employeeJane.Id, "jane.doe@gmail.com", isPrimary: true);
    var employeeJanePhone = NewPhone(employeeJane.Id, "+639192223333", isPrimary: true);
    var employeeJaneContact = NewContact(employeeJane.Id, "Sarah", "Doe", "+639193332222", isPrimary: true, barangayId: FakeBarangayId);
    var employeeJaneHmo = NewHmo(employeeJane.Id, dependentCount: 1);
    var employeeJaneGovernmentIds = NewGovernmentIds(employeeJane.Id, sssNumber: "2222233333", philHealthNumber: "222222333333", pagIbigNumber: "333333222222", tinNumber: "222223333");

    _context.EmployeeAddresses.AddRange(employeeJoeAddress, employeeJaneAddress);
    _context.EmployeeEmails.AddRange(employeeJoeEmail, employeeJaneEmail);
    _context.EmployeePhones.AddRange(employeeJoePhone, employeeJanePhone);
    _context.EmergencyContacts.AddRange(employeeJoeContact, employeeJaneContact);
    _context.EmployeeHmos.AddRange(employeeJoeHmo, employeeJaneHmo);
    _context.EmployeeGovernmentIds.AddRange(employeeJoeGovernmentIds, employeeJaneGovernmentIds);

    await _context.SaveChangesAsync();

    // Act
    var result = await _service.DeleteAsync(employeeJoe.Id);
    _context.ChangeTracker.Clear();

    // Assert
    Assert.True(result);

    var deletedJoePhone = await _context.EmployeePhones
      .IgnoreQueryFilters()
      .SingleAsync(ep => ep.Id == employeeJoePhone.Id);
    Assert.NotNull(deletedJoePhone.DeletedAt);

    var retainedEmployeeJane = await _context.Employees
      .FindAsync(employeeJane.Id);

    Assert.NotNull(retainedEmployeeJane);
    Assert.Null(retainedEmployeeJane.DeletedAt);

    var retainedEmployeeJaneAddresses = await _context.EmployeeAddresses
      .IgnoreQueryFilters()
      .Where(ea => ea.EmployeeId == employeeJane.Id)
      .ToListAsync();
    Assert.NotEmpty(retainedEmployeeJaneAddresses);
    Assert.All(retainedEmployeeJaneAddresses, a => Assert.Null(a.DeletedAt));

    var retainedEmployeeJaneEmails = await _context.EmployeeEmails
      .IgnoreQueryFilters()
      .Where(ee => ee.EmployeeId == employeeJane.Id)
      .ToListAsync();
    Assert.NotEmpty(retainedEmployeeJaneEmails);
    Assert.All(retainedEmployeeJaneEmails, e => Assert.Null(e.DeletedAt));

    var retainedEmployeeJanePhones = await _context.EmployeePhones
      .IgnoreQueryFilters()
      .Where(ep => ep.EmployeeId == employeeJane.Id)
      .ToListAsync();
    Assert.NotEmpty(retainedEmployeeJanePhones);
    Assert.All(retainedEmployeeJanePhones, p => Assert.Null(p.DeletedAt));

    var retainedEmployeeJaneContacts = await _context.EmergencyContacts
      .IgnoreQueryFilters()
      .Where(ec => ec.EmployeeId == employeeJane.Id)
      .ToListAsync();
    Assert.NotEmpty(retainedEmployeeJaneContacts);
    Assert.All(retainedEmployeeJaneContacts, ec => Assert.Null(ec.DeletedAt));

    var retainedEmployeeJaneHmo = await _context.EmployeeHmos
      .IgnoreQueryFilters()
      .Include(h => h.Dependents)
      .SingleAsync(eh => eh.EmployeeId == employeeJane.Id);
    Assert.Null(retainedEmployeeJaneHmo.DeletedAt);
    Assert.NotEmpty(retainedEmployeeJaneHmo.Dependents);
    Assert.All(retainedEmployeeJaneHmo.Dependents, d => Assert.Null(d.DeletedAt));

    var retainedEmployeeJaneGovernmentIds = await _context.EmployeeGovernmentIds
      .IgnoreQueryFilters()
      .SingleAsync(eg => eg.EmployeeId == employeeJane.Id);
    Assert.Null(retainedEmployeeJaneGovernmentIds.DeletedAt);
  }

  [Fact]
  public async Task DeleteAsync_EmployeeWithActiveRefreshTokens_RevokesThemWithTheEmployeesTimestamp()
  {
    // Arrange
    var employee = NewEmployee("Joe", "Smith", "EMP-001");
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    var token1 = NewRefreshToken(employee.Id, "hash-active-1");
    var token2 = NewRefreshToken(employee.Id, "hash-active-2");

    _context.RefreshTokens.AddRange(token1, token2);
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.DeleteAsync(employee.Id);
    _context.ChangeTracker.Clear();

    // Assert
    Assert.True(result);

    var deletedEmployee = await _context.Employees
      .IgnoreQueryFilters()
      .SingleAsync(e => e.Id == employee.Id);
    Assert.NotNull(deletedEmployee.DeletedAt);
    var expected = deletedEmployee.DeletedAt;

    var tokens = await _context.RefreshTokens
      .Where(rt => rt.EmployeeId == employee.Id)
      .ToListAsync();

    Assert.Equal(2, tokens.Count);
    Assert.All(tokens, t => Assert.Equal(expected, t.RevokedAt));
  }

  [Fact]
  public async Task DeleteAsync_RevokedRefreshToken_KeepItsOldTimestamp()
  {
    // Arrange
    var earlierRevokedAt = now.AddDays(-3);
    var employee = NewEmployee("Joe", "Smith", "EMP-001");
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    var activeToken = NewRefreshToken(employee.Id, "hash-active-1");
    var revokedToken = NewRefreshToken(employee.Id, "hash-revoked-1", earlierRevokedAt);

    _context.RefreshTokens.AddRange(revokedToken, activeToken);
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.DeleteAsync(employee.Id);
    _context.ChangeTracker.Clear();

    // Assert
    Assert.True(result);

    var deletedEmployee = await _context.Employees
      .IgnoreQueryFilters()
      .SingleAsync(e => e.Id == employee.Id);
    Assert.NotNull(deletedEmployee.DeletedAt);
    var expected = deletedEmployee.DeletedAt;

    var recentlyRevokedToken = await _context.RefreshTokens
      .SingleAsync(rt => rt.EmployeeId == employee.Id && rt.Id == activeToken.Id);
    Assert.Equal(expected, recentlyRevokedToken.RevokedAt);

    var alreadyRevokedToken = await _context.RefreshTokens
      .SingleAsync(rt => rt.EmployeeId == employee.Id && rt.Id == revokedToken.Id);
    Assert.Equal(earlierRevokedAt, alreadyRevokedToken.RevokedAt);
  }

  [Fact]
  public async Task DeleteAsync_OtherEmployeesRefreshTokens_AreNotRevoked()
  {
    // Arrange
    var employee1 = NewEmployee("Joe", "Smith", "EMP-001");
    var employee2 = NewEmployee("Jane", "Doe", "EMP-002");
    _context.Employees.AddRange(employee1, employee2);
    await _context.SaveChangesAsync();

    var employee1Token = NewRefreshToken(employee1.Id, "hash-active-1");
    var employee2Token = NewRefreshToken(employee2.Id, "hash-active-2");
    _context.RefreshTokens.AddRange(employee1Token, employee2Token);
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.DeleteAsync(employee1.Id);
    _context.ChangeTracker.Clear();

    // Assert
    Assert.True(result);

    var deletedEmployee = await _context.Employees
      .IgnoreQueryFilters()
      .SingleAsync(e => e.Id == employee1.Id);
    Assert.NotNull(deletedEmployee.DeletedAt);
    var expected = deletedEmployee.DeletedAt;

    var deletedEmployeeToken = await _context.RefreshTokens
      .SingleAsync(rt => rt.EmployeeId == employee1.Id);
    Assert.Equal(expected, deletedEmployeeToken.RevokedAt);

    var activeEmployeeToken = await _context.RefreshTokens
      .SingleAsync(rt => rt.EmployeeId == employee2.Id);
    Assert.Null(activeEmployeeToken.RevokedAt);
  }

}