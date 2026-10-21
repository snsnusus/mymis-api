using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Models;

namespace MyMIS.Api.Tests.Services.EmployeeServiceTests;

public class HobbyTests : EmployeeServiceTestBase
{
  [Fact]
  public async Task LinkHobbyAsync_NewHobby_CreatesHobbyAndLinksToEmployee()
  {
    // Arrange
    var employee = new Employee
    {
      FirstName = "John",
      LastName = "Doe",
      Gender = "MALE",
      MaritalStatus = "SINGLE",
      EmployeeCode = "EMP-001",
      Username = "johndoe",
      PasswordHash = "irrelevant-for-this-test"
    };
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    var hobby = "Ice Skating";

    // Act
    var result = await _service.LinkHobbyAsync(employee.Id, hobby);

    // Assert
    Assert.NotNull(result);
    Assert.True(result.Id > 0);
    Assert.Equal("Ice Skating", result.Name);

    var savedHobby = await _context.EmployeeHobbies
        .FirstOrDefaultAsync(eh => eh.EmployeeId == employee.Id && eh.HobbyId == result.Id);

    Assert.NotNull(savedHobby);
  }

  [Fact]
  public async Task LinkHobbyAsync_SameHobbyLinkedTwice_DoesntCreateDuplicateLink()
  {
    // Arrange
    var employee = new Employee
    {
      FirstName = "John",
      LastName = "Doe",
      Gender = "MALE",
      MaritalStatus = "SINGLE",
      EmployeeCode = "EMP-001",
      Username = "johndoe",
      PasswordHash = "irrelevant-for-this-test"
    };
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    var hobby = "Ice Skating";

    // Act
    var result1 = await _service.LinkHobbyAsync(employee.Id, hobby);
    var result2 = await _service.LinkHobbyAsync(employee.Id, hobby);

    // Assert
    Assert.NotNull(result1);
    Assert.NotNull(result2);

    Assert.Equal(result1.Id, result2.Id);

    var hobbyCount = await _context.EmployeeHobbies
        .CountAsync(eh => eh.EmployeeId == employee.Id && eh.HobbyId == result1.Id);

    Assert.Equal(1, hobbyCount);
  }

  [Fact]
  public async Task LinkHobbyAsync_NonExistentId_ReturnsNull()
  {
    // Act
    var result = await _service.LinkHobbyAsync(999, "Ice Skating");

    // Assert
    Assert.Null(result);

    var hobbyCount = await _context.Hobbies.CountAsync();
    Assert.Equal(0, hobbyCount);
  }

  [Fact]
  public async Task UnlinkHobbyAsync_ExistingLink_RemovesTheLink()
  {
    // Arrange
    var employee = new Employee
    {
      FirstName = "John",
      LastName = "Doe",
      Gender = "MALE",
      MaritalStatus = "SINGLE",
      EmployeeCode = "EMP-001",
      Username = "johndoe",
      PasswordHash = "irrelevant-for-this-test"
    };
    _context.Employees.Add(employee);

    var hobby = new Hobby { Name = "Reading", NormalizedName = "READING" };
    _context.Hobbies.Add(hobby);

    await _context.SaveChangesAsync();

    _context.EmployeeHobbies.Add(new EmployeeHobby { EmployeeId = employee.Id, HobbyId = hobby.Id });
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.UnlinkHobbyAsync(employee.Id, hobby.Id);

    // Assert
    Assert.True(result);

    var hobbyLink = await _context.EmployeeHobbies
        .FirstOrDefaultAsync(eh => eh.EmployeeId == employee.Id && eh.HobbyId == hobby.Id);
    Assert.Null(hobbyLink);
  }

  [Fact]
  public async Task UnlinkHobbyAsync_NonExistentLink_DoesntDeleteTheHobby()
  {
    // Arrange
    var employee = new Employee
    {
      FirstName = "John",
      LastName = "Doe",
      Gender = "MALE",
      MaritalStatus = "SINGLE",
      EmployeeCode = "EMP-001",
      Username = "johndoe",
      PasswordHash = "irrelevant-for-this-test"
    };
    _context.Employees.Add(employee);

    var hobby = new Hobby { Name = "Reading", NormalizedName = "READING" };
    _context.Hobbies.Add(hobby);

    await _context.SaveChangesAsync();

    // Act
    var result = await _service.UnlinkHobbyAsync(employee.Id, hobby.Id);

    // Assert
    Assert.False(result);

    var hobbyStillExists = await _context.Hobbies.FindAsync(hobby.Id);
    Assert.NotNull(hobbyStillExists);
  }

  [Fact]
  public async Task UnlinkHobbyAsync_ExistingLink_PreservesTheHobbyRow()
  {
    // Arrange
    var employee = new Employee
    {
      FirstName = "John",
      LastName = "Doe",
      Gender = "MALE",
      MaritalStatus = "SINGLE",
      EmployeeCode = "EMP-001",
      Username = "johndoe",
      PasswordHash = "irrelevant-for-this-test"
    };
    _context.Employees.Add(employee);

    var hobby = new Hobby { Name = "Reading", NormalizedName = "READING" };
    _context.Hobbies.Add(hobby);

    await _context.SaveChangesAsync();

    _context.EmployeeHobbies.Add(new EmployeeHobby { EmployeeId = employee.Id, HobbyId = hobby.Id });
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.UnlinkHobbyAsync(employee.Id, hobby.Id);

    // Assert
    Assert.True(result);

    var link = await _context.EmployeeHobbies
        .FirstOrDefaultAsync(eh => eh.EmployeeId == employee.Id && eh.HobbyId == hobby.Id);
    Assert.Null(link);

    var hobbyStillExists = await _context.Hobbies.FindAsync(hobby.Id);
    Assert.NotNull(hobbyStillExists);
    Assert.Equal("Reading", hobbyStillExists.Name);
  }
}