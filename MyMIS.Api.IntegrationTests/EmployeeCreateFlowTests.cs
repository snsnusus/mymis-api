using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyMIS.Api.Filters;
using MyMIS.Api.Helpers;
using MyMIS.Api.IntegrationTests.Infrastructure;
using MyMIS.Api.Models;
using static MyMIS.Api.IntegrationTests.Infrastructure.TestData;

namespace MyMIS.Api.IntegrationTests;

// The whole POST /api/Employees flow, through real HTTP and real Postgres.
[Collection(IntegrationCollection.Name)]
public class EmployeeCreateFlowTests(ApiFactory factory)
{
  private const string Password = "Temp-Password-2026!";

  // An id that no row has
  private const int Missing = 999_999;

  // One valid create request, plus the values the tests look for afterwards.
  private sealed record NewEmployee(
    Dictionary<string, object?> Body,
    string Username,
    string Email,
    string Mobile,
    string ContactName);

  // ---------------- helpers ----------------

  // Builds a complete, valid request body. It's a dictionary so a test can change or remove a part.
  // Everything unique (username, email, mobile, contact name) is generated per call.
  private static NewEmployee NewRequest(
    int barangayId, string? username = null, string? email = null, string? mobile = null)
  {
    var suffix = Unique();
    var name = username ?? $"new{suffix}";
    var emailAddress = email ?? $"new{suffix}@example.com";
    var number = mobile ?? NewMobile();
    var contactName = $"Contact{suffix}";

    var address = new { addressLine1 = "123 Mabini St.", barangayId, postalCode = "1105" };

    var body = new Dictionary<string, object?>
    {
      ["firstName"] = "Test",
      ["middleName"] = "Middle",
      ["lastName"] = $"Create{suffix}",
      ["gender"] = "MALE",
      ["maritalStatus"] = "SINGLE",
      ["employeeType"] = "Management",
      ["employmentStatus"] = "Probationary",
      ["joiningDate"] = "2026-10-01",
      ["username"] = name,
      ["password"] = Password,
      ["addresses"] = new object[] { new { type = "Present", isPrimary = true, address } },
      ["emails"] = new object[] { new { ownership = "Personal", isPrimary = true, email = emailAddress } },
      ["phones"] = new object[]
      {
        new { ownership = "Personal", isPrimary = true, phone = new { countryCode = "PH", number } },
      },
      ["emergencyContact"] = new
      {
        firstName = contactName,
        lastName = "Santos",
        relationship = "Parent",
        isPrimary = true,
        phone = new { countryCode = "PH", number = "+639171234567" },
        address,
      },
    };

    return new NewEmployee(body, name, emailAddress, number, contactName);
  }

  private async Task<int> NewBarangayIdAsync() =>
    (await factory.SeedBarangayAsync(await factory.SeedCityAsync(await factory.SeedRegionAsync()))).Id;

  private static Task<HttpResponseMessage> PostAsync(HttpClient client, NewEmployee request) =>
    client.PostAsJsonAsync("/api/Employees", request.Body);

  private Task<HttpClient> CreatorClientAsync() => factory.GetClientAsync(Role.User, "employees.create");

  private Task<Employee?> FindEmployeeAsync(string username) =>
    factory.QueryDbAsync(db =>
      db.Employees.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(e => e.Username == username));

  // How many rows exist for this request's unique values: employee, email, phone, emergency contact.
  private async Task AssertSavedCountsAsync(NewEmployee request, int employees, int emails, int phones, int contacts)
  {
    var saved = await factory.QueryDbAsync(async db => (
      Employees: await db.Employees.IgnoreQueryFilters().CountAsync(e => e.Username == request.Username),
      Emails: await db.EmployeeEmails.IgnoreQueryFilters().CountAsync(e => e.Email == request.Email),
      Phones: await db.EmployeePhones.IgnoreQueryFilters().CountAsync(p => p.Phone.Number == request.Mobile),
      Contacts: await db.EmergencyContacts.IgnoreQueryFilters().CountAsync(c => c.FirstName == request.ContactName)));

    Assert.Equal((employees, emails, phones, contacts), (saved.Employees, saved.Emails, saved.Phones, saved.Contacts));
  }

  private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
    JsonDocument.Parse(await response.Content.ReadAsStringAsync());

  private static long CodeNumber(string code) => long.Parse(code[(code.LastIndexOf('-') + 1)..]);

