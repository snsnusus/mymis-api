using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Models;

namespace MyMIS.Api.Tests.Services.EmployeeServiceTests;

public class GetPagedAsyncTests : EmployeeServiceTestBase
{
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

    // Assert: no rows on the page, but the total is still correct
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

}