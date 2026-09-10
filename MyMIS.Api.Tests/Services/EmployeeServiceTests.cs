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
}