  // ---------------- the happy path ----------------

  [Fact]
  public async Task PostEmployee_WithEveryRequiredPart_Returns201AndSavesTheWholeGraph()
  {
    // Arrange
    var client = await CreatorClientAsync();
    var request = NewRequest(await NewBarangayIdAsync());

    // Act
    var response = await PostAsync(client, request);

    // Assert: the response
    await HttpAssert.StatusAsync(HttpStatusCode.Created, response);
    Assert.NotNull(response.Headers.Location);

    using var json = await ReadJsonAsync(response);
    var root = json.RootElement;
    Assert.Equal(request.Username, root.GetProperty("username").GetString());
    Assert.Matches(@"^MYMIS-\d{4}-\d{5,}$", root.GetProperty("employeeCode").GetString());
    Assert.Equal(1, root.GetProperty("emergencyContacts").GetArrayLength());

    // Assert: the employee row
    var employee = await FindEmployeeAsync(request.Username);
    Assert.NotNull(employee);
    Assert.True(employee.MustChangePassword);
    Assert.True(BCrypt.Net.BCrypt.Verify(Password, employee.PasswordHash));
    Assert.Equal(EmployeeType.Management, employee.EmployeeType);
    Assert.Equal(EmploymentStatus.Probationary, employee.EmploymentStatus);
    Assert.Equal(new DateOnly(2026, 10, 1), employee.JoiningDate);

    // Assert: every child row exists, and is primary because it is the first of its kind
    var parts = await factory.QueryDbAsync(async db => new
    {
      PersonalDetails = await db.EmployeePersonalDetails.CountAsync(p => p.EmployeeId == employee.Id),
      PrimaryAddresses = await db.EmployeeAddresses.CountAsync(a => a.EmployeeId == employee.Id && a.IsPrimary),
      PrimaryEmails = await db.EmployeeEmails.CountAsync(e => e.EmployeeId == employee.Id && e.IsPrimary),
      PrimaryContacts = await db.EmergencyContacts.CountAsync(c => c.EmployeeId == employee.Id && c.IsPrimary),
      Phone = await db.EmployeePhones.SingleAsync(p => p.EmployeeId == employee.Id),
    });

    Assert.Equal(1, parts.PersonalDetails);
    Assert.Equal(1, parts.PrimaryAddresses);
    Assert.Equal(1, parts.PrimaryEmails);
    Assert.Equal(1, parts.PrimaryContacts);
    Assert.True(parts.Phone.IsPrimary);

    // The server works out the line type itself; the request never says "mobile"
    Assert.Equal(PhoneLineType.Mobile, parts.Phone.LineType);
  }

  [Fact]
  public async Task PostEmployee_ThenLoggingInAsThem_SaysTheyMustChangeTheirPassword()
  {
    // Arrange
    var client = await CreatorClientAsync();
    var request = NewRequest(await NewBarangayIdAsync());
    await HttpAssert.StatusAsync(HttpStatusCode.Created, await PostAsync(client, request));

    // Act: the new employee logs in with the password HR typed
    using var anonymous = factory.CreateApiClient();
    var login = await anonymous.PostAsJsonAsync("/api/Auth/login", new { username = request.Username, password = Password });

    // Assert
    await HttpAssert.StatusAsync(HttpStatusCode.OK, login);
    using var json = await ReadJsonAsync(login);
    Assert.True(json.RootElement.GetProperty("mustChangePassword").GetBoolean());
  }

  [Fact]
  public async Task PostEmployee_Twice_GivesDifferentEmployeeCodesThatKeepGoingUp()
  {
    // Arrange
    var client = await CreatorClientAsync();
    var barangayId = await NewBarangayIdAsync();

    // Act
    var firstResponse = await PostAsync(client, NewRequest(barangayId));
    var secondResponse = await PostAsync(client, NewRequest(barangayId));

    // Assert: the codes come from a Postgres sequence. It never repeats, but a failed create
    // also uses up a number, so "goes up" is the promise, not "goes up by exactly one".
    await HttpAssert.StatusAsync(HttpStatusCode.Created, firstResponse);
    await HttpAssert.StatusAsync(HttpStatusCode.Created, secondResponse);

    using var first = await ReadJsonAsync(firstResponse);
    using var second = await ReadJsonAsync(secondResponse);
    var firstCode = first.RootElement.GetProperty("employeeCode").GetString()!;
    var secondCode = second.RootElement.GetProperty("employeeCode").GetString()!;

    Assert.NotEqual(firstCode, secondCode);
    Assert.True(CodeNumber(secondCode) > CodeNumber(firstCode));
  }

