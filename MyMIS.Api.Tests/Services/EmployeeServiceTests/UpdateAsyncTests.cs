using Microsoft.EntityFrameworkCore;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;

namespace MyMIS.Api.Tests.Services.EmployeeServiceTests;

public class UpdateAsyncTests : EmployeeServiceTestBase
{
  [Fact]
  public async Task UpdateAsync_UsernameWithUppercaseAndWhitespace_StoresNormalized()
  {
    // Arrange
    var created = await _service.CreateAsync(new EmployeeCreateDto
    {
      FirstName = "Jane",
      LastName = "Doe",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "jdoe",
      Password = "irrelevant-to-this-test",
    }, 1);

    var updateDto = new EmployeeUpdateDto
    {
      FirstName = "Jane",
      LastName = "Doe",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "  NewName  ", // <- the behavior under test
    };

    // Act
    await _service.UpdateAsync(created.Id, updateDto);

    // Assert
    var saved = await _context.Employees
      .AsNoTracking()
      .FirstAsync(e => e.Id == created.Id);
    Assert.Equal("newname", saved.Username);
  }

  [Fact]
  public async Task UpdateAsync_PasswordOmitted_KeepsOriginalPasswordHash()
  {
    // Arrange
    var createDto = new EmployeeCreateDto
    {
      FirstName = "Alice",
      LastName = "Wong",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "awong",
      Password = "OriginalPass1"
    };
    var created = await _service.CreateAsync(createDto, 1);

    var originalHash = (await _context.Employees.FirstAsync(e => e.Id == created.Id)).PasswordHash;

    var updateDto = new EmployeeUpdateDto
    {
      FirstName = "Alice",
      LastName = "Wong-Updated",
      Gender = "Female",
      MaritalStatus = "Married",
      Username = "awong",
      Password = null // <- the behavior under test
    };

    // Act
    await _service.UpdateAsync(created.Id, updateDto);

    // Assert
    var updated = await _context.Employees.FirstAsync(e => e.Id == created.Id);
    Assert.Equal(originalHash, updated.PasswordHash);
    Assert.Equal("Wong-Updated", updated.LastName);
  }

  [Fact]
  public async Task UpdateAsync_PasswordProvided_UpdatesPasswordHash()
  {
    // Arrange
    var createDto = new EmployeeCreateDto
    {
      FirstName = "Bob",
      LastName = "Lee",
      Gender = "Male",
      MaritalStatus = "Single",
      Username = "blee",
      Password = "OldPassword1"
    };
    var created = await _service.CreateAsync(createDto, 1);
    var originalHash = (await _context.Employees.FirstAsync(e => e.Id == created.Id)).PasswordHash;

    var updateDto = new EmployeeUpdateDto
    {
      FirstName = "Bob",
      LastName = "Lee",
      Gender = "Male",
      MaritalStatus = "Single",
      Username = "blee",
      Password = "NewPassword2"
    };

    // Act
    await _service.UpdateAsync(created.Id, updateDto);

    // Assert
    var updated = await _context.Employees.FirstAsync(e => e.Id == created.Id);
    Assert.NotEqual(originalHash, updated.PasswordHash);
    Assert.True(BCrypt.Net.BCrypt.Verify("NewPassword2", updated.PasswordHash));
  }

