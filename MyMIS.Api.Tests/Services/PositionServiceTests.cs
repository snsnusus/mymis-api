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

  // ---------------- GetAllAsync: departmentId filter ----------------

  private async Task<Department> SeedDepartmentAsync(string name, string slug)
  {
    var department = new Department { Name = name, Slug = slug, Status = "Active" };
    _context.Departments.Add(department);
    await _context.SaveChangesAsync();
    return department;
  }

  private async Task SeedPositionAsync(Department department, string title, string slug, int sortOrder = 1)
  {
    _context.Positions.Add(new Position
    {
      Title = title,
      Slug = slug,
      Description = "A position description.",
      SortOrder = sortOrder,
      IsActive = true,
      IsApprover = false,
      DepartmentId = department.Id,
    });
    await _context.SaveChangesAsync();
  }

  [Fact]
  public async Task GetAllAsync_FilteredByDepartment_ReturnsOnlyThatDepartmentsPositions()
  {
    // Arrange: an uneven split, so a filter that did nothing would be noticed
    var hr = await SeedDepartmentAsync("Human Resources", "HRD");
    var tech = await SeedDepartmentAsync("Information Technology", "ITD");
    await SeedPositionAsync(hr, "Manager", "MNGR");
    await SeedPositionAsync(hr, "Associate", "ASSOC");
    await SeedPositionAsync(hr, "Director", "DIR");
    await SeedPositionAsync(tech, "Developer", "DEV");

    // Act
    var result = await _service.GetAllAsync(hr.Id);

    // Assert
    Assert.Equal(3, result.Count);
    Assert.All(result, p => Assert.Equal(hr.Id, p.DepartmentId));
  }

  [Fact]
  public async Task GetAllAsync_NoFilter_ReturnsPositionsOfEveryDepartment()
  {
    // Arrange
    var hr = await SeedDepartmentAsync("Human Resources", "HRD");
    var tech = await SeedDepartmentAsync("Information Technology", "ITD");
    await SeedPositionAsync(hr, "Manager", "MNGR");
    await SeedPositionAsync(tech, "Developer", "DEV");

    // Act
    var result = await _service.GetAllAsync();

    // Assert
    Assert.Equal(2, result.Count);
  }

  [Fact]
  public async Task GetAllAsync_Ordering_IsDepartmentNameThenSortOrderThenTitle()
  {
    // Arrange: seeded scrambled. "Beta" and "Zed" share SortOrder 1, so the title breaks the tie.
    var tech = await SeedDepartmentAsync("Information Technology", "ITD");
    var hr = await SeedDepartmentAsync("Human Resources", "HRD");
    await SeedPositionAsync(tech, "Dev", "DEV", sortOrder: 1);
    await SeedPositionAsync(hr, "Alpha", "ALPHA", sortOrder: 2);
    await SeedPositionAsync(hr, "Zed", "ZED", sortOrder: 1);
    await SeedPositionAsync(hr, "Beta", "BETA", sortOrder: 1);

    // Act
    var result = await _service.GetAllAsync();

    // Assert: Human Resources before Information Technology, then SortOrder, then Title
    Assert.Equal(
      new[] { "Beta", "Zed", "Alpha", "Dev" },
      result.Select(p => p.Title));
  }

  [Fact]
  public async Task GetAllAsync_FilteredByDepartment_OrdersBySortOrderThenTitle()
  {
    // Arrange
    var hr = await SeedDepartmentAsync("Human Resources", "HRD");
    await SeedPositionAsync(hr, "Zed", "ZED", sortOrder: 1);
    await SeedPositionAsync(hr, "Alpha", "ALPHA", sortOrder: 3);
    await SeedPositionAsync(hr, "Beta", "BETA", sortOrder: 1);

    // Act
    var result = await _service.GetAllAsync(hr.Id);

    // Assert
    Assert.Equal(new[] { "Beta", "Zed", "Alpha" }, result.Select(p => p.Title));
  }

  [Fact]
  public async Task GetAllAsync_DepartmentWithNoPositionsOrUnknownDepartment_ReturnsEmptyList()
  {
    // Arrange
    var empty = await SeedDepartmentAsync("Finance", "FIN");

    // Act + Assert: an existing department with no positions, and an id that doesn't exist
    Assert.Empty(await _service.GetAllAsync(empty.Id));
    Assert.Empty(await _service.GetAllAsync(9999));
  }
}