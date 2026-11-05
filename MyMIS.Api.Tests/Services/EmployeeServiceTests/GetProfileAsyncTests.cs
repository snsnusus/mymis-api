using MyMIS.Api.DTOs;
using MyMIS.Api.Models;

namespace MyMIS.Api.Tests.Services.EmployeeServiceTests;

public class GetProfileAsyncTests : EmployeeServiceTestBase
{
  private async Task<Employee> SeedJuanAsync()
  {
    var employee = NewEmployee("Juan", "Cruz", "EMP-001");
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();
    return employee;
  }

  [Fact]
  public async Task CorporatePhone_IsShownEvenWhenFlaggedOff()
  {
    var employee = await SeedJuanAsync();
    var phone = NewPhone(employee.Id, "+639171234567", isPrimary: true);
    phone.Ownership = ContactOwnership.Corporate;
    phone.ShowInProfile = false;
    _context.EmployeePhones.Add(phone);
    await _context.SaveChangesAsync();

    var profile = await _service.GetProfileAsync(employee.Id);

    Assert.Equal(phone.Id, Assert.Single(profile!.Phones).Id);
  }

  [Fact]
  public async Task PersonalPhones_OnlyTheFlaggedOneIsShown()
  {
    var employee = await SeedJuanAsync();
    var hidden = NewPhone(employee.Id, "+639171234567", isPrimary: true);
    hidden.ShowInProfile = false;
    var shown = NewPhone(employee.Id, "+639181234567", isPrimary: false);
    shown.ShowInProfile = true;
    _context.EmployeePhones.AddRange(hidden, shown);
    await _context.SaveChangesAsync();

    var profile = await _service.GetProfileAsync(employee.Id);

    Assert.Equal(shown.Id, Assert.Single(profile!.Phones).Id);
  }

  [Fact]
  public async Task Emails_CorporateAlwaysShown_PersonalOnlyWhenFlagged()
  {
    var employee = await SeedJuanAsync();
    var corporate = NewEmail(employee.Id, "juan@company.test", isPrimary: true);
    corporate.Ownership = ContactOwnership.Corporate;
    corporate.ShowInProfile = false;
    var personal = NewEmail(employee.Id, "juan@example.com", isPrimary: false);
    personal.ShowInProfile = false;
    _context.EmployeeEmails.AddRange(corporate, personal);
    await _context.SaveChangesAsync();

    var profile = await _service.GetProfileAsync(employee.Id);

    Assert.Equal(corporate.Id, Assert.Single(profile!.Emails).Id);
  }

  [Fact]
  public async Task Addresses_OnlyTheFlaggedOneAppearsAsCityAndRegion()
  {
    var employee = await SeedJuanAsync();
    var barangay = await SeedBarangayAsync();
    var shown = NewAddress(employee.Id, barangay.Id, isPrimary: true);
    shown.ShowInProfile = true;
    var hidden = NewAddress(employee.Id, barangay.Id, isPrimary: false);
    hidden.ShowInProfile = false;
    _context.EmployeeAddresses.AddRange(shown, hidden);
    await _context.SaveChangesAsync();

    var profile = await _service.GetProfileAsync(employee.Id);

    Assert.Equal(new EmployeeProfileLocationDto("Quezon City", "NCR"), Assert.Single(profile!.Locations));
  }

  [Fact]
  public async Task TwoFlaggedAddressesInTheSameCity_AreListedOnce()
  {
    var employee = await SeedJuanAsync();
    var barangay = await SeedBarangayAsync();
    var first = NewAddress(employee.Id, barangay.Id, isPrimary: true);
    first.ShowInProfile = true;
    var second = NewAddress(employee.Id, barangay.Id, isPrimary: false);
    second.ShowInProfile = true;
    _context.EmployeeAddresses.AddRange(first, second);
    await _context.SaveChangesAsync();

    var profile = await _service.GetProfileAsync(employee.Id);

    Assert.Single(profile!.Locations);
  }

  [Fact]
  public async Task GenderAndBirthday_AreHiddenByDefault()
  {
    var employee = NewEmployee("Juan", "Cruz", "EMP-001");
    employee.Birthdate = new DateOnly(1990, 3, 14);
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    var profile = await _service.GetProfileAsync(employee.Id);

    Assert.Null(profile!.Gender);
    Assert.Null(profile.Birthday);
  }

  [Fact]
  public async Task GenderAndBirthday_WhenFlagged_ShowGenderAndMonthDayOnly()
  {
    var employee = NewEmployee("Juan", "Cruz", "EMP-001");
    employee.Birthdate = new DateOnly(1990, 3, 14);
    employee.ShowGenderInProfile = true;
    employee.ShowBirthdayInProfile = true;
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    var profile = await _service.GetProfileAsync(employee.Id);

    Assert.Equal("MALE", profile!.Gender);
    Assert.Equal(new EmployeeBirthdayDto(3, 14), profile.Birthday);
  }

  [Fact]
  public async Task EmployeeThatDoesNotExist_ReturnsNull()
  {
    var profile = await _service.GetProfileAsync(999_999);

    Assert.Null(profile);
  }
}