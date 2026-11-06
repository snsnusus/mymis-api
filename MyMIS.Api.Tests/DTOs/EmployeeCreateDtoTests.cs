using System.ComponentModel.DataAnnotations;
using MyMIS.Api.Authorization;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;

namespace MyMIS.Api.Tests.DTOs;

public class EmployeeCreateDtoTests
{
  [Fact]
  public void Validate_EmptyAddressList_ReportsAddressesError()
  {
    // Arrange
    var dto = new EmployeeCreateDto { Addresses = [] };
    var results = new List<ValidationResult>();

    // Act
    Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

    // Assert
    Assert.Contains(results, r => r.MemberNames.Contains(nameof(EmployeeCreateDto.Addresses)));
  }

  [Fact]
  public void Validate_EmptyPhoneList_ReportsPhonesError()
  {
    // Arrange: [] passes [Required] (it isn't null) but fails [MinLength(1)]
    var dto = new EmployeeCreateDto { Phones = [] };
    var results = new List<ValidationResult>();

    // Act
    Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

    // Assert: other required fields fail too; we only care that Phones is among them
    Assert.Contains(results, r => r.MemberNames.Contains(nameof(EmployeeCreateDto.Phones)));
  }

  [Fact]
  public void Validate_MissingPhones_ReportsPhonesError()
  {
    // Arrange: Phones left null, i.e. the client omitted it entirely
    var dto = new EmployeeCreateDto();
    var results = new List<ValidationResult>();

    // Act
    Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

    // Assert
    Assert.Contains(results, r => r.MemberNames.Contains(nameof(EmployeeCreateDto.Phones)));
  }

  [Fact]
  public void Validate_EmptyEmailList_ReportsEmailsError()
  {
    // Arrange: [] passes [Required] (it isn't null) but fails [MinLength(1)]
    var dto = new EmployeeCreateDto { Emails = [] };
    var results = new List<ValidationResult>();

    // Act
    Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

    // Assert
    Assert.Contains(results, r => r.MemberNames.Contains(nameof(EmployeeCreateDto.Emails)));
  }

  [Fact]
  public void Validate_MissingEmails_ReportsEmailsError()
  {
    // Arrange: Emails left null, i.e. the client omitted it entirely
    var dto = new EmployeeCreateDto();
    var results = new List<ValidationResult>();

    // Act
    Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

    // Assert
    Assert.Contains(results, r => r.MemberNames.Contains(nameof(EmployeeCreateDto.Emails)));
  }

  private static EmployeeCreateDto ValidDto() => new()
  {
    FirstName = "Juan",
    MiddleName = "Santos",
    LastName = "Dela Cruz",
    Gender = "Male",
    MaritalStatus = "Single",
    EmployeeType = EmployeeType.Management,
    EmploymentStatus = EmploymentStatus.Regular,
    JoiningDate = new DateOnly(2026, 11, 2),
    Username = "jdelacruz",
    Password = "Temp-pass-123",
    EmergencyContact = new EmergencyContactCreateDto(),
    Addresses = [new EmployeeAddressCreateDto()],
    Emails = [new EmployeeEmailCreateDto()],
    Phones = [new EmployeePhoneCreateDto()],
  };

  private static CustomRoleCreateDto ValidCustomRole() => new()
  {
    Name = "Team Lead - Finance",
    Permissions = [PermissionNames.Employees.Read],
  };

  private static List<ValidationResult> Validate(EmployeeCreateDto dto)
  {
    var results = new List<ValidationResult>();
    Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
    return results;
  }

  [Fact]
  public void Validate_ValidDto_HasNoErrors()
  {
    Assert.Empty(Validate(ValidDto()));
  }

  [Fact]
  public void Validate_RoleIdAndCustomRole_ReportsCustomRoleError()
  {
    var dto = ValidDto();
    dto.RoleId = 5;
    dto.CustomRole = ValidCustomRole();

    var error = Assert.Single(Validate(dto));
    Assert.Contains(nameof(EmployeeCreateDto.CustomRole), error.MemberNames);
  }

  [Fact]
  public void Validate_OnlyCustomRole_HasNoErrors()
  {
    var dto = ValidDto();
    dto.CustomRole = ValidCustomRole();

    Assert.Empty(Validate(dto));
  }

  [Fact]
  public void Validate_OnlyRoleId_HasNoErrors()
  {
    var dto = ValidDto();
    dto.RoleId = 5;

    Assert.Empty(Validate(dto));
  }
}