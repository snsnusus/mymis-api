using Microsoft.EntityFrameworkCore;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;

namespace MyMIS.Api.Tests.Services.EmployeeServiceTests;

public class DeleteAsyncTests : EmployeeServiceTestBase
{
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
    var created = await _service.CreateAsync(createDto, 1);

    // Act
    var deleteResult = await _service.DeleteAsync(created.Id);

    // Assert
    Assert.True(deleteResult);

    // Normal query respects the global filter — should NOT find it
    var viaService = await _service.GetByIdAsync(created.Id, includePrivateDetails: false);
    Assert.Null(viaService);

    // Bypassing the filter proves the row still physically exists
    var rawRow = await _context.Employees
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(e => e.Id == created.Id);

    Assert.NotNull(rawRow);
    Assert.NotNull(rawRow.DeletedAt);
  }
}