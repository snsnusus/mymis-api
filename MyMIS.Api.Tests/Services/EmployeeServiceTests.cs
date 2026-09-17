using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;
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
        _service = new EmployeeService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
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
}

