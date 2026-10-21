using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Models;

namespace MyMIS.Api.Tests.Services.EmployeeServiceTests;

public class GetLookupAsyncTests : EmployeeServiceTestBase
{
  [Fact]
  public async Task GetLookupAsync_WithIds_ReturnsOnlyThoseEmployeesInNameOrder()
  {
    // Arrange
    var maria = NewEmployee("Maria", "Santos", "EMP-003");
    var ana = NewEmployee("Ana", "Cruz", "EMP-001");
    var ben = NewEmployee("Ben", "Reyes", "EMP-002");
    _context.Employees.AddRange(maria, ana, ben);
    await _context.SaveChangesAsync();

    // Act: ask for Santos and Cruz, in the "wrong" order
    var result = await _service.GetLookupAsync(null, 20, [maria.Id, ana.Id]);

    // Assert: only those two, still sorted by last name (Reyes is excluded)
    Assert.Equal(2, result.Count);
    Assert.Equal("Cruz", result[0].LastName);
    Assert.Equal("Santos", result[1].LastName);
  }

  [Fact]
  public async Task GetLookupAsync_EmptyIds_IsTreatedAsNoFilter()
  {
    // Arrange
    _context.Employees.AddRange(
        NewEmployee("Maria", "Santos", "EMP-002"),
        NewEmployee("Ana", "Cruz", "EMP-001"));
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetLookupAsync(null, 20, []);

    // Assert: an empty array means "no id filter", not "match nothing"
    Assert.Equal(2, result.Count);
  }

  [Fact]
  public async Task GetLookupAsync_IdsThatDoNotExist_AreIgnored()
  {
    // Arrange
    var maria = NewEmployee("Maria", "Santos", "EMP-001");
    _context.Employees.Add(maria);
    await _context.SaveChangesAsync();

    // Act: one real id, one that doesn't exist
    var result = await _service.GetLookupAsync(null, 20, [maria.Id, 999]);

    // Assert: the missing id is skipped, no error
    var match = Assert.Single(result);
    Assert.Equal(maria.Id, match.Id);
  }

  [Fact]
  public async Task GetLookupAsync_IdsAndSearch_AppliesBoth()
  {
    // Arrange
    var maria = NewEmployee("Maria", "Santos", "EMP-002");
    var ana = NewEmployee("Ana", "Cruz", "EMP-001");
    _context.Employees.AddRange(maria, ana);
    await _context.SaveChangesAsync();

    // Act: both ids, but a search that only matches Maria
    var result = await _service.GetLookupAsync("maria", 20, [maria.Id, ana.Id]);

    // Assert: an employee must pass both filters
    var match = Assert.Single(result);
    Assert.Equal(maria.Id, match.Id);
  }

  [Fact]
  public async Task GetLookupAsync_MoreIdsThanLimit_ReturnsFirstNAlphabetically()
  {
    // Arrange
    var maria = NewEmployee("Maria", "Santos", "EMP-003");
    var ana = NewEmployee("Ana", "Cruz", "EMP-001");
    var ben = NewEmployee("Ben", "Reyes", "EMP-002");
    _context.Employees.AddRange(maria, ana, ben);
    await _context.SaveChangesAsync();

    // Act: three ids, limit 2
    var result = await _service.GetLookupAsync(null, 2, [maria.Id, ana.Id, ben.Id]);

    // Assert: Santos is cut, so the limit still applies after sorting
    Assert.Equal(2, result.Count);
    Assert.Equal("Cruz", result[0].LastName);
    Assert.Equal("Reyes", result[1].LastName);
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
}