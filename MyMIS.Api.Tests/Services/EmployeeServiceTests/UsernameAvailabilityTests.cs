namespace MyMIS.Api.Tests.Services.EmployeeServiceTests;

public class UsernameAvailabilityTests : EmployeeServiceTestBase
{
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
    _context.Employees.Add(NewEmployee("Juan", "Cruz", "EMP-001"));
    await _context.SaveChangesAsync();

    // Act
    var available = await _service.IsUsernameAvailableAsync(input);

    // Assert
    Assert.False(available);
  }

  [Fact]
  public async Task IsUsernameAvailableAsync_SoftDeletedEmployeesUsername_ReturnsTrue()
  {
    // Arrange
    var former = NewEmployee("Juan", "Cruz", "EMP-001");
    former.DeletedAt = DateTime.UtcNow;
    _context.Employees.Add(former);
    await _context.SaveChangesAsync();

    // Act
    var available = await _service.IsUsernameAvailableAsync("emp-001");

    // Assert
    Assert.True(available);
  }

  [Fact]
  public async Task IsUsernameAvailableAsync_OwnUsernameExcluded_ReturnsTrue()
  {
    // Arrange
    var employee = NewEmployee("Juan", "Cruz", "EMP-001");
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
    var juan = NewEmployee("Juan", "Cruz", "EMP-001");
    var maria = NewEmployee("Maria", "Reyes", "EMP-002");
    _context.Employees.AddRange(juan, maria);
    await _context.SaveChangesAsync();

    // Act: Maria trying to take Juan's username
    var available = await _service.IsUsernameAvailableAsync("emp-001", excludeEmployeeId: maria.Id);

    // Assert
    Assert.False(available);
  }

  [Fact]
  public async Task IsUsernameAvailableAsync_SameUsernameOnDeletedAndLiveEmployees_ReturnsFalse()
  {
    // Arrange
    var activeEmployee = NewEmployee("Juan", "Cruz", "EMP-001");
    activeEmployee.Username = "emp-001";
    var deletedEmployee = NewEmployee("Juan", "Cruz", "EMP-002");
    deletedEmployee.Username = "emp-001";
    deletedEmployee.DeletedAt = now;

    _context.Employees.AddRange(activeEmployee, deletedEmployee);
    await _context.SaveChangesAsync();

    // Act
    var isAvailable = await _service.IsUsernameAvailableAsync("emp-001");

    // Assert
    Assert.False(isAvailable);
  }
}