  [Fact]
  public async Task UpdateAsync_PersonalDetailProvided_PersistsAllSixFields()
  {
    // Arrange
    var createDto = new EmployeeCreateDto
    {
      FirstName = "John",
      LastName = "Smith",
      Gender = "Male",
      MaritalStatus = "Married",
      Username = "jsmith",
      Password = "irrelevant-for-this-test",
    };
    var created = await _service.CreateAsync(createDto, 1);

    var employeeUpdateDto = new EmployeeUpdateDto
    {
      FirstName = "John",
      LastName = "Smith",
      Gender = "Male",
      MaritalStatus = "Married",
      Username = "jsmith",
      PersonalDetail = new EmployeePersonalDetailUpdateDto
      {
        Nickname = "JSmith",
        Birthplace = "Texas",
        Nationality = "American",
        BloodType = "AB+",
        Religion = "Baptist",
        Bio = "This is a sample bio...",
      },
    };

    // Act
    var result = await _service.UpdateAsync(created.Id, employeeUpdateDto);

    // Assert
    Assert.NotNull(result);
    Assert.NotNull(result.PersonalDetail);
    Assert.Equal("JSmith", result.PersonalDetail.Nickname);
    Assert.Equal("Texas", result.PersonalDetail.Birthplace);
    Assert.Equal("American", result.PersonalDetail.Nationality);
    Assert.Equal("AB+", result.PersonalDetail.BloodType);
    Assert.Equal("Baptist", result.PersonalDetail.Religion);
    Assert.Equal("This is a sample bio...", result.PersonalDetail.Bio);
  }

  [Fact]
  public async Task UpdateAsync_EmployeeHasNoExistingPersonalDetailRow_CreatesOneWithoutThrowing()
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

    var employeeUpdateDto = new EmployeeUpdateDto
    {
      FirstName = "John",
      LastName = "Smith",
      Gender = "Male",
      MaritalStatus = "Married",
      Username = "jsmith",
      PersonalDetail = new EmployeePersonalDetailUpdateDto
      {
        Nickname = "John",
        Birthplace = "Malibu",
        Nationality = "America",
        BloodType = "B+",
        Religion = "Catholic",
        Bio = "This is a sample bio..."
      }
    };

    // Act
    var result = await _service.UpdateAsync(employee.Id, employeeUpdateDto);

    // Assert
    Assert.NotNull(result);
    Assert.NotNull(result.PersonalDetail);
    Assert.Equal("John", result.PersonalDetail.Nickname);
    Assert.Equal("Malibu", result.PersonalDetail.Birthplace);
    Assert.Equal("America", result.PersonalDetail.Nationality);
    Assert.Equal("B+", result.PersonalDetail.BloodType);
    Assert.Equal("Catholic", result.PersonalDetail.Religion);
    Assert.Equal("This is a sample bio...", result.PersonalDetail.Bio);

    var savedPersonalDetail = await _context.EmployeePersonalDetails
        .FirstOrDefaultAsync(pd => pd.EmployeeId == employee.Id);

