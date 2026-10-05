namespace MyMIS.Api.Helpers;

public static class EmployeeCodeFormat
{
  public const string Prefix = "MYMIS";

  public const string SequenceName = "EmployeeCodeNumbers";

  // The Philippines has no daylight saving time, so Manila is always UTC+8.
  private static readonly TimeSpan ManilaOffset = TimeSpan.FromHours(8);

  public static int GetManilaYear(DateTimeOffset instant) =>
    instant.ToOffset(ManilaOffset).Year;

  public static string Format(int year, long number) =>
    $"{Prefix}-{year}-{number:D5}";
}