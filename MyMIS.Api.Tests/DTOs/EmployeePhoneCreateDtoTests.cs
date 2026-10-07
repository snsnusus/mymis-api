using System.ComponentModel.DataAnnotations;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;

namespace MyMIS.Api.Tests.DTOs;

// Checks the attributes on EmployeePhoneCreateDto ([Required] on the nullable
// Ownership and Phone). This is the same check [ApiController] runs before an
// action executes.
public class EmployeePhoneCreateDtoTests
{
  private static EmployeePhoneCreateDto ValidDto() => new()
  {
    Ownership = PhoneOwnership.Personal,
    Phone = new PhoneDto { CountryCode = "PH", Number = "+639171234567" },
  };

  private static List<ValidationResult> Validate(EmployeePhoneCreateDto dto)
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
  public void Validate_MissingOwnership_ReportsOwnershipRequired()
  {
    // Arrange: null is what a JSON body without "ownership" binds to.
    // This is why Ownership is PhoneOwnership? and not PhoneOwnership: a
    // non-nullable enum would silently default to Corporate and pass.
    var dto = ValidDto();
    dto.Ownership = null;

    // Act
    var results = Validate(dto);

    // Assert
    var error = Assert.Single(results);
    Assert.Contains(nameof(EmployeePhoneCreateDto.Ownership), error.MemberNames);
  }

  [Fact]
  public void Validate_MissingPhone_ReportsPhoneRequired()
  {
    // Arrange
    var dto = ValidDto();
    dto.Phone = null;

    // Act
    var results = Validate(dto);

    // Assert
    var error = Assert.Single(results);
    Assert.Contains(nameof(EmployeePhoneCreateDto.Phone), error.MemberNames);
  }
}