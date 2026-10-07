using MyMIS.Api.Models;
using MyMIS.Api.Helpers;

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
}