  // ---------------- conflicts: nothing from the second request is saved ----------------

  [Fact]
  public async Task PostEmployee_WithAUsernameThatIsTaken_Returns409AndSavesNothingNew()
  {
    // Arrange
    var client = await CreatorClientAsync();
    var barangayId = await NewBarangayIdAsync();
    var first = NewRequest(barangayId);
    await HttpAssert.StatusAsync(HttpStatusCode.Created, await PostAsync(client, first));


    // Act: same username, but a new email, mobile and contact name
    var second = NewRequest(barangayId, username: first.Username);
    var response = await PostAsync(client, second);

    // Assert: still one employee with that username, and none of the second request's other rows
    await HttpAssert.StatusAsync(HttpStatusCode.Conflict, response);
    using var json = await ReadJsonAsync(response);
    Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("Username", out _));
    await AssertSavedCountsAsync(second, employees: 1, emails: 0, phones: 0, contacts: 0);
  }

  [Fact]
  public async Task PostEmployee_WithAnEmailThatIsTaken_Returns409OnEmailsAndSavesNothingNew()
  {
    // Arrange
    var client = await CreatorClientAsync();
    var barangayId = await NewBarangayIdAsync();
    var first = NewRequest(barangayId);
    await HttpAssert.StatusAsync(HttpStatusCode.Created, await PostAsync(client, first));

    // Act: a new username, but the first employee's email
    var second = NewRequest(barangayId, email: first.Email);
    var response = await PostAsync(client, second);

    // Assert
    await HttpAssert.StatusAsync(HttpStatusCode.Conflict, response);
    using var json = await ReadJsonAsync(response);
    Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("Emails", out _));

    await AssertSavedCountsAsync(second, employees: 0, emails: 1, phones: 0, contacts: 0);
  }

  [Fact]
  public async Task PostEmployee_WithAMobileThatIsTaken_Returns409OnPhonesAndSavesNothingNew()
  {
    // Arrange
    var client = await CreatorClientAsync();
    var barangayId = await NewBarangayIdAsync();
    var first = NewRequest(barangayId);
    await HttpAssert.StatusAsync(HttpStatusCode.Created, await PostAsync(client, first));

    // Act: a new username and email, but the first employee's mobile
    var second = NewRequest(barangayId, mobile: first.Mobile);
    var response = await PostAsync(client, second);

    // Assert
    await HttpAssert.StatusAsync(HttpStatusCode.Conflict, response);
    using var json = await ReadJsonAsync(response);
    Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("Phones", out _));

    await AssertSavedCountsAsync(second, employees: 0, emails: 0, phones: 1, contacts: 0);
  }

  // ---------------- failures that only the database can see: everything rolls back ----------------

  [Fact]
  public async Task PostEmployee_WithADepartmentThatDoesNotExist_Returns400AndRollsEverythingBack()
  {
    // Arrange: every pre-check passes. Only the database knows the department is missing,
    // and only at save time, after the employee, address, email, phone and contact are queued.
    var client = await CreatorClientAsync();
    var request = NewRequest(await NewBarangayIdAsync());
    request.Body["departmentId"] = Missing;

    // Act
    var response = await PostAsync(client, request);

    // Assert
    await HttpAssert.StatusAsync(HttpStatusCode.BadRequest, response);
    using var json = await ReadJsonAsync(response);
    Assert.Equal(
      ForeignKeyViolationExceptionFilter.MissingReferenceMessage,
      json.RootElement.GetProperty("message").GetString());

    await AssertSavedCountsAsync(request, employees: 0, emails: 0, phones: 0, contacts: 0);
  }

  [Fact]
  public async Task PostEmployee_WithAPositionThatDoesNotExist_Returns400AndRollsEverythingBack()
  {
    // Arrange
    var client = await CreatorClientAsync();
    var request = NewRequest(await NewBarangayIdAsync());
    request.Body["positionId"] = Missing;

    // Act
    var response = await PostAsync(client, request);

    // Assert
    await HttpAssert.StatusAsync(HttpStatusCode.BadRequest, response);
    await AssertSavedCountsAsync(request, employees: 0, emails: 0, phones: 0, contacts: 0);
  }

  [Fact]
  public async Task Employees_SavedTogetherWithTheirContactDetails_RollBackTogetherWhenOnePartFails()
  {
    // Arrange: a mobile number that another employee already owns
    var owner = await factory.SeedEmployeeAsync();
    var number = $"+6391{Random.Shared.Next(10_000_000, 99_999_999)}";

    await factory.ExecuteDbAsync(async db =>
    {
      db.EmployeePhones.Add(new EmployeePhone
      {
        EmployeeId = owner.Id,
        Ownership = ContactOwnership.Personal,
        LineType = PhoneLineType.Mobile,
        Phone = new Phone { CountryCode = "PH", Number = number },
        IsPrimary = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
      });
      await db.SaveChangesAsync();
    });

    var username = $"new{Unique()}";
    var email = $"{username}@example.com";

    // Act: one employee, one email that is fine, and one phone that collides, saved in a single SaveChanges
    var exception = await Assert.ThrowsAsync<DbUpdateException>(() => factory.ExecuteDbAsync(async db =>
    {
      db.Employees.Add(new Employee
      {
        FirstName = "Atomic",
        LastName = "Test",
        Gender = "MALE",
        MaritalStatus = "SINGLE",
        EmployeeCode = $"IT-{Unique()}",
        Username = username,
        PasswordHash = "irrelevant",
        Emails =
        [
          new EmployeeEmail
          {
            Ownership = ContactOwnership.Personal,
            Email = email,
            IsPrimary = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
          },
        ],
        Phones =
        [
          new EmployeePhone
          {
            Ownership = ContactOwnership.Personal,
            LineType = PhoneLineType.Mobile,
            Phone = new Phone { CountryCode = "PH", Number = number },
            IsPrimary = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
          },
        ],
      });

      await db.SaveChangesAsync();
    }));

    // Assert: the failing phone took the employee and the harmless email down with it
    Assert.Equal("IX_EmployeePhones_Phone_Number_Mobile", exception.GetUniqueViolationConstraint());

    var left = await factory.QueryDbAsync(async db => (
      Employees: await db.Employees.IgnoreQueryFilters().CountAsync(e => e.Username == username),
      Emails: await db.EmployeeEmails.IgnoreQueryFilters().CountAsync(e => e.Email == email)));

    Assert.Equal((0, 0), (left.Employees, left.Emails));
  }

  // ---------------- requests the controller rejects before anything is saved ----------------

  [Theory]
  [InlineData("emails")]
  [InlineData("phones")]
  [InlineData("addresses")]
  [InlineData("emergencyContact")]
  public async Task PostEmployee_MissingARequiredPart_Returns400NamingItAndSavesNothing(string part)
  {
    // Arrange
    var client = await CreatorClientAsync();
    var request = NewRequest(await NewBarangayIdAsync());
    request.Body.Remove(part);

    // Act
    var response = await PostAsync(client, request);

    // Assert
    await HttpAssert.StatusAsync(HttpStatusCode.BadRequest, response);
    using var json = await ReadJsonAsync(response);
    var errors = json.RootElement.GetProperty("errors");
    Assert.Contains(errors.EnumerateObject(), e => string.Equals(e.Name, part, StringComparison.OrdinalIgnoreCase));

    await AssertSavedCountsAsync(request, employees: 0, emails: 0, phones: 0, contacts: 0);
  }

  [Fact]
  public async Task PostEmployee_WithAnAddressInABarangayThatDoesNotExist_Returns400OnAddresses()
  {
    // Arrange: unlike the department, the controller checks address barangays itself, before saving
    var client = await CreatorClientAsync();
    var request = NewRequest(await NewBarangayIdAsync());
    request.Body["addresses"] = new object[]
    {
      new
      {
        type = "Present",
        isPrimary = true,
        address = new { addressLine1 = "123 Mabini St.", barangayId = Missing, postalCode = "1105" },
      },
    };

    // Act
    var response = await PostAsync(client, request);

    // Assert
    await HttpAssert.StatusAsync(HttpStatusCode.BadRequest, response);
    using var json = await ReadJsonAsync(response);
    Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("Addresses", out _));

    await AssertSavedCountsAsync(request, employees: 0, emails: 0, phones: 0, contacts: 0);
  }
}