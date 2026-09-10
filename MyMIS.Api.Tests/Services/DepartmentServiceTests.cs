using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.Services;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;

namespace MyMIS.Api.Tests.Services;

public class DepartmentServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly DepartmentService _service;

    public DepartmentServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _service = new DepartmentService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task CreateAsync_ValidDepartment_PersistsAndReturnsCorrectData()
    {
        // Arrange
        var dto = new DepartmentCreateDto
        {
            Name = "Information Technology",
            Slug = "ITD",
            Description = "This is a sample description",
            Status = "Active",
            PrimaryContactId = null,
            SecondaryContactId = null,
        };

        // Act
        var created = await _service.CreateAsync(dto);

        Assert.True(created.Id > 0);

        // Assert
        Assert.NotNull(created);

        Assert.Equal(dto.Name, created.Name);
        Assert.Equal(dto.Slug, created.Slug);
        Assert.Equal(dto.Description, created.Description);
        Assert.Equal(dto.Status, created.Status);

        var savedDepartment = await _context.Departments.FirstAsync(d => d.Id == created.Id);
        Assert.Equal(dto.Name, savedDepartment.Name);
    }

    [Fact]
    public async Task CreateAsync_PrimaryAndSecondaryContact_ReturnsCorrectContactNames()
    {
        // Arrange
        var employee1 = new Employee
        {
            FirstName = "Jazmine Ciel",
            LastName = "Nares",
            Gender = "FEMALE",
            MaritalStatus = "SINGLE",
            EmployeeCode = "EMP100",
            Username = "jazmineciel",
            PasswordHash = "irrelevant-for-this-test"
        };

        var employee2 = new Employee
        {
            FirstName = "Sofia Leigh",
            LastName = "Vargas",
            Gender = "FEMALE",
            MaritalStatus = "SINGLE",
            EmployeeCode = "EMP200",
            Username = "sofialeigh",
            PasswordHash = "irrelevant-for-this-test"
        };

        _context.Employees.AddRange(employee1, employee2);
        await _context.SaveChangesAsync();

        var departmentDto = new DepartmentCreateDto
        {
            Name = "Human Resources",
            Slug = "HRD",
            Description = "This is a sample description",
            Status = "Active",
            PrimaryContactId = employee1.Id,
            SecondaryContactId = employee2.Id,
        };

        // Act
        var created = await _service.CreateAsync(departmentDto);

        // Assert
        Assert.Equal($"{employee1.FirstName} {employee1.LastName}", created.PrimaryContactName);
        Assert.Equal($"{employee2.FirstName} {employee2.LastName}", created.SecondaryContactName);
    }

    [Fact]
    public async Task GetByIdAsync_DepartmentWithEmployees_ReturnsAccurateEmployeeCount()
    {
        // Arrange
        var departmentDto = new DepartmentCreateDto
        {
            Name = "Human Resources",
            Slug = "HRD",
            Description = "This is a sample description",
            Status = "Active",
            PrimaryContactId = null,
            SecondaryContactId = null,
        };
        var createdDepartment = await _service.CreateAsync(departmentDto);

        var employee1 = new Employee
        {
            FirstName = "Sofia Leigh",
            LastName = "Vargas",
            Gender = "FEMALE",
            MaritalStatus = "SINGLE",
            EmployeeCode = "EMP200",
            Username = "sofialeigh",
            PasswordHash = "irrelevant-for-this-test",
            DepartmentId = createdDepartment.Id,
        };

        var employee2 = new Employee
        {
            FirstName = "Jazmine Ciel",
            LastName = "Nares",
            Gender = "FEMALE",
            MaritalStatus = "SINGLE",
            EmployeeCode = "EMP300",
            Username = "jazmineciel",
            PasswordHash = "irrelevant-for-this-test",
            DepartmentId = createdDepartment.Id,
        };

        var employee3 = new Employee
        {
            FirstName = "Pea Daphne",
            LastName = "Vargas",
            Gender = "FEMALE",
            MaritalStatus = "SINGLE",
            EmployeeCode = "EMP400",
            Username = "peadaphne",
            PasswordHash = "irrelevant-for-this-test",
            DepartmentId = createdDepartment.Id,
        };

        _context.Employees.AddRange(employee1, employee2, employee3);
        await _context.SaveChangesAsync();

        // Act
        var department = await _service.GetByIdAsync(createdDepartment.Id);

        // Assert
        Assert.NotNull(department);
        Assert.Equal(3, department.EmployeeCount);
    }

    [Fact]
    public async Task GetByIdAsync_EmployeesInOtherDepartment_ExcludesFromEmployeeCount()
    {
        // Arrange
        var targetDepartmentDto = new DepartmentCreateDto
        {
            Name = "Human Resources",
            Slug = "HRD",
            Description = "This is a sample description",
            Status = "Active",
            PrimaryContactId = null,
            SecondaryContactId = null,
        };

        var otherDepartmentDto = new DepartmentCreateDto
        {
            Name = "Finance",
            Slug = "FIN",
            Description = "This is a sample description",
            Status = "Active",
            PrimaryContactId = null,
            SecondaryContactId = null,
        };

        var targetDepartment = await _service.CreateAsync(targetDepartmentDto);
        var otherDepartment = await _service.CreateAsync(otherDepartmentDto);

        var employeeOne = new Employee
        {
            FirstName = "Pea Daphne",
            LastName = "Vargas",
            Gender = "FEMALE",
            MaritalStatus = "SINGLE",
            EmployeeCode = "EMP100",
            Username = "peadaphne",
            PasswordHash = "irrelevant-for-this-test",
            DepartmentId = targetDepartment.Id,
        };

        var employeeTwo = new Employee
        {
            FirstName = "Sofia Legih",
            LastName = "Vargas",
            Gender = "FEMALE",
            MaritalStatus = "SINGLE",
            EmployeeCode = "EMP200",
            Username = "sofialeigh",
            PasswordHash = "irrelevant-for-this-test",
            DepartmentId = targetDepartment.Id,
        };

        var employeeThree = new Employee
        {
            FirstName = "Jazmine Ciel",
            LastName = "Nares",
            Gender = "FEMALE",
            MaritalStatus = "SINGLE",
            EmployeeCode = "EMP300",
            Username = "jazmineciel",
            PasswordHash = "irrelevant-for-this-test",
            DepartmentId = otherDepartment.Id,
        };

        _context.Employees.AddRange(employeeOne, employeeTwo, employeeThree);
        await _context.SaveChangesAsync();

        // Act
        var targetDept = await _service.GetByIdAsync(targetDepartment.Id);
        var otherDept = await _service.GetByIdAsync(otherDepartment.Id);

        // Assert
        Assert.NotNull(targetDept);
        Assert.Equal(2, targetDept.EmployeeCount);
        Assert.NotNull(otherDept);
        Assert.Equal(1, otherDept.EmployeeCount);
    }

    [Fact]
    public async Task DeleteAsync_ExistingDepartment_RemovesRowEntirely()
    {
        // Arrage
        var dto = new DepartmentCreateDto
        {
            Name = "Finance",
            Slug = "FIN",
            Description = "This is a sample description",
            Status = "Active",
            PrimaryContactId = null,
            SecondaryContactId = null,
        };
        var created = await _service.CreateAsync(dto);

        // Act
        var deleted = await _service.DeleteAsync(created.Id);

        // Assert
        Assert.True(deleted);

        var viaService = await _service.GetByIdAsync(created.Id);
        Assert.Null(viaService);

        var viaContext = await _context.Departments
            .FirstOrDefaultAsync(e => e.Id == created.Id);
        Assert.Null(viaContext);
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
    public async Task DeleteAsync_NonExistentId_ReturnsFalse()
    {
        // Act
        var result = await _service.DeleteAsync(999);

        // Assert
        Assert.False(result);
    }
}