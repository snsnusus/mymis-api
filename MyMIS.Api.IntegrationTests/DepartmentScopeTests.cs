using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Authorization;
using MyMIS.Api.IntegrationTests.Infrastructure;
using MyMIS.Api.Models;

namespace MyMIS.Api.IntegrationTests;

/// <summary>
/// Gate 2 over real HTTP, with the caller holding each permission department-scoped (MANAGER-style).
/// Positions' basic cases live in ApiConflictTests.
/// </summary>
public class DepartmentScopeTests
{
  /// <summary>A logged-in client in <c><paramref name="departmentId"/></c>, holding these permissions department-scoped.</summary>
  private static Task<HttpClient> ClientInDepartmentAsync(ApiFactory factory, int departmentId, params string[] permissions) =>
    factory.CreateDepartmentScopedClientAsync(departmentId, permissions);

  /// <summary>An employee with no permissions, placed in <c><paramref name="department"/></c>.</summary>
  private static Task<Employee> SeedTargetInAsync(ApiFactory factory, Department department) =>
    factory.SeedDepartmentScopedEmployeeAsync(department.Id);

  [Collection(IntegrationCollection.Name)]
  public class Departments(ApiFactory factory)
  {
    /// <summary>Keeps the seeded name and slug, so the unique slug index can't cause a 409.</summary>
    private static object Body(Department department) => new
    {
      name = department.Name,
      slug = department.Slug,
      description = "Updated by a department-scoped test.",
      status = "Active",
    };

    [Fact]
    public async Task OwnDepartment_Returns200()
    {
      var department = await factory.SeedDepartmentAsync();
      using var client = await ClientInDepartmentAsync(factory, department.Id, PermissionNames.Departments.Update);

      var response = await client.PutAsJsonAsync($"/api/Departments/{department.Id}", Body(department));

      await HttpAssert.StatusAsync(HttpStatusCode.OK, response);
    }

    [Fact]
    public async Task AnotherDepartment_Returns403()
    {
      var ownDepartment = await factory.SeedDepartmentAsync();
      var otherDepartment = await factory.SeedDepartmentAsync();
      using var client = await ClientInDepartmentAsync(factory, ownDepartment.Id, PermissionNames.Departments.Update);

      var response = await client.PutAsJsonAsync($"/api/Departments/{otherDepartment.Id}", Body(otherDepartment));

      await HttpAssert.StatusAsync(HttpStatusCode.Forbidden, response);
    }
  }

  [Collection(IntegrationCollection.Name)]
  public class WorkDetails(ApiFactory factory)
  {
    private static readonly object Body = new
    {
      officeLocation = "Makati Office",
      workSchedule = "Mon-Fri 9:00-18:00",
    };

    private Task<Employee> SavedAsync(int employeeId) =>
      factory.QueryDbAsync(db => db.Employees.AsNoTracking().SingleAsync(e => e.Id == employeeId));

    [Fact]
    public async Task EmployeeInOwnDepartment_Returns200AndSaves()
    {
      var department = await factory.SeedDepartmentAsync();
      var target = await SeedTargetInAsync(factory, department);
      using var client = await ClientInDepartmentAsync(factory, department.Id, PermissionNames.Employees.WorkDetailsUpdate);

      var response = await client.PutAsJsonAsync($"/api/Employees/partial/{target.Id}", Body);

      await HttpAssert.StatusAsync(HttpStatusCode.OK, response);
      Assert.Equal("Makati Office", (await SavedAsync(target.Id)).OfficeLocation);
    }

    [Fact]
    public async Task EmployeeInAnotherDepartment_Returns403AndSavesNothing()
    {
      var ownDepartment = await factory.SeedDepartmentAsync();
      var otherDepartment = await factory.SeedDepartmentAsync();
      var target = await SeedTargetInAsync(factory, otherDepartment);
      using var client = await ClientInDepartmentAsync(factory, ownDepartment.Id, PermissionNames.Employees.WorkDetailsUpdate);

      var response = await client.PutAsJsonAsync($"/api/Employees/partial/{target.Id}", Body);

      await HttpAssert.StatusAsync(HttpStatusCode.Forbidden, response);
      Assert.Null((await SavedAsync(target.Id)).OfficeLocation);
    }

