using Microsoft.EntityFrameworkCore;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;

namespace MyMIS.Api.Tests.Services.EmployeeServiceTests;

public class CreateAsyncTests : EmployeeServiceTestBase
{
  [Fact]
  public async Task CreateAsync_WithEmergencyContact_SavesItForTheNewEmployee()
  {
    // Arrange
    var dto = new EmployeeCreateDto
    {
      FirstName = "Maria",
      LastName = "Reyes",
      Gender = "Female",
      MaritalStatus = "Married",
      Username = "mreyes",
      Password = "TempPass2026!",
      EmergencyContact = NewContactDto(),
    };

    // Act
    var result = await _service.CreateAsync(dto, 1);

    // Assert: in the database...
    var contacts = await _context.EmergencyContacts
      .AsNoTracking()
      .Where(c => c.EmployeeId == result.Id)
      .ToListAsync();

    var contact = Assert.Single(contacts);
    Assert.Equal("Jose", contact.FirstName);
    Assert.Equal("+639171234567", contact.Phone.Number);
    Assert.True(contact.IsPrimary);

    // ...and in the response
    var returned = Assert.Single(result.EmergencyContacts!);
    Assert.Equal("Jose", returned.FirstName);
    Assert.True(returned.IsPrimary);
  }

  [Fact]
  public async Task CreateAsync_ContactNotMarkedPrimary_StillSavesItAsPrimary()
  {
    // Arrange
    var dto = new EmployeeCreateDto
    {
      FirstName = "Maria",
      LastName = "Reyes",
      Gender = "Female",
      MaritalStatus = "Married",
      Username = "mreyes",
      Password = "TempPass2026!",
      EmergencyContact = NewContactDto(isPrimary: false),
    };

    // Act
    var result = await _service.CreateAsync(dto, 1);

    // Assert
    var contact = await _context.EmergencyContacts
      .AsNoTracking()
      .SingleAsync(c => c.EmployeeId == result.Id);
    Assert.True(contact.IsPrimary);
  }

  [Fact]
  public async Task CreateAsync_HashesPassword_NeverStoresPlainText()
  {
    // Arrange
    var dto = new EmployeeCreateDto
    {
      FirstName = "Jane",
      LastName = "Doe",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "jdoe",
      Password = "SuperSecret123"
    };

    // Act
    await _service.CreateAsync(dto, 1);

    // Assert
    var savedEmployee = await _context.Employees.FirstAsync(e => e.Username == "jdoe");

    Assert.NotEqual(dto.Password, savedEmployee.PasswordHash);
    Assert.True(BCrypt.Net.BCrypt.Verify(dto.Password, savedEmployee.PasswordHash));
  }

  [Fact]
  public async Task CreateAsync_PositionIdProvided_PersistsAndReturnsNestedPosition()
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

    var dto = new EmployeeCreateDto
    {
      FirstName = "John",
      MiddleName = "Conor",
      LastName = "Doe",
      Gender = "MALE",
      MaritalStatus = "SINGLE",
      Username = "johndoe",
      Password = "irrelevant-to-this-test",
      DepartmentId = department.Id,
      PositionId = position.Id
    };

    // Act
    var result = await _service.CreateAsync(dto, 1);

    // Assert
    Assert.NotNull(result.Position);
    Assert.Equal(position.Id, result.Position.Id);
    Assert.Equal("Manager", result.Position.Title);

