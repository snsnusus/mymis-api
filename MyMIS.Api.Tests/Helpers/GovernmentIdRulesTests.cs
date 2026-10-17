using MyMIS.Api.Helpers;

namespace MyMIS.Api.Tests.Helpers;

public class GovernmentIdRulesTests
{
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("    ")]
  [InlineData("\t\n")]
  public void Normalize_NullOrWhiteSpace_ReturnsNull(string? input)
  {
    // Arrange + Act
    var result = GovernmentIdRules.Normalize(input);

    // Assert
    Assert.Null(result);
  }

  [Theory]
  [InlineData("12-3456789-0", "1234567890")]
  [InlineData("12-345678901-2", "123456789012")]
  [InlineData("1234-5678-9012", "123456789012")]
  [InlineData("123-456-789-000", "123456789000")]
  public void Normalize_DashedInput_StripsDashes(string input, string expected)
  {
    // Arrange + Act
    var result = GovernmentIdRules.Normalize(input);

    // Assert
    Assert.Equal(expected, result);
  }


  [Theory]
  [InlineData("12 3456789 0", "1234567890")]
  [InlineData("12 345678901 2", "123456789012")]
  [InlineData("1234 5678 9012", "123456789012")]
  [InlineData("123 456 789 000", "123456789000")]
  public void Normalize_WhiteSpacedInput_StripsSpaces(string input, string expected)
  {
    // Arrange + Act
    var result = GovernmentIdRules.Normalize(input);

    // Assert
    Assert.Equal(expected, result);
  }

  [Theory]
  [InlineData("1234567890", "1234567890")]
  [InlineData("123456789012", "123456789012")]
  [InlineData("123456789000", "123456789000")]
  public void Normalize_AlreadyPlainInput_ReturnsUnchanged(string input, string expected)
  {
    // Arrange + Act
    var result = GovernmentIdRules.Normalize(input);

    // Assert
    Assert.Equal(expected, result);
  }

  [Theory]
  [InlineData("12ABC123", "12ABC123")]
  [InlineData("ABC12", "ABC12")]
  [InlineData("12-AB 34", "12AB34")]   // dash and space stripped, letters kept
  public void Normalize_NonDigitCharacters_KeepsThem(string input, string expected)
  {
    // Arrange + Act
    var result = GovernmentIdRules.Normalize(input);

    // Assert
    Assert.Equal(expected, result);
  }

  [Theory]
  [InlineData("1234567890")]
  [InlineData("0000000000")]
  public void IsValidSss_TenDigitInput_ReturnsTrue(string input)
  {
    // Arrange + Act
    var result = GovernmentIdRules.IsValidSss(input);

    // Assert
    Assert.True(result);
  }

  [Theory]
  [InlineData("123456789")]      // 9 digits: too short
  [InlineData("12345678901")]    // 11 digits: too long
  [InlineData("12345A7890")]     // 10 chars, contains a letter
  [InlineData("12345678-0")]     // 10 chars, contains a dash
  [InlineData("12-3456789-0")]   // un-normalized SSS format
  [InlineData("")]               // empty
  [InlineData("١٢٣٤٥٦٧٨٩٠")]     // 10 Arabic-Indic digits, not ASCII 0-9
  public void IsValidSss_InvalidInput_ReturnsFalse(string input)
  {
    // Arrange + Act
    var result = GovernmentIdRules.IsValidSss(input);

    // Assert
    Assert.False(result);
  }

  [Theory]
  [InlineData("123456789000")]
  [InlineData("000000000000")]
  public void IsValidPhilHealth_TwelveDigitInput_ReturnsTrue(string input)
  {
    // Arrange + Act
    var result = GovernmentIdRules.IsValidPhilHealth(input);

    // Assert
    Assert.True(result);
  }

  [Theory]
  [InlineData("12345678900")]      // 11 digits: too short
  [InlineData("0000000000000")]     // 13 digits: too long
  [InlineData("12345A789000")]      // 12 chars, contains a letter
  [InlineData("12345678-000")]      // 12 chars, contains a dash
  [InlineData("12-345678901-2")]    // un-normalized PhilHealth format
  [InlineData("")]                  // empty
  [InlineData("١١١٢٣٤٥٦٧٨٩٠")]      // 12 Arabic-Indic digits, not ASCII 0-9
  public void IsValidPhilHealth_InvalidInput_ReturnsFalse(string input)
  {
    // Arrange + Act
    var result = GovernmentIdRules.IsValidPhilHealth(input);

    // Assert
    Assert.False(result);
  }

  [Theory]
  [InlineData("123456789000")]
  [InlineData("000000000000")]
  public void IsValidPagIbig_TwelveDigitInput_ReturnsTrue(string input)
  {
    // Arrange + Act
    var result = GovernmentIdRules.IsValidPagIbig(input);

    // Assert
    Assert.True(result);
  }

  [Theory]
  [InlineData("12345678900")]      // 11 digits: too short
  [InlineData("0000000000000")]     // 13 digits: too long
  [InlineData("12345A789000")]      // 12 chars, contains a letter
  [InlineData("12345678-000")]      // 12 chars, contains a dash
  [InlineData("1234-5678-9012")]    // un-normalized Pag-IBIG format
  [InlineData("")]                  // empty
  [InlineData("١١١٢٣٤٥٦٧٨٩٠")]      // 12 Arabic-Indic digits, not ASCII 0-9
  public void IsValidPagIbig_InvalidInput_ReturnsFalse(string input)
  {
    // Arrange + Act
    var result = GovernmentIdRules.IsValidPagIbig(input);

    // Assert
    Assert.False(result);
  }

  [Theory]
  [InlineData("123456789")]     // 9 digits
  [InlineData("123456789012")]  // 12 digits
  public void IsValidTin_NineOrTwelveDigitInput_ReturnsTrue(string input)
  {
    // Arrange + Act
    var result = GovernmentIdRules.IsValidTin(input);

    // Assert
    Assert.True(result);
  }

  [Theory]
  [InlineData("12345678")]        // 8 digits: too short
  [InlineData("1234567890")]      // 10 digits: between the two valid lengths
  [InlineData("12345678900")]     // 11 digits: between the two valid lengths
  [InlineData("1234567890000")]   // 13 chars, too long
  [InlineData("12345678A")]       // 9 chars, contains a letter
  [InlineData("12345678901A")]    // 12 chars, contains a letter
  [InlineData("")]                // empty
  [InlineData("1234567-9")]       // 9 chars, contains a dash
  [InlineData("123-456-789-000")] // un-normalized TIN format
  [InlineData("١١١٢٣٤٥٦٧٨٩٠")]     // 12 Arabic-Indic digits, not ASCII 0-9
  public void IsValidTin_InvalidInput_ReturnsFalse(string input)
  {
    // Arrange + Act
    var result = GovernmentIdRules.IsValidTin(input);

    // Assert
    Assert.False(result);
  }

  [Theory]
  [InlineData("123456789000", "123456789")]
  [InlineData("123456789", "123456789")]
  public void ToStoredTin_NineOrTwelveDigitInput_ReturnsTheFirstNine(string input, string expected)
  {
    // Arrange + Act
    var result = GovernmentIdRules.ToStoredTin(input);

    // Assert
    Assert.Equal(expected, result);
  }

  [Fact]
  public void ToStoredTin_InvalidInput_ThrowsArgumentOutOfRangeException()
  {
    // Act + Assert
    Assert.Throws<ArgumentOutOfRangeException>(() => GovernmentIdRules.ToStoredTin("1234"));
  }
}