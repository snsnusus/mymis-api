using MyMIS.Api.Helpers;

namespace MyMIS.Api.Tests.Helpers;

public class EmailRulesTests
{
  [Theory]
  [InlineData(null, "")]
  [InlineData("   JDoe@gmail.com   ", "jdoe@gmail.com")]
  [InlineData("   ", "")]
  public void Normalize_Input_ReturnsTrimmedLowercaseOrEmptyString(string? input, string expected)
  {
    // Act
    var result = EmailRules.Normalize(input);

    // Assert
    Assert.Equal(expected, result);
  }

  [Theory]
  [InlineData("juan@example.com")]
  [InlineData("juan.dela.cruz@mail.example.co.ph")]
  [InlineData("juan+tag@example.com")]
  public void IsValid_ValidAddress_ReturnsTrue(string input)
  {
    // Act
    var result = EmailRules.IsValid(input);

    // Assert
    Assert.True(result);
  }

  [Theory]
  [InlineData("jdoe")]
  [InlineData("@@")]
  [InlineData("")]
  [InlineData("jdoe@gmail")]
  public void IsValid_NotParseable_ReturnsFalse(string input)
  {
    // Act
    var result = EmailRules.IsValid(input);

    // Assert
    Assert.False(result);
  }

  [Theory]
  [InlineData("juan <juan@example.com>")]
  [InlineData("\"Juan\" <juan@example.com>")]
  public void IsValid_ParsesWithExtras_ReturnsFalse(string input)
  {
    // Act
    var result = EmailRules.IsValid(input);

    // Assert
    Assert.False(result);
  }

  [Theory]
  [InlineData("jdoe@.com")]
  public void IsValid_DomainWithoutDot_ReturnsFalse(string input)
  {
    // Act
    var result = EmailRules.IsValid(input);

    // Assert
    Assert.False(result);
  }

  [Theory]
  [InlineData("jdoe@gmail.")]
  public void IsValid_DomainEndsWithDot_ReturnsFalse(string input)
  {
    // Act
    var result = EmailRules.IsValid(input);

    // Assert
    Assert.False(result);
  }
}