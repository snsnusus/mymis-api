using MyMIS.Api.Models;
using MyMIS.Api.Helpers;
using MyMIS.Api.DTOs;

namespace MyMIS.Api.Tests.Helpers;

public class PhoneFormatterTests
{
  [Theory]
  [InlineData("+639171234567", "PH", PhoneLineType.Mobile)]
  [InlineData("+63281234567", "PH", PhoneLineType.Landline)]
  [InlineData("+12025550123", "US", PhoneLineType.Mobile)]
  public void DetectLineType_KnownNumber_ReturnsExpectedLineType(string number, string countryCode, PhoneLineType expected)
  {
    // Arrange
    var phone = new Phone
    {
      CountryCode = countryCode,
      Number = number,
    };

    // Act
    var detectedLineType = PhoneFormatter.DetectLineType(phone);

    // Assert
    Assert.Equal(expected, detectedLineType);
  }

  [Theory]
  [InlineData("PH", "+639171234567", "PH", "+639171234567")]
  [InlineData(" ph ", " +639171234567 ", "PH", "+639171234567")]
  [InlineData("PH", "09171234567", "PH", "+639171234567")]
  public void TryNormalize_ValidNumber_ReturnsNormalizedPhone(string inputCode, string inputNumber, string expectedCode, string expectedNumber)
  {
    // Arrange
    var dto = new PhoneDto
    {
      CountryCode = inputCode,
      Number = inputNumber,
    };

    // Act
    var result = PhoneFormatter.TryNormalize(dto);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(expectedCode, result.CountryCode);
    Assert.Equal(expectedNumber, result.Number);
  }

  [Theory]
  [InlineData("PH", "+63917123")]
  [InlineData("PH", "+971501234567")]
  public void TryNormalize_NumberInvalidForCountry_ReturnsNull(string inputCode, string inputNumber)
  {
    // Arrange
    var dto = new PhoneDto
    {
      CountryCode = inputCode,
      Number = inputNumber,
    };

    // Act
    var result = PhoneFormatter.TryNormalize(dto);

    // Assert
    Assert.Null(result);
  }

  [Theory]
  [InlineData("PH", "abc")]
  [InlineData("PH", "")]
  public void TryNormalize_Unparseable_ReturnsNull(string inputCode, string inputNumber)
  {
    // Arrange
    var dto = new PhoneDto
    {
      CountryCode = inputCode,
      Number = inputNumber,
    };

    // Act
    var result = PhoneFormatter.TryNormalize(dto);

    // Assert
    Assert.Null(result);
  }

  [Fact]
  public async Task ToResponse_PhMobileNumber_ReturnsAllDisplayFormats()
  {
    // Arrange
    var phone = new Phone { CountryCode = "PH", Number = "+639171234567" };

    // Act
    var result = PhoneFormatter.ToResponse(phone);

    // Assert
    Assert.Equal("PH", result.CountryCode);
    Assert.Equal("+63", result.DialCode);
    Assert.Equal("+639171234567", result.International);
    Assert.Equal("09171234567", result.Local);
    Assert.Equal("(+63) 917 123 4567", result.Formatted);
  }
}