using MyMIS.Api.Helpers;
using MyMIS.Api.Validation;

namespace MyMIS.Api.Tests.Validation;

public class ValidUsernameAttributeTests
{
  private readonly ValidUsernameAttribute _attribute = new();

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("     ")]

  public void IsValid_NullOrBlank_ReturnsTrue(string? input)
  {
    // Act
    var result = _attribute.IsValid(input);

    // Assert
    Assert.True(result);
  }

  [Fact]
  public void IsValid_UnnormalizedButAllowed_ReturnsTrue()
  {
    // Act
    var result = _attribute.IsValid("  JDoe  ");

    // Assert
    Assert.True(result);
  }

  [Fact]
  public void IsValid_RuleViolation_ReturnsFalse()
  {
    // Act
    var result = _attribute.IsValid("j doe");

    // Assert
    Assert.False(result);
  }

  [Fact]
  public void IsValid_NonString_ReturnsFalse()
  {
    // Act
    var result = _attribute.IsValid(42);

    // Assert
    Assert.False(result);
  }

  [Fact]
  public void FormatErrorMessage_ReturnsUsernameDescription()
  {
    // Act
    var message = _attribute.FormatErrorMessage("Username");

    // Assert
    Assert.Equal(UsernameRules.Description, message);
  }
}