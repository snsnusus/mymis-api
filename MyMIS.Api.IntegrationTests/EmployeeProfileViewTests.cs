using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MyMIS.Api.Authorization;
using MyMIS.Api.IntegrationTests.Infrastructure;
using MyMIS.Api.Models;
using static MyMIS.Api.IntegrationTests.Infrastructure.TestData;

namespace MyMIS.Api.IntegrationTests;

/// <summary>
/// Which view of an employee each caller gets (design notes, section 7): the full record for
/// the employee and <c>employees.read</c>, the partial profile for everyone else.
/// </summary>
public class EmployeeProfileViewTests
{
  /// <summary>A target in its own department, with a hidden personal phone and a corporate email.</summary>
  private record Target(Employee Employee, Department Department, int HiddenPhoneId, int CorporateEmailId);

  private static async Task<Target> SeedTargetAsync(ApiFactory factory)
  {
    var department = await factory.SeedDepartmentAsync();
    var employee = await factory.SeedDepartmentScopedEmployeeAsync(department.Id);

    var (phoneId, emailId) = await factory.QueryDbAsync(async db =>
    {
      var now = DateTime.UtcNow;

      var phone = new EmployeePhone
      {
        EmployeeId = employee.Id,
        Ownership = ContactOwnership.Personal,
        LineType = PhoneLineType.Mobile,
        Phone = new Phone { CountryCode = "PH", Number = NewMobile() },
        IsPrimary = true,
        ShowInProfile = false,
        CreatedAt = now,
        UpdatedAt = now,
      };

      var email = new EmployeeEmail
      {
        EmployeeId = employee.Id,
        Ownership = ContactOwnership.Corporate,
        Email = $"it-{Unique()}@company.test",
        IsPrimary = true,
        ShowInProfile = false,
        CreatedAt = now,
        UpdatedAt = now,
      };

      db.EmployeePhones.Add(phone);
      db.EmployeeEmails.Add(email);
      await db.SaveChangesAsync();
      return (phone.Id, email.Id);
    });

    return new Target(employee, department, phoneId, emailId);
  }

  private static async Task<JsonElement> ReadBodyAsync(HttpResponseMessage response)
  {
    await HttpAssert.StatusAsync(HttpStatusCode.OK, response);
    return await response.Content.ReadFromJsonAsync<JsonElement>();
  }

  private static IEnumerable<int> Ids(JsonElement body, string list) =>
    body.GetProperty(list).EnumerateArray().Select(item => item.GetProperty("id").GetInt32());

  /// <summary>The full record: username present, and the hidden phone listed with its flag.</summary>
  private static void AssertFullView(JsonElement body, Target target)
  {
    Assert.True(body.TryGetProperty("username", out _));

    var hiddenPhone = Assert.Single(
      body.GetProperty("phones").EnumerateArray(),
      phone => phone.GetProperty("id").GetInt32() == target.HiddenPhoneId);
    Assert.False(hiddenPhone.GetProperty("showInProfile").GetBoolean());
  }

  /// <summary>The partial profile: private keys absent, hidden phone gone, corporate email kept.</summary>
  private static void AssertPartialView(JsonElement body, Target target)
  {
    Assert.False(body.TryGetProperty("username", out _));
    Assert.False(body.TryGetProperty("gender", out _));
    Assert.Empty(Ids(body, "phones"));
    Assert.Equal(target.CorporateEmailId, Assert.Single(Ids(body, "emails")));
  }

  [Collection(IntegrationCollection.Name)]
  public class GetById(ApiFactory factory)
  {
    [Fact]
    public async Task TheEmployeeThemselves_GetsTheFullRecord()
    {
      var target = await SeedTargetAsync(factory);
      using var client = await factory.CreateClientForAsync(target.Employee);

      var body = await ReadBodyAsync(await client.GetAsync($"/api/Employees/{target.Employee.Id}"));

      AssertFullView(body, target);
    }

    [Fact]
    public async Task HrWithEmployeesRead_GetsTheFullRecord()
    {
      var target = await SeedTargetAsync(factory);
      var hr = await factory.GetClientAsync(PermissionNames.Employees.Read);

      var body = await ReadBodyAsync(await hr.GetAsync($"/api/Employees/{target.Employee.Id}"));

      AssertFullView(body, target);
    }

    [Fact]
    public async Task AnotherEmployee_GetsThePartialProfile()
    {
      var target = await SeedTargetAsync(factory);
      var coworker = await factory.GetClientAsync();

      var body = await ReadBodyAsync(await coworker.GetAsync($"/api/Employees/{target.Employee.Id}"));

      AssertPartialView(body, target);
    }
  }

  [Collection(IntegrationCollection.Name)]
  public class UpdatePartial(ApiFactory factory)
  {
    /// <summary>A MANAGER without <c>employees.read</c> gets the partial profile back, not the full record.</summary>
    [Fact]
    public async Task ManagerInTheSameDepartment_GetsThePartialProfileBack()
    {
      var target = await SeedTargetAsync(factory);
      using var manager = await factory.CreateDepartmentScopedClientAsync(
        target.Department.Id, PermissionNames.Employees.WorkDetailsUpdate);

      var response = await manager.PutAsJsonAsync($"/api/Employees/partial/{target.Employee.Id}", new
      {
        officeLocation = "Makati Office",
        workSchedule = "Mon-Fri 9:00-18:00",
      });

      var body = await ReadBodyAsync(response);
      Assert.Equal("Makati Office", body.GetProperty("officeLocation").GetString());
      AssertPartialView(body, target);
    }
  }
}