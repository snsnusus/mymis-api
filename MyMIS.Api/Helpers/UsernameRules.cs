using System.Text.RegularExpressions;

namespace MyMIS.Api.Helpers;

public static partial class UsernameRules
{
  // 3–30 chars, starts with a letter, then lowercase letters, digits, . _ -
  public const string Pattern = @"^[a-z][a-z0-9._-]{2,29}\z";

  public const string Description =
    "Username must be 3–30 characters long, start with a letter, and contain only " +
    "letters, numbers, dots (.), underscores (_) or hyphens (-).";

  [GeneratedRegex(Pattern)]
  private static partial Regex ValidUsernameRegex();

  public static string Normalize(string? username) =>
    (username ?? string.Empty).Trim().ToLowerInvariant();

  // Expects an already-normalized value.
  public static bool IsValid(string normalizedUsername) =>
    ValidUsernameRegex().IsMatch(normalizedUsername);
}