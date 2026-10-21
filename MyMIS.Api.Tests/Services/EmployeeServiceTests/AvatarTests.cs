using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Models;

namespace MyMIS.Api.Tests.Services.EmployeeServiceTests;

public class AvatarTests : EmployeeServiceTestBase
{
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

}