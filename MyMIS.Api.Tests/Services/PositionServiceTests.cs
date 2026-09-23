using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;
using MyMIS.Api.Services;

namespace MyMIS.Api.Tests.Services;

public class PositionServiceTests : IDisposable
{
  private readonly AppDbContext _context;
  private readonly PositionService _service;

  public PositionServiceTests()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    _context = new AppDbContext(options);
    _service = new PositionService(_context);
  }

  public void Dispose()
  {
    _context.Dispose();
    GC.SuppressFinalize(this);
  }

  [Fact]
  public async Task CreateAsync_ValidPosition_PersistsAndReturnsCorrectData()
  {
    // Arrange
    var department = new Department
    {
      Name = "Human Resources",
      Slug = "HRD",
      Status = "Active",
    };
    _context.Departments.Add(department);
    await _context.SaveChangesAsync();

    var dto = new PositionCreateDto
    {
      Title = "Manager",
      Slug = "MNGR",
      Description = "This is a position description.",
      SortOrder = 2,
      IsActive = true,
      IsApprover = true,
      DepartmentId = department.Id
    };

    // Act
    var result = await _service.CreateAsync(dto);

    // Assert
    Assert.NotNull(result);
    Assert.True(result.Id > 0);
    Assert.Equal("Manager", result.Title);
    Assert.Equal("MNGR", result.Slug);
    Assert.Equal("This is a position description.", result.Description);
    Assert.Equal(2, result.SortOrder);
    Assert.True(result.IsActive);
    Assert.True(result.IsApprover);
    Assert.Equal(department.Id, result.DepartmentId);
    Assert.Equal("Human Resources", result.DepartmentName);

    var savedPosition = await _context.Positions
        .FirstAsync(p => p.Id == result.Id);

    Assert.NotNull(savedPosition);
    Assert.Equal(result.Title, savedPosition.Title);
  }

  [Fact]
  public async Task CreateAsync_ReturnsCorrectDepartmentName()
  {
    // Arrange
    var department1 = new Department
    {
      Name = "Information Technology",
      Slug = "ITD",
      Status = "Active"
    };
    var department2 = new Department
    {
      Name = "Human Resources",
      Slug = "HRD",
      Status = "Active"
    };
    _context.Departments.AddRange(department1, department2);
    await _context.SaveChangesAsync();

    var position1 = new PositionCreateDto
    {
      Title = "Associate",
      Slug = "ASSOC",
      Description = "This is a associate description.",
      SortOrder = 1,
      IsActive = true,
      IsApprover = false,
      DepartmentId = department1.Id,
    };
    var position2 = new PositionCreateDto
    {
      Title = "Regional Manager",
      Slug = "REG_MNGR",
      Description = "This is a regional manager description.",
      SortOrder = 1,
      IsActive = true,
      IsApprover = true,
      DepartmentId = department2.Id,
    };

    // Act
    var result1 = await _service.CreateAsync(position1);
    var result2 = await _service.CreateAsync(position2);

    // Assert
    Assert.NotNull(result1);
    Assert.Equal("Information Technology", result1.DepartmentName);

    Assert.NotNull(result2);
    Assert.Equal("Human Resources", result2.DepartmentName);
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
  public async Task GetByIdAsync_ExistingId_ReturnsMatchingPosition()
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
      DepartmentId = department.Id
    };
    _context.Positions.Add(position);
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetByIdAsync(position.Id);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(position.Title, result.Title);
    Assert.Equal(position.Slug, result.Slug);
    Assert.Equal(position.Description, result.Description);
    Assert.Equal(position.SortOrder, result.SortOrder);
    Assert.True(result.IsActive);
    Assert.True(result.IsApprover);
    Assert.Equal(position.Id, result.Id);
    Assert.Equal(position.DepartmentId, result.DepartmentId);
    Assert.Equal(department.Name, result.DepartmentName);
  }

  [Fact]
  public async Task UpdateAsync_ValidUpdate_PersistsChanges()
  {
    // Arrange
    var department1 = new Department
    {
      Name = "Information Technology",
      Slug = "ITD",
      Status = "Active"
    };
    var department2 = new Department
    {
      Name = "Human Resources",
      Slug = "HRD",
      Status = "Active"
    };
    _context.Departments.AddRange(department1, department2);
    await _context.SaveChangesAsync();

    var position = new Position
    {
      Title = "Associate",
      Slug = "ASSOC",
      Description = "This is a sample description.",
      SortOrder = 1,
      IsActive = true,
      IsApprover = false,
      DepartmentId = department1.Id
    };
    _context.Positions.Add(position);
    await _context.SaveChangesAsync();

    var dto = new PositionCreateDto
    {
      Title = "Senior Engineer",
      Slug = "SNR_ENG",
      Description = "A changed description.",
      SortOrder = 2,
      IsActive = false,
      IsApprover = true,
      DepartmentId = department2.Id,
    };

    // Act
    var result = await _service.UpdateAsync(position.Id, dto);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(dto.Title, result.Title);
    Assert.Equal(dto.Slug, result.Slug);
    Assert.Equal(dto.Description, result.Description);
    Assert.Equal(dto.SortOrder, result.SortOrder);
    Assert.False(result.IsActive);
    Assert.True(result.IsApprover);
    Assert.Equal(dto.DepartmentId, result.DepartmentId);
    Assert.Equal(department2.Name, result.DepartmentName);
  }

  [Fact]
  public async Task UpdateAsync_NonExistentId_ReturnsNull()
  {
    // Arrange
    var dto = new PositionCreateDto
    {
      Title = "irrelevant",
      Slug = "also_irrelevant",
      SortOrder = 1,
      IsActive = true,
      IsApprover = false,
      DepartmentId = 999
    };

    // Act
    var result = await _service.UpdateAsync(999, dto);

    // Assert
    Assert.Null(result);
  }

  [Fact]
  public async Task DeleteAsync_ExistingPosition_RemovesRowEntirely()
  {
    // Arrange
    var department = new Department
    {
      Name = "Test Department",
      Slug = "TDD",
      Status = "Active"
    };
    _context.Departments.Add(department);
    await _context.SaveChangesAsync();

    var position = new Position
    {
      Title = "Test Position",
      Slug = "TP",
      SortOrder = 1,
      IsActive = true,
      IsApprover = false,
      DepartmentId = department.Id
    };
    _context.Positions.Add(position);
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.DeleteAsync(position.Id);

    // Assert
    Assert.True(result);

    var deletedPosition = await _context.Positions.FirstOrDefaultAsync(p => p.Id == position.Id);
    Assert.Null(deletedPosition);
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