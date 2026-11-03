using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Authorization;
using MyMIS.Api.IntegrationTests.Infrastructure;
using MyMIS.Api.Models;
using static MyMIS.Api.IntegrationTests.Infrastructure.TestData;

namespace MyMIS.Api.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public class ShowInProfileApiTests(ApiFactory factory)
{
  // ---------------- helpers ----------------

  private Task<EmployeePhone> SeedPhoneAsync(Employee employee, bool showInProfile) =>
    factory.QueryDbAsync(async db =>
    {
      var phone = new EmployeePhone
      {
        EmployeeId = employee.Id,
        Ownership = ContactOwnership.Personal,
        LineType = PhoneLineType.Mobile,
        Phone = new Phone { CountryCode = "PH", Number = NewMobile() },
        IsPrimary = true,
        ShowInProfile = showInProfile,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
      };
      db.EmployeePhones.Add(phone);
      await db.SaveChangesAsync();
      return phone;
    });

  private Task<EmployeeEmail> SeedEmailAsync(Employee employee, bool showInProfile) =>
    factory.QueryDbAsync(async db =>
    {
      var email = new EmployeeEmail
      {
        EmployeeId = employee.Id,
        Ownership = ContactOwnership.Personal,
        Email = $"it-{Unique()}@example.com",
        IsPrimary = true,
        ShowInProfile = showInProfile,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
      };
      db.EmployeeEmails.Add(email);
      await db.SaveChangesAsync();
      return email;
    });

  private Task<EmployeePhone> SavedPhoneAsync(int id) =>
    factory.QueryDbAsync(db => db.EmployeePhones.AsNoTracking().SingleAsync(p => p.Id == id));

  private Task<EmployeeEmail> SavedEmailAsync(int id) =>
    factory.QueryDbAsync(db => db.EmployeeEmails.AsNoTracking().SingleAsync(e => e.Id == id));

  // ---------------- tests ----------------

  [Fact]
  public async Task Employee_PutOwnPhoneWithShowInProfileFalse_HidesIt()
  {
    // Arrange
    var employee = await factory.SeedEmployeeAsync();
    var phone = await SeedPhoneAsync(employee, showInProfile: true);
    using var client = await factory.CreateClientForAsync(employee);

    // Act
    var response = await client.PutAsJsonAsync($"/api/Employees/me/phones/{phone.Id}", new
    {
      ownership = "Personal",
      phone = new { countryCode = "PH", number = phone.Phone.Number },
      isPrimary = true,
      showInProfile = false,
    });

    // Assert: the response...
    await HttpAssert.StatusAsync(HttpStatusCode.OK, response);
    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
    Assert.False(body.GetProperty("showInProfile").GetBoolean());

    // ...and the database
    Assert.False((await SavedPhoneAsync(phone.Id)).ShowInProfile);
  }

  [Fact]
  public async Task PutWithoutTheShowInProfileKey_KeepsTheSavedValue()
  {
    // Arrange: hidden phone
    var employee = await factory.SeedEmployeeAsync();
    var phone = await SeedPhoneAsync(employee, showInProfile: false);
    using var client = await factory.CreateClientForAsync(employee);

    // Act: an older client's body, no "showInProfile" key at all, but a real change elsewhere
    var response = await client.PutAsJsonAsync($"/api/Employees/me/phones/{phone.Id}", new
    {
      ownership = "Corporate",
      phone = new { countryCode = "PH", number = phone.Phone.Number },
      isPrimary = true,
    });

    // Assert
    await HttpAssert.StatusAsync(HttpStatusCode.OK, response);
    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
    Assert.False(body.GetProperty("showInProfile").GetBoolean());

    var saved = await SavedPhoneAsync(phone.Id);
    Assert.Equal(ContactOwnership.Corporate, saved.Ownership);   // the update ran...
    Assert.False(saved.ShowInProfile);                           // ...and kept the flag
  }

  [Fact]
  public async Task Hr_PutAnotherEmployeesEmailWithShowInProfileFalse_HidesIt()
  {
    // Arrange: HR holds employees.update company-wide (decision S2)
    var target = await factory.SeedEmployeeAsync();
    var email = await SeedEmailAsync(target, showInProfile: true);
    var hr = await factory.GetClientAsync(PermissionNames.Employees.Update);

    // Act
    var response = await hr.PutAsJsonAsync($"/api/Employees/{target.Id}/emails/{email.Id}", new
    {
      ownership = "Personal",
      email = email.Email,
      isPrimary = true,
      showInProfile = false,
    });

    // Assert
    await HttpAssert.StatusAsync(HttpStatusCode.OK, response);
    Assert.False((await SavedEmailAsync(email.Id)).ShowInProfile);
  }

  [Fact]
  public async Task Employee_PutMeWithShowInProfileFalse_HidesPersonalDetails()
  {
    // Arrange: a seeded employee has no personal-details row yet; PUT /me creates it
    var employee = await factory.SeedEmployeeAsync();
    using var client = await factory.CreateClientForAsync(employee);

    // Act
    var response = await client.PutAsJsonAsync("/api/Employees/me", new
    {
      nickname = "Jun",
      showInProfile = false,
    });

    // Assert
    await HttpAssert.StatusAsync(HttpStatusCode.OK, response);
    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
    Assert.False(body.GetProperty("personalDetail").GetProperty("showInProfile").GetBoolean());

    var saved = await factory.QueryDbAsync(db =>
      db.EmployeePersonalDetails.AsNoTracking().SingleAsync(pd => pd.EmployeeId == employee.Id));
    Assert.False(saved.ShowInProfile);
  }
}