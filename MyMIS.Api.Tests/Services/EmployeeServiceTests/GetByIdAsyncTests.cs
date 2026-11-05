using MyMIS.Api.DTOs;
using MyMIS.Api.Models;

namespace MyMIS.Api.Tests.Services.EmployeeServiceTests;

public class GetByIdAsyncTests : EmployeeServiceTestBase
{
  [Fact]
  public async Task GetByIdAsync_NonExistentId_ReturnsNull()
  {
    // Act
    var result = await _service.GetByIdAsync(999, includePrivateDetails: false);

    // Assert
    Assert.Null(result);
  }

  [Fact]
  public async Task GetByIdAsync_ExistingId_ReturnsMatchingEmployee()
  {
    // Arrange
    var employee = new Employee
    {
      FirstName = "John",
      LastName = "Smith",
      Gender = "Male",
      MaritalStatus = "Married",
      EmployeeCode = "EMP002",
      Username = "jsmith",
      PasswordHash = "irrelevant-for-this-test"
    };
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetByIdAsync(employee.Id, includePrivateDetails: false);

    // Assert
    Assert.NotNull(result);
    Assert.Equal("John", result.FirstName);
    Assert.Equal("EMP002", result.EmployeeCode);
  }

  [Fact]
  public async Task GetByIdAsync_EmployeeHasPosition_PopulatesNestedPositionDto()
  {
    // Arrange
    var department = new Department
    {
      Name = "Human Resources",
      Slug = "HRD",
      Status = "Active"
    };
    _context.Departments.Add(department);
    await _context.SaveChangesAsync();

    var position = new Position
    {
      Title = "Manager",
      Slug = "MNGR",
      Description = "This is a sample description.",
      SortOrder = 1,
      IsActive = true,
      IsApprover = true,
      DepartmentId = department.Id,
    };
    _context.Positions.Add(position);
    await _context.SaveChangesAsync();

    var employee = new Employee
    {
      FirstName = "John",
      LastName = "Doe",
      Gender = "MALE",
      MaritalStatus = "SINGLE",
      EmployeeCode = "EMP-001",
      Username = "johndoe",
      PasswordHash = "irrelevant-to-this-test",
      DepartmentId = department.Id,
      PositionId = position.Id
    };
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetByIdAsync(employee.Id, includePrivateDetails: false);

    // Assert
    Assert.NotNull(result);
    Assert.NotNull(result.Position);
    Assert.Equal("Manager", result.Position.Title);
    Assert.Equal("MNGR", result.Position.Slug);
    Assert.Equal("This is a sample description.", result.Position.Description);
    Assert.True(result.Position.IsActive);
    Assert.True(result.Position.IsApprover);
  }

  [Fact]
  public async Task GetByIdAsync_EmployeeHasNoPosition_PositionIsNull()
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
      PasswordHash = "irrelevant-to-this-test",
    };
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetByIdAsync(employee.Id, includePrivateDetails: false);

    // Assert
    Assert.NotNull(result);
    Assert.Null(result.Position);
  }

  [Fact]
  public async Task GetByIdAsync_EmployeeHasHobbies_PopulatesHobbiesList()
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
      PasswordHash = "irrelevant-to-this-test",
    };
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    var hobby1 = new Hobby
    {
      Name = "Reading",
      NormalizedName = "READING"
    };
    var hobby2 = new Hobby
    {
      Name = "Chess",
      NormalizedName = "CHESS"
    };
    _context.Hobbies.AddRange(hobby1, hobby2);
    await _context.SaveChangesAsync();

    _context.EmployeeHobbies.Add(new EmployeeHobby { EmployeeId = employee.Id, HobbyId = hobby1.Id });
    _context.EmployeeHobbies.Add(new EmployeeHobby { EmployeeId = employee.Id, HobbyId = hobby2.Id });
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetByIdAsync(employee.Id, includePrivateDetails: false);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(2, result.Hobbies.Count);
    Assert.Contains(result.Hobbies, h => h.Id == hobby1.Id && h.Name == "Reading");
    Assert.Contains(result.Hobbies, h => h.Id == hobby2.Id && h.Name == "Chess");
  }

  [Fact]
  public async Task GetByIdAsync_EmployeeHasNoHobbies_ReturnsEmptyListNotNull()
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
      PasswordHash = "irrelevant-to-this-test",
    };
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetByIdAsync(employee.Id, includePrivateDetails: false);

    // Assert
    Assert.NotNull(result);
    Assert.NotNull(result.Hobbies);
    Assert.Empty(result.Hobbies);
  }

  [Fact]
  public async Task GetByIdAsync_LegacyEmployeeWithoutEmploymentFields_ReturnsNulls()
  {
    // Arrange: seeded directly, the way a pre-migration row looks
    var employee = new Employee
    {
      FirstName = "Legacy",
      LastName = "Employee",
      Gender = "Male",
      MaritalStatus = "Single",
      EmployeeCode = "EMP-OLD-001",
      Username = "legacy",
      PasswordHash = "irrelevant-to-this-test",
    };
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetByIdAsync(employee.Id, includePrivateDetails: false);

    // Assert
    Assert.NotNull(result);
    Assert.Null(result.EmployeeType);
    Assert.Null(result.EmploymentStatus);
    Assert.Null(result.JoiningDate);
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task GetByIdAsync_EmployeeWithContact_IncludesContactsOnlyWhenAsked(bool include)
  {
    // Arrange
    var created = await _service.CreateAsync(new EmployeeCreateDto
    {
      FirstName = "Maria",
      LastName = "Reyes",
      Gender = "Female",
      MaritalStatus = "Married",
      Username = "mreyes",
      Password = "TempPass2026!",
      EmergencyContact = NewContactDto(),
    }, 1);

    // Act
    var result = await _service.GetByIdAsync(created.Id, includePrivateDetails: include);

    // Assert
    Assert.NotNull(result);
    if (include)
    {
      var contact = Assert.Single(result.EmergencyContacts!);
      Assert.Equal("Jose", contact.FirstName);
    }
    else
    {
      Assert.Null(result.EmergencyContacts);
    }
  }

  [Fact]
  public async Task GetByIdAsync_ReturnsGenderAndBirthdayFlags()
  {
    var employee = NewEmployee("Juan", "Cruz", "EMP-001");
    employee.ShowBirthdayInProfile = true;
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    var result = await _service.GetByIdAsync(employee.Id, includePrivateDetails: true);

    Assert.False(result!.ShowGenderInProfile);
    Assert.True(result.ShowBirthdayInProfile);
  }
}