    Assert.NotNull(savedPersonalDetail);
  }

  [Fact]
  public async Task UpdateAsync_PersonalDetailFieldOmitted_ClearsThatFieldToNull()
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
      PersonalDetail = new EmployeePersonalDetail
      {
        Nickname = "John",
        Bio = "My name is John."
      }
    };
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    var updateDto = new EmployeeUpdateDto
    {
      FirstName = "John",
      LastName = "Smith",
      Gender = "Male",
      MaritalStatus = "Married",
      Username = "jsmith",
      PersonalDetail = new EmployeePersonalDetailUpdateDto
      {
        Nickname = "JSmith",
      }
    };

    // Act
    var result = await _service.UpdateAsync(employee.Id, updateDto);

    // Assert
    Assert.NotNull(result);
    Assert.NotNull(result.PersonalDetail);
    Assert.Equal("JSmith", result.PersonalDetail.Nickname);
    Assert.Null(result.PersonalDetail.Bio);
  }

  [Fact]
  public async Task UpdateAsync_PersonalDetailOmittedEntirely_ClearsAllPersonalDetailFields()
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
      PersonalDetail = new EmployeePersonalDetail
      {
        Nickname = "John",
        Birthplace = "California",
        Nationality = "American",
        BloodType = "O+",
        Religion = "Christian",
        Bio = "My name is John."
      }
    };
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    var updateDto = new EmployeeUpdateDto
    {
      FirstName = "John",
      LastName = "Smith",
      Gender = "Male",
      MaritalStatus = "Married",
      Username = "jsmith",
    };

    // Act
    var result = await _service.UpdateAsync(employee.Id, updateDto);

    // Assert
    Assert.NotNull(result);
    Assert.NotNull(result.PersonalDetail);
    Assert.Null(result.PersonalDetail.Nickname);
    Assert.Null(result.PersonalDetail.Birthplace);
    Assert.Null(result.PersonalDetail.Nationality);
    Assert.Null(result.PersonalDetail.BloodType);
    Assert.Null(result.PersonalDetail.Religion);
    Assert.Null(result.PersonalDetail.Bio);
  }

  [Fact]
  public async Task UpdateAsync_KeepsUploadedAvatarKeys()
  {
    // Arrange: an employee with an uploaded photo and its thumbnail
    var employee = NewEmployee("Maria", "Santos", "EMP-001");
    employee.AvatarUrl = "avatars/1.jpg";
    employee.AvatarThumbnailUrl = "avatars-thumbnails/1.jpg";
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    var updateDto = new EmployeeUpdateDto
    {
      FirstName = "Maria",
      LastName = "Santos-Reyes",
      Gender = "MALE",
      MaritalStatus = "SINGLE",
      Username = "emp-001",
    };

    // Act
    await _service.UpdateAsync(employee.Id, updateDto);

    // Assert: the edit applied, and the avatar keys survived it
    var saved = await _context.Employees
      .AsNoTracking()
      .FirstAsync(e => e.Id == employee.Id);
    Assert.Equal("Santos-Reyes", saved.LastName);
    Assert.Equal("avatars/1.jpg", saved.AvatarUrl);
    Assert.Equal("avatars-thumbnails/1.jpg", saved.AvatarThumbnailUrl);
  }

  [Fact]
  public async Task UpdateAsync_EmploymentStatusAndJoiningDateProvided_UpdatesThem()
  {
    // Arrange
    var created = await _service.CreateAsync(new EmployeeCreateDto
    {
      FirstName = "Maria",
      LastName = "Reyes",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "mreyes",
      Password = "irrelevant-to-this-test",
      EmployeeType = EmployeeType.Client,
      EmploymentStatus = EmploymentStatus.Probationary,
      JoiningDate = new DateOnly(2026, 10, 5),
    }, 1);

    var updateDto = new EmployeeUpdateDto
    {
      FirstName = "Maria",
      LastName = "Reyes",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "mreyes",
      EmploymentStatus = EmploymentStatus.Regular,   // <- changed
      JoiningDate = new DateOnly(2026, 11, 1),       // <- changed
    };

    // Act
    await _service.UpdateAsync(created.Id, updateDto);

    // Assert
    var saved = await _context.Employees
      .AsNoTracking()
      .FirstAsync(e => e.Id == created.Id);
    Assert.Equal(EmploymentStatus.Regular, saved.EmploymentStatus);
    Assert.Equal(new DateOnly(2026, 11, 1), saved.JoiningDate);
  }

  [Fact]
  public async Task UpdateAsync_ExistingEmployeeType_LeavesItUnchanged()
  {
    // Arrange
    var created = await _service.CreateAsync(new EmployeeCreateDto
    {
      FirstName = "Maria",
      LastName = "Reyes",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "mreyes",
      Password = "irrelevant-to-this-test",
      EmployeeType = EmployeeType.Client,
      EmploymentStatus = EmploymentStatus.Probationary,
      JoiningDate = new DateOnly(2026, 10, 5),
    }, 1);

    var updateDto = new EmployeeUpdateDto
    {
      FirstName = "Maria",
      LastName = "Reyes-Santos",   // an unrelated change, so the update really does something
      Gender = "Female",
      MaritalStatus = "Married",
      Username = "mreyes",
      EmploymentStatus = EmploymentStatus.Regular,
      JoiningDate = new DateOnly(2026, 10, 5),
    };

    // Act
    await _service.UpdateAsync(created.Id, updateDto);

    // Assert: type is fixed for the life of the record
    var saved = await _context.Employees
      .AsNoTracking()
      .FirstAsync(e => e.Id == created.Id);
    Assert.Equal(EmployeeType.Client, saved.EmployeeType);
    Assert.Equal("Reyes-Santos", saved.LastName);
  }

  [Fact]
  public async Task UpdateAsync_PasswordProvided_SetsMustChangePassword()
  {
    // Arrange: an employee who already changed their password
    var employee = NewEmployee("Juan", "Cruz", "EMP-001");
    employee.MustChangePassword = false;
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    var updateDto = new EmployeeUpdateDto
    {
      FirstName = "Juan",
      LastName = "Cruz",
      Gender = "MALE",
      MaritalStatus = "SINGLE",
      Username = employee.Username,
      Password = "ResetByHr2026!",   // <- HR resets the password
    };

    // Act
    await _service.UpdateAsync(employee.Id, updateDto);

    // Assert
    var saved = await _context.Employees.AsNoTracking().FirstAsync(e => e.Id == employee.Id);
    Assert.True(saved.MustChangePassword);
  }

  [Fact]
  public async Task UpdateAsync_PasswordOmitted_LeavesMustChangePasswordUnchanged()
  {
    // Arrange: an employee who already changed their password
    var employee = NewEmployee("Juan", "Cruz", "EMP-001");
    employee.MustChangePassword = false;
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    var updateDto = new EmployeeUpdateDto
    {
      FirstName = "Juan",
      LastName = "Dela Cruz",   // an unrelated change
      Gender = "MALE",
      MaritalStatus = "SINGLE",
      Username = employee.Username,
      // Password omitted
    };

    // Act
    await _service.UpdateAsync(employee.Id, updateDto);

    // Assert
    var saved = await _context.Employees.AsNoTracking().FirstAsync(e => e.Id == employee.Id);
    Assert.False(saved.MustChangePassword);
    Assert.Equal("Dela Cruz", saved.LastName);   // proves the update actually ran
  }

  [Fact]
  public async Task UpdateSelfAsync_NoPersonalDetailRowExists_CreatesOneWithoutThrowing()
  {
    // Arrange
    var employee = new Employee
    {
      FirstName = "John",
      LastName = "Smith",
      Gender = "MALE",
      MaritalStatus = "SINGLE",
      EmployeeCode = "EMP-2026-001",
      Username = "jsmith",
      PasswordHash = "irrelevant-for-this-test"
    };
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    var selfUpdateDto = new EmployeeSelfUpdateDto
    {
      Nickname = "Johny",
      Birthplace = "California",
      Nationality = "American",
      BloodType = "AB+",
      Religion = "Catholic",
      Bio = "My name is John Smith..."
    };

    // Act
    var result = await _service.UpdateSelfAsync(employee.Id, selfUpdateDto);

    // Assert
    Assert.NotNull(result);
    Assert.NotNull(result.PersonalDetail);

    Assert.Equal("Johny", result.PersonalDetail.Nickname);
    Assert.Equal("California", result.PersonalDetail.Birthplace);
    Assert.Equal("American", result.PersonalDetail.Nationality);
    Assert.Equal("AB+", result.PersonalDetail.BloodType);
    Assert.Equal("Catholic", result.PersonalDetail.Religion);
    Assert.Equal("My name is John Smith...", result.PersonalDetail.Bio);

    var savedPersonalDetail = await _context.EmployeePersonalDetails
        .FirstOrDefaultAsync(pd => pd.EmployeeId == employee.Id);

    Assert.NotNull(savedPersonalDetail);
  }

  [Fact]
  public async Task UpdateSelfAsync_NonExistentEmployeeId_ReturnsNull()
  {
    // Arrange
    var selfUpdateDto = new EmployeeSelfUpdateDto
    {
      Nickname = "Johny",
      Birthplace = "Manila",
      Nationality = "Filipino",
      BloodType = "A+",
      Religion = "Baptist",
      Bio = "This is johny..."
    };

    // Act
    var result = await _service.UpdateSelfAsync(999, selfUpdateDto);

    // Assert
    Assert.Null(result);

    var savedPersonalDetailsCount = await _context.EmployeePersonalDetails.CountAsync();

    Assert.Equal(0, savedPersonalDetailsCount);
  }

  [Fact]
  public async Task UpdateSelfAsync_ValidRequest_PersistsAllSixFields()
  {
    // Arrange
    var createDto = new EmployeeCreateDto
    {
      FirstName = "John",
      LastName = "Smith",
      Gender = "MALE",
      MaritalStatus = "SINGLE",
      Username = "jsmith",
      Password = "irrelevant-to-this-test"
    };

    var createdResult = await _service.CreateAsync(createDto, 1);

    var selfUpdateDto = new EmployeeSelfUpdateDto
    {
      Nickname = "Johny",
      Birthplace = "Manila",
      Nationality = "Filipino",
      BloodType = "A+",
      Religion = "Baptist",
      Bio = "This is johny..."
    };

    // Act
    var selfUpdateResult = await _service.UpdateSelfAsync(createdResult.Id, selfUpdateDto);

    // Assert
    Assert.NotNull(selfUpdateResult);
    Assert.NotNull(selfUpdateResult.PersonalDetail);
    Assert.Equal("Johny", selfUpdateResult.PersonalDetail.Nickname);
    Assert.Equal("Manila", selfUpdateResult.PersonalDetail.Birthplace);
    Assert.Equal("Filipino", selfUpdateResult.PersonalDetail.Nationality);
    Assert.Equal("A+", selfUpdateResult.PersonalDetail.BloodType);
    Assert.Equal("Baptist", selfUpdateResult.PersonalDetail.Religion);
    Assert.Equal("This is johny...", selfUpdateResult.PersonalDetail.Bio);
  }

  [Fact]
  public async Task UpdateSelfAsync_FieldOmitted_ClearsThatFieldToNull()
  {
    // Arrange
    var employee = new Employee
    {
      FirstName = "Jazmine Ciel",
      LastName = "Nares",
      Gender = "FEMALE",
      MaritalStatus = "SINGLE",
      EmployeeCode = "EMP-001",
      Username = "jazminecielnares",
      PasswordHash = "irrelevant-for-this-test",
      PersonalDetail = new EmployeePersonalDetail
      {
        Nickname = "Yel",
        Bio = "Hello! My name is Jazmine Ciel..."
      }
    };
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    var selfUpdateDto = new EmployeeSelfUpdateDto
    {
      Nickname = "Buyengyeng"
    };

    // Act
    var result = await _service.UpdateSelfAsync(employee.Id, selfUpdateDto);

    // Assert
    Assert.NotNull(result);
    Assert.NotNull(result.PersonalDetail);
    Assert.Equal("Buyengyeng", result.PersonalDetail.Nickname);
    Assert.Null(result.PersonalDetail.Bio);
  }

  [Fact]
  public async Task UpdatePartialAsync_ValidRequest_UpdatesOfficeLocationAndWorkSchedule()
  {
    // Arrange
    var createDto = new EmployeeCreateDto
    {
      FirstName = "Sofia Leigh",
      LastName = "Vargas",
      Gender = "FEMALE",
      MaritalStatus = "SINGLE",
      Username = "sofialeighvargas",
      Password = "irrelevant-to-this-test",
      OfficeLocation = "Manila",
      WorkSchedule = "Morning, 8AM - 5PM",
      PersonalDetail = new EmployeePersonalDetailCreateDto
      {
        Nickname = "Lei"
      }
    };
    var createResult = await _service.CreateAsync(createDto, 1);
    var partialUpdateDto = new EmployeePartialUpdateDto
    {
      OfficeLocation = "Pasig",
      WorkSchedule = "Mid, 11AM - 9PM"
    };

    // Act
    var partialUpdateResult = await _service.UpdatePartialAsync(createResult.Id, partialUpdateDto);

    // Assert
    Assert.NotNull(partialUpdateResult);

    Assert.Equal("Pasig", partialUpdateResult.OfficeLocation);
    Assert.Equal("Mid, 11AM - 9PM", partialUpdateResult.WorkSchedule);

    Assert.NotNull(partialUpdateResult.PersonalDetail);
    Assert.Equal("Lei", partialUpdateResult.PersonalDetail.Nickname);
  }

  [Fact]
  public async Task UpdatePartialAsync_NonExistentId_ReturnsNull()
  {
    // Arrange
    var updateDto = new EmployeePartialUpdateDto
    {
      OfficeLocation = "Manila",
      WorkSchedule = "Mid, 11AM - 9PM"
    };

    // Act
    var result = await _service.UpdatePartialAsync(999, updateDto);

    // Assert
    Assert.Null(result);
    var savedEmployeeCount = await _context.Employees.CountAsync();
    Assert.Equal(0, savedEmployeeCount);
  }

  [Fact]
  public async Task UpdatePartialAsync_WorkScheduleOmitted_ClearsItToNull()
  {
    // Arrange
    var createDto = new EmployeeCreateDto
    {
      FirstName = "John",
      LastName = "Smith",
      Gender = "MALE",
      MaritalStatus = "SINGLE",
      Username = "johnsmith",
      Password = "irrelevant-for-this-test",
      OfficeLocation = "Pasig",
      WorkSchedule = "Morning, 8AM - 5PM",
    };
    var createResult = await _service.CreateAsync(createDto, 1);
    var updateDto = new EmployeePartialUpdateDto
    {
      OfficeLocation = "Manila"
    };

    // Act
    var updateResult = await _service.UpdatePartialAsync(createResult.Id, updateDto);

    // Assert
    Assert.NotNull(updateResult);
    Assert.Equal("Manila", updateResult.OfficeLocation);
    Assert.Null(updateResult.WorkSchedule);
  }

  private async Task<Employee> SeedEmployeeWithFlagsAsync(bool showGender = false, bool showBirthday = false)
  {
    var employee = NewEmployee("Juan", "Cruz", "EMP-001");
    employee.ShowGenderInProfile = showGender;
    employee.ShowBirthdayInProfile = showBirthday;

    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();
    return employee;
  }

  private Task<Employee> SavedEmployeeAsync(int employeeId) =>
    _context.Employees.AsNoTracking().SingleAsync(e => e.Id == employeeId);

  [Fact]
  public async Task UpdateSelfAsync_GenderAndBirthdayFlagsOmitted_KeepsTheCurrentValues()
  {
    var employee = await SeedEmployeeWithFlagsAsync(showGender: true, showBirthday: true);

    await _service.UpdateSelfAsync(employee.Id, new EmployeeSelfUpdateDto { Nickname = "Jun" });

    var saved = await SavedEmployeeAsync(employee.Id);
    Assert.True(saved.ShowGenderInProfile);
    Assert.True(saved.ShowBirthdayInProfile);
  }

  [Fact]
  public async Task UpdateSelfAsync_GenderAndBirthdayFlagsSent_SavesThem()
  {
    var employee = await SeedEmployeeWithFlagsAsync();

    var result = await _service.UpdateSelfAsync(employee.Id, new EmployeeSelfUpdateDto
    {
      ShowGenderInProfile = true,
      ShowBirthdayInProfile = true,
    });

    Assert.True(result!.ShowGenderInProfile);
    Assert.True(result.ShowBirthdayInProfile);

    var saved = await SavedEmployeeAsync(employee.Id);
    Assert.True(saved.ShowGenderInProfile);
    Assert.True(saved.ShowBirthdayInProfile);
  }

  /// <summary>HR can hide what the employee chose to show (decision S2).</summary>
  [Fact]
  public async Task UpdateAsync_HrSendsShowGenderFalse_HidesGender()
  {
    var employee = await SeedEmployeeWithFlagsAsync(showGender: true);

    await _service.UpdateAsync(employee.Id, new EmployeeUpdateDto
    {
      FirstName = "Juan",
      LastName = "Cruz",
      Gender = "MALE",
      MaritalStatus = "SINGLE",
      Username = "emp-001",
      ShowGenderInProfile = false,
    });

    Assert.False((await SavedEmployeeAsync(employee.Id)).ShowGenderInProfile);
  }
}