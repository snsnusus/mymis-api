using System.ComponentModel.DataAnnotations;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;

namespace MyMIS.Api.Tests.DTOs;

public class EmergencyContactCreateDtoTests
{
  private static EmergencyContactCreateDto ValidDto() => new()
  {
    FirstName = "Jose",
    LastName = "Reyes",
    Relationship = EmergencyContactRelationship.Spouse,
    Phone = new PhoneDto { CountryCode = "PH", Number = "+639171234567" },
    Address = new AddressDto
    {
      AddressLine1 = "123 Mabini St.",
      BarangayId = 1,
      PostalCode = "1100",
    },
  };

  private static List<ValidationResult> Validate(EmergencyContactCreateDto dto)
  {
    var results = new List<ValidationResult>();
    Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
    return results;
  }

  [Fact]
  public void Validate_WithAddress_HasNoErrors()
  {
    // Act
    var results = Validate(ValidDto());

    // Assert
    Assert.Empty(results);
  }

  [Fact]
  public void Validate_WithoutAddress_ReportsAddressRequired()
  {
    // Arrange
    var dto = ValidDto();
    dto.Address = null;

    // Act
    var results = Validate(dto);

    // Assert
    var error = Assert.Single(results);
    Assert.Contains(nameof(EmergencyContactCreateDto.Address), error.MemberNames);
  }
}