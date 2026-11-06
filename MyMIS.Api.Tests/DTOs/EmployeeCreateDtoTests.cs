using System.ComponentModel.DataAnnotations;
using MyMIS.Api.DTOs;

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
}