    /// <summary>No department becomes <c>-1</c> in the guard, which a department scope never matches.</summary>
    [Fact]
    public async Task EmployeeWithNoDepartment_Returns403()
    {
      var department = await factory.SeedDepartmentAsync();
      var target = await factory.SeedEmployeeAsync();
      using var client = await ClientInDepartmentAsync(factory, department.Id, PermissionNames.Employees.WorkDetailsUpdate);

      var response = await client.PutAsJsonAsync($"/api/Employees/partial/{target.Id}", Body);

      await HttpAssert.StatusAsync(HttpStatusCode.Forbidden, response);
    }

    [Fact]
    public async Task EmployeeThatDoesNotExist_Returns404NotForbidden()
    {
      var department = await factory.SeedDepartmentAsync();
      using var client = await ClientInDepartmentAsync(factory, department.Id, PermissionNames.Employees.WorkDetailsUpdate);

      var response = await client.PutAsJsonAsync("/api/Employees/partial/999999", Body);

      await HttpAssert.StatusAsync(HttpStatusCode.NotFound, response);
    }
  }

  /// <summary>Every sub-resource controller uses the same EmployeeAccessGuard call; phones stands in for all.</summary>
  [Collection(IntegrationCollection.Name)]
  public class SubResources(ApiFactory factory)
  {
    [Fact]
    public async Task EmployeeInOwnDepartment_Returns200()
    {
      var department = await factory.SeedDepartmentAsync();
      var target = await SeedTargetInAsync(factory, department);
      using var client = await ClientInDepartmentAsync(factory, department.Id, PermissionNames.Employees.Read);

      var response = await client.GetAsync($"/api/Employees/{target.Id}/phones");

      await HttpAssert.StatusAsync(HttpStatusCode.OK, response);
    }

    [Fact]
    public async Task EmployeeInAnotherDepartment_Returns403()
    {
      var ownDepartment = await factory.SeedDepartmentAsync();
      var otherDepartment = await factory.SeedDepartmentAsync();
      var target = await SeedTargetInAsync(factory, otherDepartment);
      using var client = await ClientInDepartmentAsync(factory, ownDepartment.Id, PermissionNames.Employees.Read);

      var response = await client.GetAsync($"/api/Employees/{target.Id}/phones");

      await HttpAssert.StatusAsync(HttpStatusCode.Forbidden, response);
    }

    [Fact]
    public async Task CompanyScopedCaller_AnotherDepartment_Returns200()
    {
      var otherDepartment = await factory.SeedDepartmentAsync();
      var target = await SeedTargetInAsync(factory, otherDepartment);
      var hr = await factory.GetClientAsync(PermissionNames.Employees.Read);

      var response = await hr.GetAsync($"/api/Employees/{target.Id}/phones");

      await HttpAssert.StatusAsync(HttpStatusCode.OK, response);
    }
  }

  [Collection(IntegrationCollection.Name)]
  public class Positions(ApiFactory factory)
  {
    private static object Body(int departmentId, string slug) => new
    {
      title = "Associate",
      slug,
      description = "A position description.",
      sortOrder = 1,
      isActive = true,
      isApprover = false,
      departmentId,
    };

    /// <summary>The position is in the caller's department, but the body moves it out (the dto.DepartmentId check).</summary>
    [Fact]
    public async Task MovingIntoAnotherDepartment_Returns403AndKeepsItInPlace()
    {
      var ownDepartment = await factory.SeedDepartmentAsync();
      var otherDepartment = await factory.SeedDepartmentAsync();
      var position = await factory.SeedPositionAsync(ownDepartment);
      using var client = await ClientInDepartmentAsync(factory, ownDepartment.Id, PermissionNames.Positions.Update);

      var response = await client.PutAsJsonAsync($"/api/Positions/{position.Id}", Body(otherDepartment.Id, position.Slug));

      await HttpAssert.StatusAsync(HttpStatusCode.Forbidden, response);
      var saved = await factory.QueryDbAsync(db => db.Positions.AsNoTracking().SingleAsync(p => p.Id == position.Id));
      Assert.Equal(ownDepartment.Id, saved.DepartmentId);
    }
  }
}