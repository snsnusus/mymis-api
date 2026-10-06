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
}