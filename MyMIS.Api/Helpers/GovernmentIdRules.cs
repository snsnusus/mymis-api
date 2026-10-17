namespace MyMIS.Api.Helpers;

public static class GovernmentIdRules
{
  private const int SssLength = 10;
  private const int PagIbigLength = 12;
  private const int PhilHealthLength = 12;
  private const int TinLength = 9;
  private const int TinWithBranchLength = 12;

  // Turns user input into plain digits, or null when the field was left blank.
  // "12-3456789-0" -> "1234567890", "   " -> null
  public static string? Normalize(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return null;
    }

    return value.Replace("-", "").Replace(" ", "");
  }

  public static bool IsValidSss(string digits) =>
        IsDigitsOfLength(digits, SssLength);

  public static bool IsValidPhilHealth(string digits) =>
      IsDigitsOfLength(digits, PhilHealthLength);

  public static bool IsValidPagIbig(string digits) =>
      IsDigitsOfLength(digits, PagIbigLength);

  // Accepts the 9-digit TIN, or the 12-digit form with a branch code appended.
  public static bool IsValidTin(string digits) =>
      IsDigitsOfLength(digits, TinLength) ||
      IsDigitsOfLength(digits, TinWithBranchLength);

  // Keeps only the 9-digit TIN proper, so "123456789000" and "123456789"
  // are stored the same way and the unique index can catch duplicates.
  // Call this only after IsValidTin has returned true.
  public static string ToStoredTin(string digits) =>
      digits[..TinLength];

  private static bool IsDigitsOfLength(string value, int length) =>
      value.Length == length && value.All(char.IsAsciiDigit);
}