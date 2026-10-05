using Xunit;
using MyMIS.Api.Helpers;

namespace MyMIS.Api.Tests.Helpers;

public class UsernameRulesTests
{

  [Theory]
  [InlineData("  jdoe  ", "jdoe")]      // trims only
  [InlineData("JDoe", "jdoe")]          // lowercases only
  [InlineData("  JDoe  ", "jdoe")]      // both together
  [InlineData("\tJDoe\n", "jdoe")]      // Trim() also removes tabs/newlines, not just spaces
  public void Normalize_MixedCaseWithWhitespace_ReturnsTrimmedLowercase(string input, string expected)
  {
    // Act
    var result = UsernameRules.Normalize(input);

    // Assert
    Assert.Equal(expected, result);
  }

  [Fact]
  public void Normalize_Null_ReturnsEmptyString()
  {
    // Act
    var result = UsernameRules.Normalize(null);

    // Assert
    Assert.Equal("", result);
  }

  [Theory]
  [InlineData("jdoe")]
  [InlineData("juan.delacruz")]
  [InlineData("j_doe-2")]
  public void Normalize_AlreadyNormalized_ReturnsUnchanged(string input)
  {
    // Act
    var result = UsernameRules.Normalize(input);

    // Assert
    Assert.Equal(input, result);
  }

  public static TheoryData<string> ValidBoundaryUsernames =>
    new(new string('a', 30));

  [Theory]
  [InlineData("jdoe")]
  [InlineData("juan.delacruz")]
  [InlineData("j_doe-2")]
  [InlineData("abc")]
  [MemberData(nameof(ValidBoundaryUsernames))]
  public void IsValid_AllowedUsername_ReturnsTrue(string input)
  {
    // Act
    var result = UsernameRules.IsValid(input);

    // Assert
    Assert.True(result);
  }

  public static TheoryData<string> InvalidBoundaryUsernames =>
    new(new string('a', 31));

  [Theory]
  [InlineData("")]
  [InlineData("ab")]
  [InlineData("1jdoe")]
  [InlineData(".jdoe")]
  [InlineData("j doe")]
  [InlineData("jdoe@corp")]
  [InlineData("niño")]
  [InlineData("JDoe")]
  [InlineData("jdoe\n")]
  [MemberData(nameof(InvalidBoundaryUsernames))]
  public void IsValid_DisallowedUsername_ReturnsFalse(string input)
  {
    // Act
    var result = UsernameRules.IsValid(input);

    // Assert
    Assert.False(result);
  }
}