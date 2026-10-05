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
        .Returns((GetPreSignedUrlRequest request) => $"https://fake-presigned-url.test/{request.Key}");
    var s3UploadService = new S3UploadService(mockS3Client.Object, s3Options);
    var codeCounter = 0;
    var mockCodeGenerator = new Mock<IEmployeeCodeGenerator>();
    mockCodeGenerator
        .Setup(generator => generator.GenerateAsync())
        .ReturnsAsync(() => $"MYMIS-TEST-{++codeCounter:D5}");

    _service = new EmployeeService(_context, hobbyService, s3UploadService, mockCodeGenerator.Object);
  }

  public void Dispose()
  {
    _context.Dispose();
    GC.SuppressFinalize(this);
  }

  // Lookup tests seed several employees at once; only name and code matter to them.
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

  // Five employees, inserted out of order.
  // Sorted by last name: Bautista, Cruz, Garcia, Reyes, Santos.
  private async Task SeedFiveEmployeesAsync()
  {
    _context.Employees.AddRange(
        NewEmployee("Maria", "Santos", "EMP-005"),
        NewEmployee("Ana", "Cruz", "EMP-002"),
        NewEmployee("Dina", "Garcia", "EMP-003"),
        NewEmployee("Carlo", "Bautista", "EMP-001"),
        NewEmployee("Ben", "Reyes", "EMP-004"));
    await _context.SaveChangesAsync();
  }

  [Fact]
  public async Task GetPagedAsync_EmployeeWithDepartmentAndPosition_PopulatesAllSummaryFields()
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
      AvatarStyle = AvatarStyle.Bottts,
      DepartmentId = department.Id,
      PositionId = position.Id
    };
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetPagedAsync(null, 1, 20);

    // Assert
    var item = Assert.Single(result.Data);
    Assert.Equal("John", item.FirstName);
    Assert.Equal("Conor", item.MiddleName);
    Assert.Equal("Doe", item.LastName);
    Assert.Equal("Sr.", item.Suffix);
    Assert.Equal("EMP-001", item.EmployeeCode);
    Assert.Equal("https://fake-presigned-url.test/johndoe.jpg", item.AvatarUrl);
    Assert.Equal(AvatarStyle.Bottts, item.AvatarStyle);
    Assert.Equal(department.Name, item.DepartmentName);
    Assert.Equal(position.Title, item.PositionTitle);
  }

  [Fact]
  public async Task GetPagedAsync_EmployeeWithNoDepartmentOrPosition_ReturnsNullNamesWithoutThrowing()
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
    var result = await _service.GetPagedAsync(null, 1, 20);

    // Assert
    var item = Assert.Single(result.Data);
    Assert.Null(item.DepartmentName);
    Assert.Null(item.PositionTitle);
  }

  [Fact]
  public async Task GetPagedAsync_SecondPage_ReturnsNextSliceInSortOrder()
  {
    // Arrange
    await SeedFiveEmployeesAsync();

    // Act: page 2, 2 per page
    var result = await _service.GetPagedAsync(null, 2, 2);

    // Assert: skips Bautista and Cruz, returns Garcia and Reyes
    Assert.Equal(2, result.Data.Count);
    Assert.Equal("Garcia", result.Data[0].LastName);
    Assert.Equal("Reyes", result.Data[1].LastName);
    Assert.Equal(5, result.TotalCount);
    Assert.Equal(2, result.Page);
    Assert.Equal(2, result.PageSize);
  }

  [Fact]
  public async Task GetPagedAsync_LastPartialPage_ReturnsRemainingRows()
  {
    // Arrange
    await SeedFiveEmployeesAsync();

    // Act: 5 rows at 2 per page = 2 + 2 + 1, so page 3 has one row
    var result = await _service.GetPagedAsync(null, 3, 2);

    // Assert
    var item = Assert.Single(result.Data);
    Assert.Equal("Santos", item.LastName);
    Assert.Equal(5, result.TotalCount);
  }

  [Fact]
  public async Task GetPagedAsync_PagePastTheEnd_ReturnsEmptyItemsWithRealTotal()
  {
    // Arrange
    await SeedFiveEmployeesAsync();

    // Act
    var result = await _service.GetPagedAsync(null, 10, 2);

    // Assert: no rows on that page, but the total is still correct
    Assert.Empty(result.Data);
    Assert.Equal(5, result.TotalCount);
  }

  [Fact]
  public async Task GetPagedAsync_SoftDeletedEmployee_ExcludedFromItemsAndTotalCount()
  {
    // Arrange
    await SeedFiveEmployeesAsync();
    var santos = await _context.Employees.FirstAsync(e => e.LastName == "Santos");
    santos.DeletedAt = DateTime.UtcNow;
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetPagedAsync(null, 1, 20);

    // Assert: the count and the page agree
    Assert.Equal(4, result.TotalCount);
    Assert.Equal(4, result.Data.Count);
    Assert.DoesNotContain(result.Data, e => e.LastName == "Santos");
  }

  [Fact]
  public async Task GetPagedAsync_SearchTerm_FiltersItemsAndTotalCount()
  {
    // Arrange: "ar" matches Maria Santos, Dina Garcia, Carlo Bautista
    // (sorted: Bautista, Garcia, Santos), but not Ana Cruz or Ben Reyes.
    await SeedFiveEmployeesAsync();

    // Act: first page, 2 per page
    var result = await _service.GetPagedAsync("ar", 1, 2);

    // Assert: total counts only the 3 matches; the page holds the first 2
    Assert.Equal(3, result.TotalCount);
    Assert.Equal(2, result.Data.Count);
    Assert.Equal("Bautista", result.Data[0].LastName);
    Assert.Equal("Garcia", result.Data[1].LastName);
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
    var result = await _service.GetByIdAsync(employee.Id);

    // Assert
    Assert.NotNull(result);
    Assert.Null(result.EmployeeType);
    Assert.Null(result.EmploymentStatus);
    Assert.Null(result.JoiningDate);
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
      Username = "johndoe",
      Password = "irrelevant-to-this-test",
    };

    // Act
    var result = await _service.CreateAsync(dto);

    // Assert
    Assert.Null(result.Position);
  }

  [Fact]
  public async Task CreateAsync_AvatarStyleProvided_PersistsAndReturnsStyle()
  {
    // Arrange
    var dto = new EmployeeCreateDto
    {
      FirstName = "Jane",
      LastName = "Doe",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "jdoe",
      Password = "irrelevant-to-this-test",
      AvatarStyle = AvatarStyle.Constellation,
    };

    // Act
    var result = await _service.CreateAsync(dto);

    // Assert: returned in the response AND saved in the database
    Assert.Equal(AvatarStyle.Constellation, result.AvatarStyle);

    var saved = await _context.Employees
      .AsNoTracking()
      .FirstAsync(e => e.Id == result.Id);
    Assert.Equal(AvatarStyle.Constellation, saved.AvatarStyle);
  }

  [Fact]
  public async Task CreateAsync_AvatarStyleOmitted_StoresNull()
  {
    // Arrange
    var dto = new EmployeeCreateDto
    {
      FirstName = "Jane",
      LastName = "Doe",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "jdoe",
      Password = "irrelevant-to-this-test",
    };

    // Act
    var result = await _service.CreateAsync(dto);

    // Assert: not defaulted to the first enum member
    Assert.Null(result.AvatarStyle);
  }

  [Fact]
  public async Task CreateAsync_UsernameWithUppercaseAndWhitespace_StoresNormalized()
  {
    // Arrange
    var dto = new EmployeeCreateDto
    {
      FirstName = "Jane",
      LastName = "Doe",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "  JDoe  ",
      Password = "irrelevant-to-this-test",
    };

    // Act
    var result = await _service.CreateAsync(dto);

    // Assert: normalized in the response AND in the database
    Assert.Equal("jdoe", result.Username);

    var saved = await _context.Employees
      .AsNoTracking()
      .FirstAsync(e => e.Id == result.Id);
    Assert.Equal("jdoe", saved.Username);
  }

  [Fact]
  public async Task CreateAsync_EmploymentFieldsProvided_PersistsAndReturnsThem()
  {
    // Arrange
    var dto = new EmployeeCreateDto
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
    };

    // Act
    var result = await _service.CreateAsync(dto);

    // Assert: in the response...
    Assert.Equal(EmployeeType.Client, result.EmployeeType);
    Assert.Equal(EmploymentStatus.Probationary, result.EmploymentStatus);
    Assert.Equal(new DateOnly(2026, 10, 5), result.JoiningDate);

    // ...and in the database
    var saved = await _context.Employees
      .AsNoTracking()
      .FirstAsync(e => e.Id == result.Id);
    Assert.Equal(EmployeeType.Client, saved.EmployeeType);
    Assert.Equal(EmploymentStatus.Probationary, saved.EmploymentStatus);
    Assert.Equal(new DateOnly(2026, 10, 5), saved.JoiningDate);
  }

  [Fact]
  public async Task CreateAsync_NewEmployee_UsesGeneratedEmployeeCode()
  {
    // Arrange
    var dto = new EmployeeCreateDto
    {
      FirstName = "Maria",
      LastName = "Reyes",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "mreyes",
      Password = "irrelevant-to-this-test",
    };

    // Act
    var result = await _service.CreateAsync(dto);

    // Assert: the code comes from the generator, in the response and the database
    Assert.Equal("MYMIS-TEST-00001", result.EmployeeCode);

    var saved = await _context.Employees
      .AsNoTracking()
      .FirstAsync(e => e.Id == result.Id);
    Assert.Equal("MYMIS-TEST-00001", saved.EmployeeCode);
  }

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
    });

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
    var created = await _service.CreateAsync(createDto);

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
    var created = await _service.CreateAsync(createDto);
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
    var created = await _service.CreateAsync(createDto);

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
    });

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
    });

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

  [Fact]
  public async Task GetLookupAsync_NoSearch_ReturnsAllOrderedByLastNameThenFirstName()
  {
    // Arrange: inserted deliberately out of order
    _context.Employees.AddRange(
        NewEmployee("Maria", "Santos", "EMP-002"),
        NewEmployee("Ben", "Cruz", "EMP-003"),
        NewEmployee("Ana", "Cruz", "EMP-001"));
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetLookupAsync(null, 20);

    // Assert
    Assert.Equal(3, result.Count);
    Assert.Equal(("Ana", "Cruz"), (result[0].FirstName, result[0].LastName));
    Assert.Equal(("Ben", "Cruz"), (result[1].FirstName, result[1].LastName));
    Assert.Equal(("Maria", "Santos"), (result[2].FirstName, result[2].LastName));
  }

  [Theory]
  [InlineData("maria")]      // first name
  [InlineData("SANTOS")]     // last name, different case
  [InlineData("maria san")]  // across first + last name
  [InlineData("emp-002")]    // employee code, different case
  [InlineData("  Maria  ")]  // surrounding whitespace is trimmed
  public async Task GetLookupAsync_SearchTerm_MatchesCaseInsensitivelyAcrossNameAndCode(string search)
  {
    // Arrange
    var maria = NewEmployee("Maria", "Santos", "EMP-002");
    _context.Employees.AddRange(
        maria,
        NewEmployee("Ana", "Cruz", "EMP-001"),
        NewEmployee("Ben", "Reyes", "EMP-003"));
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetLookupAsync(search, 20);

    // Assert
    var match = Assert.Single(result);
    Assert.Equal(maria.Id, match.Id);
  }

  [Fact]
  public async Task GetLookupAsync_SearchWithNoMatch_ReturnsEmptyList()
  {
    // Arrange
    _context.Employees.Add(NewEmployee("Maria", "Santos", "EMP-002"));
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetLookupAsync("zzz", 20);

    // Assert
    Assert.NotNull(result);
    Assert.Empty(result);
  }

  [Fact]
  public async Task GetLookupAsync_WhitespaceOnlySearch_IsTreatedAsNoFilter()
  {
    // Arrange
    _context.Employees.AddRange(
        NewEmployee("Maria", "Santos", "EMP-002"),
        NewEmployee("Ana", "Cruz", "EMP-001"));
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetLookupAsync("   ", 20);

    // Assert
    Assert.Equal(2, result.Count);
  }

  [Fact]
  public async Task GetLookupAsync_MoreEmployeesThanLimit_ReturnsFirstNAlphabetically()
  {
    // Arrange
    _context.Employees.AddRange(
        NewEmployee("Maria", "Santos", "EMP-002"),
        NewEmployee("Ben", "Reyes", "EMP-003"),
        NewEmployee("Ana", "Cruz", "EMP-001"));
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetLookupAsync(null, 2);

    // Assert: Santos is cut, proving the sort happens before the limit
    Assert.Equal(2, result.Count);
    Assert.Equal("Cruz", result[0].LastName);
    Assert.Equal("Reyes", result[1].LastName);
  }

  [Theory]
  [InlineData("avatars/1.jpg", "avatars-thumbnails/1.jpg", "https://fake-presigned-url.test/avatars-thumbnails/1.jpg")]
  [InlineData("avatars/1.jpg", null, "https://fake-presigned-url.test/avatars/1.jpg")]
  [InlineData(null, null, null)]
  public async Task GetLookupAsync_AvatarKeys_PrefersThumbnailThenOriginal(
      string? avatarKey, string? thumbnailKey, string? expectedUrl)
  {
    // Arrange
    var employee = NewEmployee("Maria", "Santos", "EMP-002");
    employee.AvatarUrl = avatarKey;
    employee.AvatarThumbnailUrl = thumbnailKey;
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetLookupAsync(null, 20);

    // Assert
    Assert.Equal(expectedUrl, Assert.Single(result).AvatarUrl);
  }

  [Fact]
  public async Task GetLookupAsync_EmployeeWithPositionAndDepartment_PopulatesPositionTitleAndDepartmentId()
  {
    // Arrange
    var department = new Department { Name = "Human Resources", Slug = "HRD", Status = "Active" };
    _context.Departments.Add(department);
    await _context.SaveChangesAsync();

    var position = new Position
    {
      Title = "Manager",
      Slug = "MNGR",
      SortOrder = 1,
      IsActive = true,
      DepartmentId = department.Id,
    };
    _context.Positions.Add(position);
    await _context.SaveChangesAsync();

    var assigned = NewEmployee("Maria", "Santos", "EMP-002");
    assigned.PositionId = position.Id;
    assigned.DepartmentId = department.Id;
    assigned.AvatarStyle = AvatarStyle.Constellation;
    _context.Employees.AddRange(assigned, NewEmployee("Ana", "Cruz", "EMP-001"));
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetLookupAsync(null, 20);

    // Assert
    Assert.Null(result[0].PositionTitle);
    Assert.Null(result[0].DepartmentId);
    Assert.Null(result[0].AvatarStyle);
    Assert.Equal("Manager", result[1].PositionTitle);
    Assert.Equal(department.Id, result[1].DepartmentId);
    Assert.Equal(AvatarStyle.Constellation, result[1].AvatarStyle);
  }

  [Fact]
  public async Task UpdateAvatarStyleAsync_ExistingEmployee_SavesStyle()
  {
    // Arrange
    var employee = NewEmployee("Maria", "Santos", "EMP-001");
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    // Act
    var updated = await _service.UpdateAvatarStyleAsync(employee.Id, AvatarStyle.Constellation);

    // Assert: read back without tracking, so the value comes from the store
    Assert.True(updated);
    var saved = await _context.Employees
      .AsNoTracking()
      .FirstAsync(e => e.Id == employee.Id);
    Assert.Equal(AvatarStyle.Constellation, saved.AvatarStyle);
  }

  [Fact]
  public async Task UpdateAvatarStyleAsync_MissingEmployee_ReturnsFalse()
  {
    // Act
    var updated = await _service.UpdateAvatarStyleAsync(999, AvatarStyle.Bottts);

    // Assert
    Assert.False(updated);
  }

  [Fact]
  public async Task UpdateAvatarStyleAsync_SoftDeletedEmployee_ReturnsFalseAndLeavesStyleUnchanged()
  {
    // Arrange
    var employee = NewEmployee("Maria", "Santos", "EMP-001");
    employee.DeletedAt = DateTime.UtcNow;
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    // Act
    var updated = await _service.UpdateAvatarStyleAsync(employee.Id, AvatarStyle.Bottts);

    // Assert
    Assert.False(updated);
    var saved = await _context.Employees
      .IgnoreQueryFilters()
      .AsNoTracking()
      .FirstAsync(e => e.Id == employee.Id);
    Assert.Null(saved.AvatarStyle);
  }

  [Fact]
  public async Task IsUsernameAvailableAsync_UnusedUsername_ReturnsTrue()
  {
    // Arrange
    _context.Employees.Add(NewEmployee("Juan", "Cruz", "EMP-001"));   // username "emp-001"
    await _context.SaveChangesAsync();

    // Act
    var available = await _service.IsUsernameAvailableAsync("someoneelse");

    // Assert
    Assert.True(available);
  }

  [Theory]
  [InlineData("emp-001")]       // exact match
  [InlineData("  EMP-001  ")]   // same username after normalizing
  public async Task IsUsernameAvailableAsync_TakenUsername_ReturnsFalse(string input)
  {
    // Arrange
    _context.Employees.Add(NewEmployee("Juan", "Cruz", "EMP-001"));   // username "emp-001"
    await _context.SaveChangesAsync();

    // Act
    var available = await _service.IsUsernameAvailableAsync(input);

    // Assert
    Assert.False(available);
  }

  [Fact]
  public async Task IsUsernameAvailableAsync_SoftDeletedEmployeesUsername_ReturnsFalse()
  {
    // Arrange
    var former = NewEmployee("Juan", "Cruz", "EMP-001");   // username "emp-001"
    former.DeletedAt = DateTime.UtcNow;
    _context.Employees.Add(former);
    await _context.SaveChangesAsync();

    // Act
    var available = await _service.IsUsernameAvailableAsync("emp-001");

    // Assert
    Assert.False(available);
  }

  [Fact]
  public async Task IsUsernameAvailableAsync_OwnUsernameExcluded_ReturnsTrue()
  {
    // Arrange
    var employee = NewEmployee("Juan", "Cruz", "EMP-001");   // username "emp-001"
    _context.Employees.Add(employee);
    await _context.SaveChangesAsync();

    // Act: the employee keeping their own username during an update
    var available = await _service.IsUsernameAvailableAsync("emp-001", excludeEmployeeId: employee.Id);

    // Assert
    Assert.True(available);
  }

  [Fact]
  public async Task IsUsernameAvailableAsync_OtherEmployeesUsernameWithExclude_ReturnsFalse()
  {
    // Arrange
    var juan = NewEmployee("Juan", "Cruz", "EMP-001");     // username "emp-001"
    var maria = NewEmployee("Maria", "Reyes", "EMP-002");  // username "emp-002"
    _context.Employees.AddRange(juan, maria);
    await _context.SaveChangesAsync();

    // Act: Maria trying to take Juan's username
    var available = await _service.IsUsernameAvailableAsync("emp-001", excludeEmployeeId: maria.Id);

    // Assert
    Assert.False(available);
  }
}

