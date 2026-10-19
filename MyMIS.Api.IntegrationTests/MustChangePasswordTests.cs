using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyMIS.Api.IntegrationTests.Infrastructure;
using MyMIS.Api.Models;
using static MyMIS.Api.IntegrationTests.Infrastructure.TestData;

namespace MyMIS.Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public class MustChangePasswordTests(ApiFactory factory)
{

  private const string NewPassword = "My-New-Password-123!";

  private async Task<Employee> SeedFlaggedEmployeeAsync(Role role = Role.User, params string[] permissions)
  {
    var employee = await factory.SeedEmployeeAsync(role, permissions);

    await factory.ExecuteDbAsync(async db =>
      await db.Employees
        .Where(e => e.Id == employee.Id)
        .ExecuteUpdateAsync(set => set.SetProperty(e => e.MustChangePassword, true)));

    return employee;
  }

  [Fact]
  public async Task FlaggedUser_CallingAProtectedRoute_Gets403WithPasswordChangeRequiredCode()
  {
    // Arrange
    var employee = await SeedFlaggedEmployeeAsync(Role.User);
    using var client = await factory.CreateClientForAsync(employee);

    // Act
    var response = await client.GetAsync("/api/Employees");

    // Assert
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
    Assert.Equal("password_change_required", body.GetProperty("code").GetString());
  }

  [Fact]
  public async Task FlaggedUser_CallingAProtectedRoute_Gets403EvenWithPermission()
  {
    // Arrange
    var employee = await SeedFlaggedEmployeeAsync(Role.User, "employees.create");
    using var client = await factory.CreateClientForAsync(employee);

    // Act
    var response = await client.PostAsJsonAsync("/api/Employees", new { });

    // Assert
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
    Assert.Equal("password_change_required", body.GetProperty("code").GetString());
  }

  [Fact]
  public async Task FlaggedUser_CallingChangePassword_IsAllowedThrough()
  {
    // Arrange
    var employee = await SeedFlaggedEmployeeAsync(Role.User);
    using var client = await factory.CreateClientForAsync(employee);

    // Act
    var response = await client.PostAsJsonAsync("/api/Auth/change-password", new { currentPassword = ApiFactory.DefaultPassword, newPassword = NewPassword });

    // Assert
    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
  }

  [Fact]
  public async Task FlaggedUser_AfterChangingPasswordAndLoggingInAgain_IsNoLongerBlocked()
  {
    // Arrange
    var employee = await SeedFlaggedEmployeeAsync(Role.User);
    using var client = await factory.CreateClientForAsync(employee);
    var changePasswordResponse = await client.PostAsJsonAsync("/api/Auth/change-password", new { currentPassword = ApiFactory.DefaultPassword, newPassword = NewPassword });
    Assert.Equal(HttpStatusCode.NoContent, changePasswordResponse.StatusCode);
    var token = await factory.LoginAsync(employee.Username, NewPassword);
    using var newClient = factory.CreateApiClient();
    newClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    // Act
    var employeesResponse = await newClient.GetAsync("/api/Employees");

    // Assert
    Assert.Equal(HttpStatusCode.OK, employeesResponse.StatusCode);
  }
}