using System.ComponentModel.DataAnnotations;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;

namespace MyMIS.Api.Tests.DTOs;

// Checks the attributes on EmployeeEmailCreateDto. This is the same check
// [ApiController] runs before an action executes.
public class EmployeeEmailCreateDtoTests
{
  private static EmployeeEmailCreateDto ValidDto() => new()
  {
    Ownership = ContactOwnership.Personal,
    Email = "juan@example.com",
  };

  private static List<ValidationResult> Validate(EmployeeEmailCreateDto dto)
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
    // Arrange: null is what a JSON body without "ownership" binds to
    var dto = ValidDto();
    dto.Ownership = null;

    // Act
    var results = Validate(dto);

    // Assert
    var error = Assert.Single(results);
    Assert.Contains(nameof(EmployeeEmailCreateDto.Ownership), error.MemberNames);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  public void Validate_MissingOrEmptyEmail_ReportsEmailError(string? email)
  {
    // Arrange
    var dto = ValidDto();
    dto.Email = email;

    // Act
    var results = Validate(dto);

    // Assert
    Assert.Contains(results, r => r.MemberNames.Contains(nameof(EmployeeEmailCreateDto.Email)));
  }

  [Fact]
  public void Validate_EmailLongerThan254Characters_ReportsEmailError()
  {
    // Arrange: 256 characters, otherwise a well-formed address
    var dto = ValidDto();
    dto.Email = new string('a', 250) + "@x.com";

    // Act
    var results = Validate(dto);

    // Assert
    Assert.Contains(results, r => r.MemberNames.Contains(nameof(EmployeeEmailCreateDto.Email)));
  }

  [Fact]
  public void Validate_EmailWithoutAtSign_ReportsEmailError()
  {
    // Arrange
    var dto = ValidDto();
    dto.Email = "jdoe";

    // Act
    var results = Validate(dto);

    // Assert
    Assert.Contains(results, r => r.MemberNames.Contains(nameof(EmployeeEmailCreateDto.Email)));
  }
}