    var savedEmployee = await _context.Employees.FirstAsync(e => e.Id == result.Id);
    Assert.Equal(position.Id, savedEmployee.PositionId);
  }

  [Fact]
  public async Task CreateAsync_WithTwoAddresses_SavesBothWithExactlyOnePrimary()
  {
    // Arrange
    var barangay = await SeedBarangayAsync();
    var dto = new EmployeeCreateDto
    {
      FirstName = "Maria",
      LastName = "Reyes",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "mreyes",
      Password = "TempPass2026!",
      Addresses =
      [
        NewAddressDto(AddressType.Present, barangay.Id),
        NewAddressDto(AddressType.Permanent, barangay.Id, isPrimary: true),
      ],
    };

    // Act
    var result = await _service.CreateAsync(dto, 1);

    // Assert
    var saved = await _context.EmployeeAddresses
        .AsNoTracking()
        .Where(a => a.EmployeeId == result.Id)
        .ToListAsync();

    Assert.Equal(2, saved.Count);
    var primary = Assert.Single(saved, a => a.IsPrimary);
    Assert.Equal(AddressType.Permanent, primary.Type);
  }

  [Fact]
  public async Task CreateAsync_WithMobileAndLandline_SavesBothWithExactlyOnePrimary()
  {
    // Arrange: the landline (second) is the one marked primary
    var dto = new EmployeeCreateDto
    {
      FirstName = "Maria",
      LastName = "Reyes",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "mreyes",
      Password = "TempPass2026!",
      Phones =
      [
        NewPhoneDto("+639171234567"),
        NewPhoneDto("+63281234567", isPrimary: true),
      ],
    };

    // Act
    var result = await _service.CreateAsync(dto, 1);

    // Assert: both saved through the employee.Phones navigation (EF filled in
    // EmployeeId), line types detected, and only the landline is primary
    var saved = await _context.EmployeePhones
        .AsNoTracking()
        .Where(p => p.EmployeeId == result.Id)
        .ToListAsync();

    Assert.Equal(2, saved.Count);
    var primary = Assert.Single(saved, p => p.IsPrimary);
    Assert.Equal(PhoneLineType.Landline, primary.LineType);
    Assert.Contains(saved, p => p.LineType == PhoneLineType.Mobile && !p.IsPrimary);
  }

  [Fact]
  public async Task CreateAsync_WithoutPhones_StillCreatesEmployee()
  {
    // Arrange: like addresses, the "at least one phone" rule lives in the DTO
    // and controller, so the service itself must not assume phones are present.
    var dto = new EmployeeCreateDto
    {
      FirstName = "Maria",
      LastName = "Reyes",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "mreyes",
      Password = "TempPass2026!",
    };

    // Act
    var result = await _service.CreateAsync(dto, 1);

    // Assert
    Assert.True(result.Id > 0);
    Assert.Empty(await _context.EmployeePhones.Where(p => p.EmployeeId == result.Id).ToListAsync());
  }

  [Fact]
  public async Task CreateAsync_WithoutAddresses_StillCreatesEmployee()
  {
    // Arrange: the "at least one" rule lives in the DTO's validation, which
    // only runs on HTTP requests; the service itself must not assume it.
    var dto = new EmployeeCreateDto
    {
      FirstName = "Maria",
      LastName = "Reyes",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "mreyes",
      Password = "TempPass2026!",
    };

    // Act
    var result = await _service.CreateAsync(dto, 1);

    // Assert
    Assert.True(result.Id > 0);
    Assert.Empty(await _context.EmployeeAddresses.Where(a => a.EmployeeId == result.Id).ToListAsync());
  }

  [Fact]
  public async Task CreateAsync_PositionIdOmitted_CreatesEmployeeWithNullPosition()
  {
    // Arrange
    var dto = new EmployeeCreateDto
    {
      FirstName = "John",
      MiddleName = "Conor",
      LastName = "Doe",
      Gender = "MALE",
      MaritalStatus = "SINGLE",
      Username = "johndoe",
      Password = "irrelevant-to-this-test",
    };

    // Act
    var result = await _service.CreateAsync(dto, 1);

    // Assert
    Assert.Null(result.Position);
  }

  [Fact]
  public async Task CreateAsync_AvatarStyleProvided_PersistsAndReturnsStyle()
  {
    // Arrange
    var dto = new EmployeeCreateDto
    {
      FirstName = "Jane",
      LastName = "Doe",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "jdoe",
      Password = "irrelevant-to-this-test",
      AvatarStyle = AvatarStyle.Constellation,
    };

    // Act
    var result = await _service.CreateAsync(dto, 1);

    // Assert: returned in the response AND saved in the database
    Assert.Equal(AvatarStyle.Constellation, result.AvatarStyle);

    var saved = await _context.Employees
      .AsNoTracking()
      .FirstAsync(e => e.Id == result.Id);
    Assert.Equal(AvatarStyle.Constellation, saved.AvatarStyle);
  }

  [Fact]
  public async Task CreateAsync_AvatarStyleOmitted_StoresNull()
  {
    // Arrange
    var dto = new EmployeeCreateDto
    {
      FirstName = "Jane",
      LastName = "Doe",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "jdoe",
      Password = "irrelevant-to-this-test",
    };

    // Act
    var result = await _service.CreateAsync(dto, 1);

    // Assert: not defaulted to the first enum member
    Assert.Null(result.AvatarStyle);
  }

  [Fact]
  public async Task CreateAsync_UsernameWithUppercaseAndWhitespace_StoresNormalized()
  {
    // Arrange
    var dto = new EmployeeCreateDto
    {
      FirstName = "Jane",
      LastName = "Doe",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "  JDoe  ",
      Password = "irrelevant-to-this-test",
    };

    // Act
    var result = await _service.CreateAsync(dto, 1);

    // Assert: normalized in the response AND in the database
    Assert.Equal("jdoe", result.Username);

    var saved = await _context.Employees
      .AsNoTracking()
      .FirstAsync(e => e.Id == result.Id);
    Assert.Equal("jdoe", saved.Username);
  }

  [Fact]
  public async Task CreateAsync_EmploymentFieldsProvided_PersistsAndReturnsThem()
  {
    // Arrange
    var dto = new EmployeeCreateDto
    {
      FirstName = "Maria",
      LastName = "Reyes",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "mreyes",
      Password = "irrelevant-to-this-test",
      EmployeeType = EmployeeType.Client,
      EmploymentStatus = EmploymentStatus.Probationary,
      JoiningDate = new DateOnly(2026, 10, 5),
    };

    // Act
    var result = await _service.CreateAsync(dto, 1);

    // Assert: in the response...
    Assert.Equal(EmployeeType.Client, result.EmployeeType);
    Assert.Equal(EmploymentStatus.Probationary, result.EmploymentStatus);
    Assert.Equal(new DateOnly(2026, 10, 5), result.JoiningDate);

    // ...and in the database
    var saved = await _context.Employees
      .AsNoTracking()
      .FirstAsync(e => e.Id == result.Id);
    Assert.Equal(EmployeeType.Client, saved.EmployeeType);
    Assert.Equal(EmploymentStatus.Probationary, saved.EmploymentStatus);
    Assert.Equal(new DateOnly(2026, 10, 5), saved.JoiningDate);
  }

  [Fact]
  public async Task CreateAsync_NewEmployee_UsesGeneratedEmployeeCode()
  {
    // Arrange
    var dto = new EmployeeCreateDto
    {
      FirstName = "Maria",
      LastName = "Reyes",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "mreyes",
      Password = "irrelevant-to-this-test",
    };

    // Act
    var result = await _service.CreateAsync(dto, 1);

    // Assert: the code comes from the generator, in the response and the database
    Assert.Equal("MYMIS-TEST-00001", result.EmployeeCode);

    var saved = await _context.Employees
      .AsNoTracking()
      .FirstAsync(e => e.Id == result.Id);
    Assert.Equal("MYMIS-TEST-00001", saved.EmployeeCode);
  }

  [Fact]
  public async Task CreateAsync_NewEmployee_SetsMustChangePassword()
  {
    // Arrange
    var dto = new EmployeeCreateDto
    {
      FirstName = "Maria",
      LastName = "Reyes",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "mreyes",
      Password = "TempPass2026!",
    };

    // Act
    var result = await _service.CreateAsync(dto, 1);

    // Assert
    var saved = await _context.Employees.AsNoTracking().FirstAsync(e => e.Id == result.Id);
    Assert.True(saved.MustChangePassword);
  }

  [Fact]
  public async Task CreateAsync_WithEmailsNoneMarkedPrimary_SavesBothNormalizedWithFirstPrimary()
  {
    // Arrange
    var dto = new EmployeeCreateDto
    {
      FirstName = "Maria",
      LastName = "Reyes",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "mreyes",
      Password = "TempPass2026!",
      Emails =
      [
        NewEmailDto("  Maria@Example.com "),
      NewEmailDto("maria.work@example.com"),
    ],
    };

    // Act
    var result = await _service.CreateAsync(dto, 1);

    // Assert: both saved through the employee.Emails navigation, lowercased,
    // and only the first one is primary
    var saved = await _context.EmployeeEmails
        .AsNoTracking()
        .Where(e => e.EmployeeId == result.Id)
        .ToListAsync();

    Assert.Equal(2, saved.Count);
    var primary = Assert.Single(saved, e => e.IsPrimary);
    Assert.Equal("maria@example.com", primary.Email);
    Assert.Contains(saved, e => e.Email == "maria.work@example.com" && !e.IsPrimary);
  }

  [Fact]
  public async Task CreateAsync_WithSecondEmailMarkedPrimary_OnlyTheSecondIsPrimary()
  {
    // Arrange
    var dto = new EmployeeCreateDto
    {
      FirstName = "Maria",
      LastName = "Reyes",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "mreyes",
      Password = "TempPass2026!",
      Emails =
      [
        NewEmailDto("maria@example.com"),
      NewEmailDto("maria.work@example.com", isPrimary: true),
    ],
    };

    // Act
    var result = await _service.CreateAsync(dto, 1);

    // Assert
    var saved = await _context.EmployeeEmails
        .AsNoTracking()
        .Where(e => e.EmployeeId == result.Id)
        .ToListAsync();

    var primary = Assert.Single(saved, e => e.IsPrimary);
    Assert.Equal("maria.work@example.com", primary.Email);
  }

  [Fact]
  public async Task CreateAsync_WithoutEmails_StillCreatesEmployee()
  {
    // Arrange: the "at least one email" rule lives in the DTO and controller,
    // so the service itself must not assume emails are present.
    var dto = new EmployeeCreateDto
    {
      FirstName = "Maria",
      LastName = "Reyes",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "mreyes",
      Password = "TempPass2026!",
    };

    // Act
    var result = await _service.CreateAsync(dto, 1);

    // Assert
    Assert.True(result.Id > 0);
    Assert.Empty(await _context.EmployeeEmails.Where(e => e.EmployeeId == result.Id).ToListAsync());
  }

  // ---------- ShowInProfile (Step 9) ----------

  [Fact]
  public async Task CreateAsync_NoPersonalDetailSent_PersonalDetailIsVisible()
  {
    // Arrange: no PersonalDetail object, so dto.PersonalDetail?.ShowInProfile is null
    var dto = new EmployeeCreateDto
    {
      FirstName = "Jane",
      LastName = "Doe",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "jdoe",
      Password = "irrelevant-to-this-test",
    };

    // Act
    var result = await _service.CreateAsync(dto, 1);

    // Assert
    Assert.True(result.PersonalDetail!.ShowInProfile);
    var saved = await _context.EmployeePersonalDetails
      .AsNoTracking()
      .SingleAsync(pd => pd.EmployeeId == result.Id);
    Assert.True(saved.ShowInProfile);
  }

  [Fact]
  public async Task CreateAsync_PersonalDetailHidden_SavesHidden()
  {
    // Arrange
    var dto = new EmployeeCreateDto
    {
      FirstName = "Jane",
      LastName = "Doe",
      Gender = "Female",
      MaritalStatus = "Single",
      Username = "jdoe",
      Password = "irrelevant-to-this-test",
      PersonalDetail = new EmployeePersonalDetailCreateDto { ShowInProfile = false },
    };

    // Act
    var result = await _service.CreateAsync(dto, 1);

    // Assert
    Assert.False(result.PersonalDetail!.ShowInProfile);
    var saved = await _context.EmployeePersonalDetails
      .AsNoTracking()
      .SingleAsync(pd => pd.EmployeeId == result.Id);
    Assert.False(saved.ShowInProfile);
  }
}