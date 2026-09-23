using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using MSOPtions = Microsoft.Extensions.Options.Options;
using Moq;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;
using MyMIS.Api.Options;
using MyMIS.Api.Services;

namespace MyMIS.Api.Tests.Services;

public class EmployeeServiceTests : IDisposable
{
  private readonly AppDbContext _context;
  private readonly EmployeeService _service;

  public EmployeeServiceTests()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;


    _context = new AppDbContext(options);

    var hobbyService = new HobbyService(_context);

    var s3Options = MSOPtions.Create(new S3Options
    {
      AccessKey = "test-access-key",
      SecretKey = "test-secret-key",
      Region = "ap-southeast-1",
      BucketName = "test-bucket"
    });
    var mockS3Client = new Mock<IAmazonS3>();
    mockS3Client
        .Setup(client => client.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
        .Returns("https://fake-presigned-url.test/johndoe.jpg");
    var s3UploadService = new S3UploadService(mockS3Client.Object, s3Options);

    _service = new EmployeeService(_context, hobbyService, s3UploadService);
  }

  public void Dispose()
  {
    _context.Dispose();
    GC.SuppressFinalize(this);
  }

  [Fact]
  public async Task GetAllAsync_EmployeeWithDepartmentAndPosition_PopulatesAllSummaryFields()
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
      MiddleName = "Conor",
      LastName = "Doe",
      Suffix = "Sr.",
      Gender = "MALE",
      MaritalStatus = "SINGLE",
      EmployeeCode = "EMP-001",
      Username = "johndoe",
      PasswordHash = "irrelevant-to-this-test",
      AvatarUrl = "johndoe.jpg",
      DepartmentId = department.Id,
      PositionId = position.Id
    };
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetAllAsync();

    // Assert
    Assert.Single(result);
    Assert.Equal("John", result[0].FirstName);
    Assert.Equal("Conor", result[0].MiddleName);
    Assert.Equal("Doe", result[0].LastName);
    Assert.Equal("Sr.", result[0].Suffix);
    Assert.Equal("EMP-001", result[0].EmployeeCode);
    Assert.Equal("https://fake-presigned-url.test/johndoe.jpg", result[0].AvatarUrl);
    Assert.Equal(department.Name, result[0].DepartmentName);
    Assert.Equal(position.Title, result[0].PositionTitle);
  }

  [Fact]
  public async Task GetAllAsync_EmployeeWithNoDepartmentOrPosition_ReturnsNullNamesWithoutThrowing()
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
    var result = await _service.GetAllAsync();

    // Assert
    Assert.Single(result);
    Assert.Null(result[0].DepartmentName);
    Assert.Null(result[0].PositionTitle);
  }

  [Fact]
  public async Task GetByIdAsync_NonExistentId_ReturnsNull()
  {
    // Act
    var result = await _service.GetByIdAsync(999);

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
    var result = await _service.GetByIdAsync(employee.Id);

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
    var result = await _service.GetByIdAsync(employee.Id);

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
    var result = await _service.GetByIdAsync(employee.Id);

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
    var result = await _service.GetByIdAsync(employee.Id);

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
    var result = await _service.GetByIdAsync(employee.Id);

    // Assert
    Assert.NotNull(result);
    Assert.NotNull(result.Hobbies);
    Assert.Empty(result.Hobbies);
  }

  [Fact]
  public async Task CreateAsync_HashesPassword_NeverStoresPlainText()
  {
    // Arrange
    var dto = new EmployeeCreateDto
    {
      FirstName = "Jane",
      LastName = "Doe",
      Gender = "Female",
      MaritalStatus = "Single",
      EmployeeCode = "EMP001",
      Username = "jdoe",
      Password = "SuperSecret123"
    };

    // Act
    await _service.CreateAsync(dto);

    // Assert
    var savedEmployee = await _context.Employees.FirstAsync(e => e.Username == "jdoe");

    Assert.NotEqual(dto.Password, savedEmployee.PasswordHash);
    Assert.True(BCrypt.Net.BCrypt.Verify(dto.Password, savedEmployee.PasswordHash));
  }

  [Fact]
  public async Task CreateAsync_PositionIdProvided_PersistsAndReturnsNestedPosition()
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
      SortOrder = 1,
      IsActive = true,
      IsApprover = true,
      DepartmentId = department.Id,
    };
    _context.Positions.Add(position);
    await _context.SaveChangesAsync();

    var dto = new EmployeeCreateDto
    {
      FirstName = "John",
      MiddleName = "Conor",
      LastName = "Doe",
      Gender = "MALE",
      MaritalStatus = "SINGLE",
      EmployeeCode = "EMP-001",
      Username = "johndoe",
      Password = "irrelevant-to-this-test",
      DepartmentId = department.Id,
      PositionId = position.Id
    };

    // Act
    var result = await _service.CreateAsync(dto);

    // Assert
    Assert.NotNull(result.Position);
    Assert.Equal(position.Id, result.Position.Id);
    Assert.Equal("Manager", result.Position.Title);

    var savedEmployee = await _context.Employees.FirstAsync(e => e.Id == result.Id);
    Assert.Equal(position.Id, savedEmployee.PositionId);
  }

  [Fact]
  public async Task CreateAsync_PositionIdOmitted_CreatesEmployeeWithNullPosition()
  {
    // Arrange
    var dto = new EmployeeCreateDto
    {
      FirstName = "John",
      MiddleName = "Conor",
      LastName = "Doe",
      Gender = "MALE",
      MaritalStatus = "SINGLE",
      EmployeeCode = "EMP-001",
      Username = "johndoe",
      Password = "irrelevant-to-this-test",
    };

    // Act
    var result = await _service.CreateAsync(dto);

    // Assert
    Assert.Null(result.Position);
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
      EmployeeCode = "EMP003",
      Username = "awong",
      Password = "OriginalPass1"
    };
    var created = await _service.CreateAsync(createDto);

    var originalHash = (await _context.Employees.FirstAsync(e => e.Id == created.Id)).PasswordHash;

    var updateDto = new EmployeeUpdateDto
    {
      FirstName = "Alice",
      LastName = "Wong-Updated",
      Gender = "Female",
      MaritalStatus = "Married",
      EmployeeCode = "EMP003",
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
      EmployeeCode = "EMP004",
      Username = "blee",
      Password = "OldPassword1"
    };
    var created = await _service.CreateAsync(createDto);
    var originalHash = (await _context.Employees.FirstAsync(e => e.Id == created.Id)).PasswordHash;

    var updateDto = new EmployeeUpdateDto
    {
      FirstName = "Bob",
      LastName = "Lee",
      Gender = "Male",
      MaritalStatus = "Single",
      EmployeeCode = "EMP004",
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
      EmployeeCode = "EMP002",
      Username = "jsmith",
      Password = "irrelevant-for-this-test",
    };
    var created = await _service.CreateAsync(createDto);

    var employeeUpdateDto = new EmployeeUpdateDto
    {
      FirstName = "John",
      LastName = "Smith",
      Gender = "Male",
      MaritalStatus = "Married",
      EmployeeCode = "EMP002",
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
      EmployeeCode = "EMP002",
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
      EmployeeCode = "EMP002",
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
      EmployeeCode = "EMP002",
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
      EmployeeCode = "EMP-001",
      Username = "jsmith",
      Password = "irrelevant-to-this-test"
    };

    var createdResult = await _service.CreateAsync(createDto);

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
      EmployeeCode = "EMP-001",
      Username = "sofialeighvargas",
      Password = "irrelevant-to-this-test",
      OfficeLocation = "Manila",
      WorkSchedule = "Morning, 8AM - 5PM",
      PersonalDetail = new EmployeePersonalDetailCreateDto
      {
        Nickname = "Lei"
      }
    };
    var createResult = await _service.CreateAsync(createDto);
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
      EmployeeCode = "EMP-001",
      Username = "johnsmith",
      Password = "irrelevant-for-this-test",
      OfficeLocation = "Pasig",
      WorkSchedule = "Morning, 8AM - 5PM",
    };
    var createResult = await _service.CreateAsync(createDto);
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
      EmployeeCode = "EMP005",
      Username = "creyes",
      Password = "SomePassword1"
    };
    var created = await _service.CreateAsync(createDto);

    // Act
    var deleteResult = await _service.DeleteAsync(created.Id);

    // Assert
    Assert.True(deleteResult);

    // Normal query respects the global filter — should NOT find it
    var viaService = await _service.GetByIdAsync(created.Id);
    Assert.Null(viaService);

    // Bypassing the filter proves the row still physically exists
    var rawRow = await _context.Employees
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(e => e.Id == created.Id);

    Assert.NotNull(rawRow);
    Assert.NotNull(rawRow.DeletedAt);
  }

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

